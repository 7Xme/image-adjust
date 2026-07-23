using ImageAdjust.Models;
using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;
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
            int stride = bitmap.Width * 4;
            int bufferSize = bitmap.Height * stride;
            byte[] pixels = new byte[bufferSize];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, bufferSize);

            float contrastFactor = (100.0f + settings.Contrast) / 100.0f;
            contrastFactor *= contrastFactor;
            float contrastOffset = 128.0f * (1.0f - contrastFactor);

            float satFactor = (100.0f + settings.Saturation) / 100.0f;

            float shadowFactor = settings.Shadows / 100.0f;
            float highlightFactor = settings.Highlights / 100.0f;

            bool doContrast = settings.Contrast != 0;
            bool doSaturation = settings.Saturation != 0;
            bool doShadows = settings.Shadows != 0;
            bool doHighlights = settings.Highlights != 0;

            for (int i = 0; i < bufferSize; i += 4)
            {
                float r = pixels[i + 2];
                float g = pixels[i + 1];
                float b = pixels[i];

                if (doContrast)
                {
                    r = r * contrastFactor + contrastOffset;
                    g = g * contrastFactor + contrastOffset;
                    b = b * contrastFactor + contrastOffset;
                }

                if (doSaturation)
                {
                    float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                    r = gray + (r - gray) * satFactor;
                    g = gray + (g - gray) * satFactor;
                    b = gray + (b - gray) * satFactor;
                }

                if (doShadows)
                {
                    float luminance = 0.299f * r + 0.587f * g + 0.114f * b;
                    float shadowAmount = Math.Max(0, 1.0f - luminance / 128.0f);
                    float adjust = shadowFactor * shadowAmount * 128.0f;
                    r += adjust;
                    g += adjust;
                    b += adjust;
                }

                if (doHighlights)
                {
                    float luminance = 0.299f * r + 0.587f * g + 0.114f * b;
                    float highlightAmount = Math.Max(0, (luminance - 128.0f) / 127.0f);
                    float adjust = highlightFactor * highlightAmount * 128.0f;
                    r -= adjust;
                    g -= adjust;
                    b -= adjust;
                }

                pixels[i] = ClampByte((int)b);
                pixels[i + 1] = ClampByte((int)g);
                pixels[i + 2] = ClampByte((int)r);
            }

            Marshal.Copy(pixels, 0, bitmap.GetPixels(), bufferSize);
        }

        public SKBitmap ApplyAdjustments(SKBitmap source, AdjustmentSettings settings)
        {
            var result = source.Copy();
            ApplyAdjustmentsInPlace(result, settings);
            return result;
        }

        public SKRectI AutoTrimBounds(SKBitmap bitmap)
        {
            var bg = bitmap.GetPixel(0, 0);
            int bgR = bg.Red, bgG = bg.Green, bgB = bg.Blue;
            int threshold = 40;

            int x1 = 0, y1 = 0, x2 = bitmap.Width - 1, y2 = bitmap.Height - 1;

            for (int x = 0; x < bitmap.Width; x++)
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (Math.Abs(p.Red - bgR) > threshold || Math.Abs(p.Green - bgG) > threshold || Math.Abs(p.Blue - bgB) > threshold)
                    { x1 = x; goto topDone; }
                }
            topDone:;

            for (int x = bitmap.Width - 1; x >= 0; x--)
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (Math.Abs(p.Red - bgR) > threshold || Math.Abs(p.Green - bgG) > threshold || Math.Abs(p.Blue - bgB) > threshold)
                    { x2 = x; goto rightDone; }
                }
            rightDone:;

            for (int y = 0; y < bitmap.Height; y++)
                for (int x = x1; x <= x2; x++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (Math.Abs(p.Red - bgR) > threshold || Math.Abs(p.Green - bgG) > threshold || Math.Abs(p.Blue - bgB) > threshold)
                    { y1 = y; goto topEdgeDone; }
                }
            topEdgeDone:;

            for (int y = bitmap.Height - 1; y >= 0; y--)
                for (int x = x1; x <= x2; x++)
                {
                    var p = bitmap.GetPixel(x, y);
                    if (Math.Abs(p.Red - bgR) > threshold || Math.Abs(p.Green - bgG) > threshold || Math.Abs(p.Blue - bgB) > threshold)
                    { y2 = y; goto bottomDone; }
                }
            bottomDone:;

            return new SKRectI(x1, y1, x2 + 1, y2 + 1);
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
