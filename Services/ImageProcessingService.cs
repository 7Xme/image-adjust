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
            int w = bitmap.Width, h = bitmap.Height;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

            var result = DetectByEdgeProjection(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            result = DetectRedBorder(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            return TrimBackgroundSimple(pixels, w, h, stride);
        }

        private SKRectI? DetectByEdgeProjection(byte[] pixels, int w, int h, int stride)
        {
            byte[] gray = new byte[w * h];

            for (int y = 0; y < h; y++)
            {
                int rowSrc = y * stride;
                int rowDst = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowSrc + x * 4;
                    gray[rowDst + x] = (byte)((pixels[idx] + pixels[idx + 1] + pixels[idx + 2]) / 3);
                }
            }

            float[] rowScores = new float[h];
            for (int y = 0; y < h; y++)
            {
                int rowStart = y * w;
                float sum = 0;
                byte prev = gray[rowStart];
                for (int x = 1; x < w; x++)
                {
                    byte cur = gray[rowStart + x];
                    sum += Math.Abs(cur - prev);
                    prev = cur;
                }
                rowScores[y] = sum / w;
            }

            float[] colScores = new float[w];
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                byte prev = gray[x];
                for (int y = 1; y < h; y++)
                {
                    byte cur = gray[y * w + x];
                    sum += Math.Abs(cur - prev);
                    prev = cur;
                }
                colScores[x] = sum / h;
            }

            float rowMean = 0;
            foreach (var s in rowScores) rowMean += s;
            rowMean /= h;
            float rowVar = 0;
            foreach (var s in rowScores) rowVar += (s - rowMean) * (s - rowMean);
            float rowStd = (float)Math.Sqrt(rowVar / h);

            float colMean = 0;
            foreach (var s in colScores) colMean += s;
            colMean /= w;
            float colVar = 0;
            foreach (var s in colScores) colVar += (s - colMean) * (s - colMean);
            float colStd = (float)Math.Sqrt(colVar / w);

            float rowThreshold = rowMean + rowStd * 1.5f;
            float colThreshold = colMean + colStd * 1.5f;

            int y1 = 0;
            for (y1 = 0; y1 < h * 0.3; y1++)
                if (rowScores[y1] > rowThreshold) break;
            if (y1 >= h * 0.3) y1 = 0;

            int y2 = h - 1;
            for (y2 = h - 1; y2 > h * 0.7; y2--)
                if (rowScores[y2] > rowThreshold) break;
            if (y2 <= h * 0.7) y2 = h - 1;

            int x1 = 0;
            for (x1 = 0; x1 < w * 0.3; x1++)
                if (colScores[x1] > colThreshold) break;
            if (x1 >= w * 0.3) x1 = 0;

            int x2 = w - 1;
            for (x2 = w - 1; x2 > w * 0.7; x2--)
                if (colScores[x2] > colThreshold) break;
            if (x2 <= w * 0.7) x2 = w - 1;

            if (y1 >= y2 || x1 >= x2) return null;

            int pad = Math.Max(4, Math.Min(w, h) / 100);
            x1 = Math.Max(0, x1 - pad);
            y1 = Math.Max(0, y1 - pad);
            x2 = Math.Min(w - 1, x2 + pad);
            y2 = Math.Min(h - 1, y2 + pad);

            int rw = x2 - x1 + 1;
            int rh = y2 - y1 + 1;
            if (rw < w * 0.08 || rh < h * 0.08) return null;

            return new SKRectI(x1, y1, x2 + 1, y2 + 1);
        }

        private SKRectI? DetectRedBorder(byte[] pixels, int w, int h, int stride)
        {
            int xMin = w, yMin = h, xMax = 0, yMax = 0, count = 0;

            for (int y = 0; y < h; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowStart + x * 4;
                    int b = pixels[idx];
                    int g = pixels[idx + 1];
                    int r = pixels[idx + 2];

                    if (r > g + 25 && r > b + 25 && r > 80)
                    {
                        count++;
                        if (x < xMin) xMin = x;
                        if (y < yMin) yMin = y;
                        if (x > xMax) xMax = x;
                        if (y > yMax) yMax = y;
                    }
                }
            }

            if (count < w * h / 200) return null;

            int pad = Math.Max(5, Math.Min(w, h) / 50);
            xMin = Math.Max(0, xMin - pad);
            yMin = Math.Max(0, yMin - pad);
            xMax = Math.Min(w - 1, xMax + pad);
            yMax = Math.Min(h - 1, yMax + pad);

            return new SKRectI(xMin, yMin, xMax + 1, yMax + 1);
        }

        private SKRectI TrimBackgroundSimple(byte[] pixels, int w, int h, int stride)
        {
            int sampleSize = Math.Max(4, Math.Min(w, h) / 40);
            long sumR = 0, sumG = 0, sumB = 0;
            int total = 0;

            void SampleCorner(int startX, int startY)
            {
                int limitX = Math.Min(startX + sampleSize, w);
                int limitY = Math.Min(startY + sampleSize, h);
                for (int dy = startY; dy < limitY; dy++)
                    for (int dx = startX; dx < limitX; dx++)
                    {
                        int idx = dy * stride + dx * 4;
                        sumB += pixels[idx];
                        sumG += pixels[idx + 1];
                        sumR += pixels[idx + 2];
                        total++;
                    }
            }

            SampleCorner(0, 0);
            SampleCorner(Math.Max(0, w - sampleSize), 0);
            SampleCorner(0, Math.Max(0, h - sampleSize));
            SampleCorner(Math.Max(0, w - sampleSize), Math.Max(0, h - sampleSize));

            int avgR = (int)(sumR / Math.Max(1, total));
            int avgG = (int)(sumG / Math.Max(1, total));
            int avgB = (int)(sumB / Math.Max(1, total));

            int contrastSum = 0, contrastCount = 0;
            for (int y = 0; y < h; y += 4)
            {
                int rowStart = y * stride;
                for (int x = 0; x < w - 1; x += 4)
                {
                    int idx = rowStart + x * 4;
                    int idx2 = rowStart + (x + 1) * 4;
                    contrastSum += Math.Abs(pixels[idx] - pixels[idx2]);
                    contrastSum += Math.Abs(pixels[idx + 1] - pixels[idx2 + 1]);
                    contrastSum += Math.Abs(pixels[idx + 2] - pixels[idx2 + 2]);
                    contrastCount++;
                }
            }
            int threshold = Math.Max(30, contrastCount > 0 ? contrastSum / contrastCount : 10);

            int x1 = 0, y1 = 0, x2 = w - 1, y2 = h - 1;
            bool found = false;

            for (y1 = 0; y1 < h; y1++)
            {
                int rowStart = y1 * stride;
                for (int x = 3; x < w - 3; x++)
                {
                    int idx = rowStart + x * 4;
                    if (Math.Abs(pixels[idx] - avgB) > threshold ||
                        Math.Abs(pixels[idx + 1] - avgG) > threshold ||
                        Math.Abs(pixels[idx + 2] - avgR) > threshold)
                    { found = true; break; }
                }
                if (found) break;
            }

            found = false;
            for (y2 = h - 1; y2 >= 0; y2--)
            {
                int rowStart = y2 * stride;
                for (int x = 3; x < w - 3; x++)
                {
                    int idx = rowStart + x * 4;
                    if (Math.Abs(pixels[idx] - avgB) > threshold ||
                        Math.Abs(pixels[idx + 1] - avgG) > threshold ||
                        Math.Abs(pixels[idx + 2] - avgR) > threshold)
                    { found = true; break; }
                }
                if (found) break;
            }

            found = false;
            for (x1 = 0; x1 < w; x1++)
            {
                for (int y = y1; y <= y2; y++)
                {
                    int idx = y * stride + x1 * 4;
                    if (Math.Abs(pixels[idx] - avgB) > threshold ||
                        Math.Abs(pixels[idx + 1] - avgG) > threshold ||
                        Math.Abs(pixels[idx + 2] - avgR) > threshold)
                    { found = true; break; }
                }
                if (found) break;
            }

            found = false;
            for (x2 = w - 1; x2 >= 0; x2--)
            {
                for (int y = y1; y <= y2; y++)
                {
                    int idx = y * stride + x2 * 4;
                    if (Math.Abs(pixels[idx] - avgB) > threshold ||
                        Math.Abs(pixels[idx + 1] - avgG) > threshold ||
                        Math.Abs(pixels[idx + 2] - avgR) > threshold)
                    { found = true; break; }
                }
                if (found) break;
            }

            int pad = Math.Max(2, Math.Min(w, h) / 200);
            x1 = Math.Max(0, x1 - pad);
            y1 = Math.Max(0, y1 - pad);
            x2 = Math.Min(w - 1, x2 + pad);
            y2 = Math.Min(h - 1, y2 + pad);

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
