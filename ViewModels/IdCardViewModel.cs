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
    public partial class IdCardViewModel : ObservableObject
    {
        private readonly ImageProcessingService _imageService = new();
        private readonly PdfService _pdfService = new();
        private readonly PrintService _printService = new();

        private SKBitmap? _originalFront;
        private SKBitmap? _originalBack;
        private SKBitmap? _baseFrontPreview;
        private SKBitmap? _baseBackPreview;
        private WriteableBitmap? _writableFront;
        private WriteableBitmap? _writableBack;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private BitmapSource? _frontPreview;

        [ObservableProperty]
        private BitmapSource? _backPreview;

        [ObservableProperty]
        private AdjustmentSettings _settings = new();

        [ObservableProperty]
        private CropRegion _frontCrop = new();

        [ObservableProperty]
        private CropRegion _backCrop = new();

        [ObservableProperty]
        private bool _isCroppingFront;

        [ObservableProperty]
        private bool _isCroppingBack;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _windowTitle = "بطاقة التعريف / Carte d'identité";

        public int DisplayWidth { get; private set; } = 400;
        public int DisplayHeight { get; private set; } = 252;

        public IdCardViewModel(string frontPath, string backPath)
        {
            LoadImages(frontPath, backPath);
            InitCropRegions();

            Settings.PropertyChanged += (_, _) => QueuePreviewUpdate();
        }

        private void LoadImages(string frontPath, string backPath)
        {
            try
            {
                _originalFront = _imageService.LoadImage(frontPath);
                _originalBack = _imageService.LoadImage(backPath);

                _baseFrontPreview = CreateBasePreview(_originalFront);
                _baseBackPreview = CreateBasePreview(_originalBack);

                _writableFront = CreateWritable(_baseFrontPreview);
                _writableBack = CreateWritable(_baseBackPreview);

                FrontPreview = _writableFront;
                BackPreview = _writableBack;

                WindowTitle = $"بطاقة التعريف / Carte d'identité - {Path.GetFileName(frontPath)} & {Path.GetFileName(backPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الصور / Erreur de chargement: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private WriteableBitmap CreateWritable(SKBitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int stride = w * 4;
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            wb.Lock();
            try
            {
                wb.WritePixels(new Int32Rect(0, 0, w, h), bmp.GetPixels(), h * stride, stride);
            }
            finally
            {
                wb.Unlock();
            }
            return wb;
        }

        private void UpdateWritable(WriteableBitmap wb, SKBitmap bmp)
        {
            int stride = wb.PixelWidth * 4;
            wb.Lock();
            try
            {
                wb.WritePixels(
                    new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight),
                    bmp.GetPixels(),
                    wb.PixelHeight * stride,
                    stride);
            }
            finally
            {
                wb.Unlock();
            }
        }

        private SKBitmap CreateBasePreview(SKBitmap original)
        {
            var scale = Math.Min(
                (float)DisplayWidth / original.Width,
                (float)DisplayHeight / original.Height);
            int pw = (int)(original.Width * scale);
            int ph = (int)(original.Height * scale);
            return original.Resize(new SKImageInfo(pw, ph), SKFilterQuality.Medium);
        }

        private void InitCropRegions()
        {
            FrontCrop.Set(0, 0, DisplayWidth, DisplayHeight);
            BackCrop.Set(0, 0, DisplayWidth, DisplayHeight);
        }

        private async void QueuePreviewUpdate()
        {
            if (_baseFrontPreview == null || _baseBackPreview == null ||
                _writableFront == null || _writableBack == null) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsProcessing = true;

            try
            {
                await Task.Delay(80, token);
                token.ThrowIfCancellationRequested();

                using var frontCopy = _baseFrontPreview.Copy();
                using var backCopy = _baseBackPreview.Copy();
                token.ThrowIfCancellationRequested();

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    _imageService.ApplyAdjustmentsInPlace(frontCopy, Settings);
                    _imageService.ApplyAdjustmentsInPlace(backCopy, Settings);
                }, token);

                token.ThrowIfCancellationRequested();

                UpdateWritable(_writableFront, frontCopy);
                UpdateWritable(_writableBack, backCopy);
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsProcessing = false;
            }
        }

        private void UpdatePreview()
        {
            QueuePreviewUpdate();
        }

        [RelayCommand]
        private void ResetCrop()
        {
            InitCropRegions();
            UpdatePreview();
        }

        [RelayCommand]
        private void AutoCropFront()
        {
            if (_originalFront == null) return;
            var rect = _imageService.AutoTrimBounds(_originalFront);
            float sx = (float)DisplayWidth / _originalFront.Width;
            float sy = (float)DisplayHeight / _originalFront.Height;
            FrontCrop.Set((int)(rect.Left * sx), (int)(rect.Top * sy),
                          (int)(rect.Width * sx), (int)(rect.Height * sy));
            IsCroppingFront = true;
            IsCroppingBack = false;
        }

        [RelayCommand]
        private void AutoCropBack()
        {
            if (_originalBack == null) return;
            var rect = _imageService.AutoTrimBounds(_originalBack);
            float sx = (float)DisplayWidth / _originalBack.Width;
            float sy = (float)DisplayHeight / _originalBack.Height;
            BackCrop.Set((int)(rect.Left * sx), (int)(rect.Top * sy),
                         (int)(rect.Width * sx), (int)(rect.Height * sy));
            IsCroppingBack = true;
            IsCroppingFront = false;
        }

        [RelayCommand]
        private void ToggleCropFront()
        {
            IsCroppingFront = !IsCroppingFront;
            IsCroppingBack = false;
        }

        [RelayCommand]
        private void ToggleCropBack()
        {
            IsCroppingBack = !IsCroppingBack;
            IsCroppingFront = false;
        }

        [RelayCommand]
        private void ResetAdjustments()
        {
            Settings.Shadows = 0;
            Settings.Highlights = 0;
            Settings.Saturation = 0;
            Settings.Contrast = 0;
        }

        [RelayCommand]
        private async Task SavePdf()
        {
            if (_originalFront == null || _originalBack == null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "حفظ PDF / Enregistrer le PDF",
                Filter = "PDF File|*.pdf",
                DefaultExt = ".pdf",
                FileName = "ID_Card.pdf"
            };

            if (dialog.ShowDialog() != true) return;

            IsProcessing = true;

            try
            {
                var (front, back) = await Task.Run(() =>
                    _imageService.PrepareCardImages(
                        _originalFront, _originalBack, Settings,
                        IsCroppingFront ? FrontCrop : null,
                        IsCroppingBack ? BackCrop : null,
                        DisplayWidth, DisplayHeight));

                var pdfBytes = await Task.Run(() => _pdfService.GenerateCardPdf(front, back));

                await File.WriteAllBytesAsync(dialog.FileName, pdfBytes);

                MessageBox.Show("تم حفظ PDF / PDF enregistré",
                    "نجاح / Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الحفظ / Erreur d'enregistrement: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task PrintIdCard()
        {
            if (_originalFront == null || _originalBack == null) return;

            IsProcessing = true;

            try
            {
                var (front, back) = await Task.Run(() =>
                    _imageService.PrepareCardImages(
                        _originalFront, _originalBack, Settings,
                        IsCroppingFront ? FrontCrop : null,
                        IsCroppingBack ? BackCrop : null,
                        DisplayWidth, DisplayHeight));

                var pdfBytes = await Task.Run(() => _pdfService.GenerateCardPdf(front, back));

                _printService.PrintPdfBytes(pdfBytes, "Image Adjust - بطاقة التعريف / Carte d'identité");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في الطباعة / Erreur d'impression: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
