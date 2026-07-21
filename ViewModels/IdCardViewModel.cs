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
    public partial class IdCardViewModel : ObservableObject
    {
        private readonly ImageProcessingService _imageService = new();
        private readonly PdfService _pdfService = new();
        private readonly PrintService _printService = new();

        private SKBitmap? _originalFront;
        private SKBitmap? _originalBack;
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

                WindowTitle = $"بطاقة التعريف / Carte d'identité - {Path.GetFileName(frontPath)} & {Path.GetFileName(backPath)}";

                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل الصور / Erreur de chargement: {ex.Message}",
                    "خطأ / Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitCropRegions()
        {
            FrontCrop.Set(0, 0, DisplayWidth, DisplayHeight);
            BackCrop.Set(0, 0, DisplayWidth, DisplayHeight);
        }

        private async void QueuePreviewUpdate()
        {
            if (_originalFront == null || _originalBack == null) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsProcessing = true;

            try
            {
                await Task.Delay(80, token);
                token.ThrowIfCancellationRequested();

                var scale = Math.Min(
                    (float)DisplayWidth / _originalFront.Width,
                    (float)DisplayHeight / _originalFront.Height);
                int pw = (int)(_originalFront.Width * scale);
                int ph = (int)(_originalFront.Height * scale);

                using var frontResized = _originalFront.Resize(new SKImageInfo(pw, ph), SKFilterQuality.Medium);
                using var backResized = _originalBack.Resize(new SKImageInfo(pw, ph), SKFilterQuality.Medium);
                if (frontResized == null || backResized == null) return;
                token.ThrowIfCancellationRequested();

                await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    _imageService.ApplyAdjustmentsInPlace(frontResized, Settings);
                    _imageService.ApplyAdjustmentsInPlace(backResized, Settings);
                }, token);

                token.ThrowIfCancellationRequested();

                FrontPreview = _imageService.ToBitmapSource(frontResized);
                BackPreview = _imageService.ToBitmapSource(backResized);
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
        private async Task PrintIdCard()
        {
            if (_originalFront == null || _originalBack == null) return;

            IsProcessing = true;

            try
            {
                var (front, back) = await Task.Run(() =>
                    _imageService.PrepareCardImages(
                        _originalFront, _originalBack, Settings,
                        null, null, DisplayWidth, DisplayHeight));

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
