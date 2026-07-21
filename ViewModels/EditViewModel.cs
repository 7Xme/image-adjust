using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageAdjust.Models;
using ImageAdjust.Services;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageAdjust.ViewModels
{
    public partial class EditViewModel : ObservableObject
    {
        private readonly ImageProcessingService _imageService = new();
        private readonly PrintService _printService = new();
        private SKBitmap? _originalBitmap;
        private string _sourceFilePath = string.Empty;

        [ObservableProperty]
        private BitmapSource? _previewImage;

        [ObservableProperty]
        private string _windowTitle = "تحرير الصورة / Modifier l'image";

        [ObservableProperty]
        private AdjustmentSettings _settings = new();

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        private bool _isProcessing;

        public string SourceFilePath => _sourceFilePath;

        public EditViewModel(string filePath)
        {
            _sourceFilePath = filePath;
            LoadImage(filePath);

            Settings.PropertyChanged += (_, _) => ApplyAdjustments();
        }

        private void LoadImage(string filePath)
        {
            try
            {
                _originalBitmap = _imageService.LoadImage(filePath);
                PreviewImage = _imageService.ToBitmapSource(_originalBitmap);
                WindowTitle = $"تحرير / Modifier - {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الصورة / Erreur de chargement: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ApplyAdjustments()
        {
            if (_originalBitmap == null || _isProcessing) return;

            _isProcessing = true;

            try
            {
                var previewSize = GetPreviewSize(_originalBitmap, 1200);
                using var previewBmp = _originalBitmap.Resize(
                    new SKImageInfo(previewSize.width, previewSize.height), SKFilterQuality.Medium);

                if (previewBmp == null) return;

                await Task.Run(() =>
                {
                    _imageService.ApplyAdjustments(previewBmp, Settings);
                });

                PreviewImage = _imageService.ToBitmapSource(previewBmp);
            }
            catch { }
            finally
            {
                _isProcessing = false;
            }
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
