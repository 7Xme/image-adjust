using ImageAdjust.Models;
using SkiaSharp;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageAdjust.Services
{
    public class ImageProcessingService
    {
        private const int ThumbnailSize = 200;

        public SKBitmap LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Image file not found", filePath);
            return SKBitmap.Decode(filePath);
        }

        public BitmapSource CreateThumbnail(SKBitmap bitmap)
        {
            float scale = Math.Min((float)ThumbnailSize / bitmap.Width, (float)ThumbnailSize / bitmap.Height);
            int thumbW = (int)(bitmap.Width * scale);
            int thumbH = (int)(bitmap.Height * scale);

            using var resized = bitmap.Resize(new SKImageInfo(thumbW, thumbH), SKFilterQuality.Medium);
            if (resized == null) return ToBitmapSource(bitmap);
            return ToBitmapSource(resized);
        }

        public BitmapSource ToBitmapSource(SKBitmap bitmap)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        public void ApplyAdjustmentsInPlace(SKBitmap bitmap, AdjustmentSettings settings)
        {
            if (settings.Contrast != 0)
                ApplyContrast(bitmap, settings.Contrast);

            if (settings.Saturation != 0)
                ApplySaturation(bitmap, settings.Saturation);

            if (settings.Shadows != 0)
                ApplyShadows(bitmap, settings.Shadows);

            if (settings.Highlights != 0)
                ApplyHighlights(bitmap, settings.Highlights);
        }

        public SKBitmap ApplyAdjustments(SKBitmap source, AdjustmentSettings settings)
        {
            var result = source.Copy();
            ApplyAdjustmentsInPlace(result, settings);
            return result;
        }

        private static void ApplyContrast(SKBitmap bitmap, int contrast)
        {
            float factor = (100.0f + contrast) / 100.0f;
            factor *= factor;
            float offset = 128.0f * (1.0f - factor);

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    byte r = ClampByte((int)(pixel.Red * factor + offset));
                    byte g = ClampByte((int)(pixel.Green * factor + offset));
                    byte b = ClampByte((int)(pixel.Blue * factor + offset));
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, pixel.Alpha));
                }
            }
        }

        private static void ApplySaturation(SKBitmap bitmap, int saturation)
        {
            float factor = (100.0f + saturation) / 100.0f;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    float gray = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                    byte r = ClampByte((int)(gray + (pixel.Red - gray) * factor));
                    byte g = ClampByte((int)(gray + (pixel.Green - gray) * factor));
                    byte b = ClampByte((int)(gray + (pixel.Blue - gray) * factor));
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, pixel.Alpha));
                }
            }
        }

        private static void ApplyShadows(SKBitmap bitmap, int shadows)
        {
            float factor = shadows / 100.0f;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    float luminance = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                    float shadowAmount = Math.Max(0, 1.0f - luminance / 128.0f);
                    float adjust = factor * shadowAmount * 128.0f;

                    byte r = ClampByte((int)(pixel.Red + adjust));
                    byte g = ClampByte((int)(pixel.Green + adjust));
                    byte b = ClampByte((int)(pixel.Blue + adjust));
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, pixel.Alpha));
                }
            }
        }

        private static void ApplyHighlights(SKBitmap bitmap, int highlights)
        {
            float factor = highlights / 100.0f;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    float luminance = 0.299f * pixel.Red + 0.587f * pixel.Green + 0.114f * pixel.Blue;
                    float highlightAmount = Math.Max(0, (luminance - 128.0f) / 127.0f);
                    float adjust = factor * highlightAmount * 128.0f;

                    byte r = ClampByte((int)(pixel.Red - adjust));
                    byte g = ClampByte((int)(pixel.Green - adjust));
                    byte b = ClampByte((int)(pixel.Blue - adjust));
                    bitmap.SetPixel(x, y, new SKColor(r, g, b, pixel.Alpha));
                }
            }
        }

        public static byte ClampByte(int value)
        {
            return (byte)Math.Clamp(value, 0, 255);
        }

        public void SaveImage(SKBitmap bitmap, string filePath)
        {
            var format = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".png" => SKEncodedImageFormat.Png,
                ".bmp" => SKEncodedImageFormat.Bmp,
                _ => SKEncodedImageFormat.Jpeg
            };

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 95);
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            data.SaveTo(fs);
        }

        public SKBitmap CropToCardRatio(SKBitmap source, double targetWidth, double targetHeight)
        {
            double targetRatio = targetWidth / targetHeight;
            double sourceRatio = (double)source.Width / source.Height;

            int cropW, cropH, cropX, cropY;

            if (sourceRatio > targetRatio)
            {
                cropH = source.Height;
                cropW = (int)(source.Height * targetRatio);
                cropX = (source.Width - cropW) / 2;
                cropY = 0;
            }
            else
            {
                cropW = source.Width;
                cropH = (int)(source.Width / targetRatio);
                cropX = 0;
                cropY = (source.Height - cropH) / 2;
            }

            var rect = new SKRectI(cropX, cropY, cropX + cropW, cropY + cropH);
            return ExtractSubset(source, rect);
        }

        public SKBitmap ExtractSubset(SKBitmap source, SKRectI rect)
        {
            using var image = SKImage.FromBitmap(source);
            using var subsetImage = image.Subset(rect);
            return SKBitmap.FromImage(subsetImage) ?? source.Copy();
        }

        public SKBitmap CropRegion(SKBitmap source, CropRegion region, int fullWidth, int fullHeight)
        {
            float scaleX = (float)source.Width / fullWidth;
            float scaleY = (float)source.Height / fullHeight;

            int x = (int)(region.X * scaleX);
            int y = (int)(region.Y * scaleY);
            int w = (int)(region.Width * scaleX);
            int h = (int)(region.Height * scaleY);

            x = Math.Max(0, x);
            y = Math.Max(0, y);
            w = Math.Min(w, source.Width - x);
            h = Math.Min(h, source.Height - y);

            if (w <= 0 || h <= 0) return source.Copy();

            var rect = new SKRectI(x, y, x + w, y + h);
            return ExtractSubset(source, rect);
        }

        public (SKBitmap front, SKBitmap back) PrepareCardImages(
            SKBitmap frontImage, SKBitmap backImage,
            AdjustmentSettings settings, CropRegion? frontCrop, CropRegion? backCrop,
            int displayWidth, int displayHeight)
        {
            var front = frontCrop != null
                ? CropRegion(frontImage, frontCrop, displayWidth, displayHeight)
                : CropToCardRatio(frontImage, 856, 540);
            var back = backCrop != null
                ? CropRegion(backImage, backCrop, displayWidth, displayHeight)
                : CropToCardRatio(backImage, 856, 540);

            front = ApplyAdjustments(front, settings);
            back = ApplyAdjustments(back, settings);

            return (front, back);
        }
    }
}
