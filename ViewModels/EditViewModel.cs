using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAdjust.Models;
using ImageAdjust.Services;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageAdjust.ViewModels
{
    public partial class EditViewModel : ObservableObject
    {
        private readonly ImageProcessingService _imageService = new();
        private readonly PrintService _printService = new();
        private SKBitmap? _originalBitmap;
        private SKBitmap? _basePreviewBitmap;
        private WriteableBitmap? _writablePreview;
        private string _sourceFilePath = string.Empty;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private BitmapSource? _previewImage;

        [ObservableProperty]
        private string _windowTitle = "تحرير الصورة / Modifier l'image";

        [ObservableProperty]
        private AdjustmentSettings _settings = new();

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        public string SourceFilePath => _sourceFilePath;

        public EditViewModel(string filePath)
        {
            _sourceFilePath = filePath;
            LoadImage(filePath);

            Settings.PropertyChanged += (_, _) => QueuePreviewUpdate();
        }

        private void LoadImage(string filePath)
        {
            try
            {
                _originalBitmap = _imageService.LoadImage(filePath);
                _basePreviewBitmap = CreateBasePreview();

                int w = _basePreviewBitmap.Width;
                int h = _basePreviewBitmap.Height;
                int stride = w * 4;

                _writablePreview = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                _writablePreview.Lock();
                try
                {
                    _writablePreview.WritePixels(
                        new Int32Rect(0, 0, w, h),
                        _basePreviewBitmap.GetPixels(),
                        h * stride,
                        stride);
                }
                finally
                {
                    _writablePreview.Unlock();
                }

                PreviewImage = _writablePreview;
                WindowTitle = $"تحرير / Modifier - {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الصورة / Erreur de chargement: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private SKBitmap CreateBasePreview()
        {
            var size = GetPreviewSize(_originalBitmap!, 1200);
            return _originalBitmap!.Resize(
                new SKImageInfo(size.width, size.height), SKFilterQuality.Medium);
        }

        private async void QueuePreviewUpdate()
        {
            if (_basePreviewBitmap == null || _writablePreview == null) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                await Task.Delay(80, token);
                token.ThrowIfCancellationRequested();

                using var workingCopy = _basePreviewBitmap.Copy();
                token.ThrowIfCancellationRequested();

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    _imageService.ApplyAdjustmentsInPlace(workingCopy, Settings);
                }, token);

                token.ThrowIfCancellationRequested();

                int stride = _writablePreview.PixelWidth * 4;
                _writablePreview.Lock();
                try
                {
                    _writablePreview.WritePixels(
                        new Int32Rect(0, 0, _writablePreview.PixelWidth, _writablePreview.PixelHeight),
                        workingCopy.GetPixels(),
                        _writablePreview.PixelHeight * stride,
                        stride);
                }
                finally
                {
                    _writablePreview.Unlock();
                }
            }
            catch (OperationCanceledException) { }
        }

        private static (int width, int height) GetPreviewSize(SKBitmap bitmap, int maxDimension)
        {
            if (bitmap.Width <= maxDimension && bitmap.Height <= maxDimension)
                return (bitmap.Width, bitmap.Height);

            float ratio = (float)bitmap.Width / bitmap.Height;
            if (bitmap.Width > bitmap.Height)
                return (maxDimension, (int)(maxDimension / ratio));
            else
                return ((int)(maxDimension * ratio), maxDimension);
        }

        [RelayCommand]
        private void SaveImage()
        {
            if (_originalBitmap == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "حفظ الصورة / Enregistrer l'image",
                Filter = "JPEG Image|*.jpg|PNG Image|*.png|BMP Image|*.bmp|TIFF Image|*.tiff",
                DefaultExt = ".jpg",
                FileName = Path.GetFileNameWithoutExtension(_sourceFilePath) + "_adjusted"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var fullAdjusted = _imageService.ApplyAdjustments(_originalBitmap, Settings);
                    _imageService.SaveImage(fullAdjusted, dialog.FileName);
                    MessageBox.Show("تم حفظ الصورة / Image enregistrée",
                        "نجاح / Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطأ في الحفظ / Erreur d'enregistrement: {ex.Message}",
                        "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void PrintImage()
        {
            if (_originalBitmap == null) return;

            try
            {
                using var fullAdjusted = _imageService.ApplyAdjustments(_originalBitmap, Settings);
                var bmp = _imageService.ToBitmapSource(fullAdjusted);
                _printService.PrintImageDirect(bmp, $"طباعة - {Path.GetFileName(_sourceFilePath)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة / Erreur d'impression: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ZoomIn()
        {
            ZoomLevel = Math.Min(ZoomLevel * 1.25, 5.0);
        }

        [RelayCommand]
        private void ZoomOut()
        {
            ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.2);
        }

        [RelayCommand]
        private void ResetZoom()
        {
            ZoomLevel = 1.0;
        }

        [RelayCommand]
        private void ResetAdjustments()
        {
            Settings.Shadows = 0;
            Settings.Highlights = 0;
            Settings.Saturation = 0;
            Settings.Contrast = 0;
        }
    }
}
