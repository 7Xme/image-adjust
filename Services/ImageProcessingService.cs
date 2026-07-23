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

        public SKRectI AutoTrimBounds(SKBitmap bitmap, CardTemplateProfile? profile = null)
        {
            int w = bitmap.Width, h = bitmap.Height;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

            SKRectI? result;

            result = DetectByEdgePeaks(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            result = DetectByEdgeDensity(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            result = DetectByDominantColor(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            result = DetectByContentScore(pixels, w, h, stride);
            if (result.HasValue) return result.Value;

            return TrimBackgroundSimple(pixels, w, h, stride);
        }

        private float[] ComputeRowDensity(int[] edgeMag, int w, int h, float threshold)
        {
            float[] d = new float[h];
            for (int y = 0; y < h; y++)
            {
                int start = y * w;
                int count = 0;
                for (int x = 0; x < w; x++)
                    if (edgeMag[start + x] > threshold) count++;
                d[y] = (float)count / w;
            }
            return d;
        }

        private float[] ComputeColDensity(int[] edgeMag, int w, int h, float threshold)
        {
            float[] d = new float[w];
            for (int x = 0; x < w; x++)
            {
                int count = 0;
                for (int y = 0; y < h; y++)
                    if (edgeMag[y * w + x] > threshold) count++;
                d[x] = (float)count / h;
            }
            return d;
        }

        private float PercentileThreshold(float[] data, double pct)
        {
            float[] s = (float[])data.Clone();
            Array.Sort(s);
            return s[(int)(s.Length * Math.Min(pct, 0.999))];
        }

        private int? ScanEdge(float[] profile, int start, int end, int step, float threshold)
        {
            for (int i = start; step > 0 ? i <= end : i >= end; i += step)
                if (profile[i] > threshold) return i;
            return null;
        }

        private float[] SmoothProfile(float[] data, int kernelSize)
        {
            int k = Math.Max(1, kernelSize);
            float[] smoothed = new float[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int start = Math.Max(0, i - k);
                int end = Math.Min(data.Length - 1, i + k);
                double sum = 0;
                for (int j = start; j <= end; j++) sum += data[j];
                smoothed[i] = (float)(sum / (end - start + 1));
            }
            return smoothed;
        }

        private int? FindStrongestExtremum(float[] data, int start, int end, bool positive)
        {
            int best = -1;
            float bestVal = positive ? float.MinValue : float.MaxValue;
            for (int i = Math.Max(1, start); i <= Math.Min(end, data.Length - 2); i++)
            {
                if (!positive && data[i] < bestVal) { bestVal = data[i]; best = i; }
                else if (positive && data[i] > bestVal) { bestVal = data[i]; best = i; }
            }
            if (best < 0) return null;

            float mean = 0;
            for (int i = 0; i < data.Length; i++) mean += data[i];
            mean /= data.Length;
            float dev = bestVal - mean;
            if (Math.Abs(dev) < Math.Abs(mean) * 0.05f) return null;
            return best;
        }

        private SKRectI? DetectByEdgePeaks(byte[] pixels, int w, int h, int stride)
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

            int[] gx = new int[w * h], gy = new int[w * h];
            for (int y = 1; y < h - 1; y++)
            {
                int row = y * w, up = (y - 1) * w, dn = (y + 1) * w;
                for (int x = 1; x < w - 1; x++)
                {
                    gx[row + x] = gray[row + x + 1] - gray[row + x - 1];
                    gy[row + x] = gray[dn + x] - gray[up + x];
                }
            }

            float[] rowProj = new float[h], colProj = new float[w];
            for (int y = 1; y < h - 1; y++)
            {
                long sum = 0; int row = y * w;
                for (int x = 1; x < w - 1; x++) sum += gy[row + x];
                rowProj[y] = sum;
            }
            for (int x = 1; x < w - 1; x++)
            {
                long sum = 0;
                for (int y = 1; y < h - 1; y++) sum += gx[y * w + x];
                colProj[x] = sum;
            }

            int k = Math.Max(3, Math.Min(w, h) / 60);
            rowProj = SmoothProfile(rowProj, k);
            colProj = SmoothProfile(colProj, k);

            int? top = FindStrongestExtremum(rowProj, 0, h / 2, positive: true);
            int? bottom = FindStrongestExtremum(rowProj, h / 2, h - 1, positive: false);
            int? left = FindStrongestExtremum(colProj, 0, w / 2, positive: true);
            int? right = FindStrongestExtremum(colProj, w / 2, w - 1, positive: false);

            if (top == null || bottom == null || left == null || right == null)
                return null;

            int y1 = top.Value, y2 = bottom.Value;
            int x1 = left.Value, x2 = right.Value;

            if (y1 >= y2 || x1 >= x2) return null;

            int cardW = x2 - x1 + 1, cardH = y2 - y1 + 1;
            if (cardW < w * 0.08 || cardH < h * 0.08) return null;

            double ratio = (double)cardW / cardH;
            double target = 856.0 / 540;
            double ratioOk = Math.Min(ratio, target) / Math.Max(ratio, target);
            if (ratioOk < 0.5) return null;

            int pad = Math.Max(4, Math.Min(w, h) / 100);
            x1 = Math.Max(0, x1 - pad);
            y1 = Math.Max(0, y1 - pad);
            x2 = Math.Min(w - 1, x2 + pad);
            y2 = Math.Min(h - 1, y2 + pad);

            return new SKRectI(x1, y1, x2 + 1, y2 + 1);
        }

        private SKRectI? DetectByEdgeDensity(byte[] pixels, int w, int h, int stride)
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

            int[] edgeMag = new int[w * h];
            int maxMag = 0;
            for (int y = 1; y < h - 1; y++)
            {
                int row = y * w;
                int up = (y - 1) * w;
                int dn = (y + 1) * w;
                for (int x = 1; x < w - 1; x++)
                {
                    int gx = Math.Abs(gray[row + x + 1] - gray[row + x - 1]);
                    int gy = Math.Abs(gray[dn + x] - gray[up + x]);
                    int mag = gx + gy;
                    edgeMag[row + x] = mag;
                    if (mag > maxMag) maxMag = mag;
                }
            }

            if (maxMag == 0) return null;

            float edgeThresh = maxMag * 0.10f;
            float[] rowDensity = ComputeRowDensity(edgeMag, w, h, edgeThresh);
            float[] colDensity = ComputeColDensity(edgeMag, w, h, edgeThresh);

            float row85 = PercentileThreshold(rowDensity, 0.85);
            float rowThresh = Math.Max(0.02f, row85);

            float col85 = PercentileThreshold(colDensity, 0.85);
            float colThresh = Math.Max(0.02f, col85);

            int y1 = ScanEdge(rowDensity, 0, (int)(h * 0.45), 1, rowThresh) ?? 0;
            int y2 = ScanEdge(rowDensity, h - 1, (int)(h * 0.55), -1, rowThresh) ?? (h - 1);
            int x1 = ScanEdge(colDensity, 0, (int)(w * 0.45), 1, colThresh) ?? 0;
            int x2 = ScanEdge(colDensity, w - 1, (int)(w * 0.55), -1, colThresh) ?? (w - 1);

            if (y1 >= y2 || x1 >= x2) return null;

            int rw = x2 - x1 + 1;
            int rh = y2 - y1 + 1;
            if (rw < w * 0.08 || rh < h * 0.08) return null;

            int pad = Math.Max(4, Math.Min(w, h) / 80);
            x1 = Math.Max(0, x1 - pad);
            y1 = Math.Max(0, y1 - pad);
            x2 = Math.Min(w - 1, x2 + pad);
            y2 = Math.Min(h - 1, y2 + pad);

            return new SKRectI(x1, y1, x2 + 1, y2 + 1);
        }

        private SKRectI? DetectByDominantColor(byte[] pixels, int w, int h, int stride)
        {
            int step = Math.Max(8, Math.Min(w, h) / 60);
            const int B = 8;
            int[,,] buckets = new int[B, B, B];
            int total = 0;

            for (int y = 0; y < h; y += step)
            {
                int rowStart = y * stride;
                for (int x = 0; x < w; x += step)
                {
                    int idx = rowStart + x * 4;
                    int r = pixels[idx + 2] * B / 256;
                    int g = pixels[idx + 1] * B / 256;
                    int bb = pixels[idx] * B / 256;
                    buckets[r, g, bb]++;
                    total++;
                }
            }

            int bestR = 0, bestG = 0, bestB = 0, bestCount = 0;
            for (int br = 0; br < B; br++)
                for (int bg = 0; bg < B; bg++)
                    for (int bb = 0; bb < B; bb++)
                    {
                        int cnt = buckets[br, bg, bb];
                        if (cnt <= bestCount) continue;
                        int r = (br + 1) * 256 / B - 1;
                        int g = (bg + 1) * 256 / B - 1;
                        int b = (bb + 1) * 256 / B - 1;
                        int avg = (r + g + b) / 3;
                        int range = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                        if (avg < 50 || avg > 235 || range > 90) continue;
                        bestCount = cnt;
                        bestR = r; bestG = g; bestB = b;
                    }

            if (bestCount < total * 0.02) return null;

            int maxDist = 45;
            float[] rowScore = new float[h];
            float[] colScore = new float[w];

            for (int y = 0; y < h; y++)
            {
                int rowStart = y * stride;
                int match = 0;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowStart + x * 4;
                    int dr = Math.Abs(pixels[idx + 2] - bestR);
                    int dg = Math.Abs(pixels[idx + 1] - bestG);
                    int db = Math.Abs(pixels[idx] - bestB);
                    if (dr + dg + db < maxDist) match++;
                }
                rowScore[y] = (float)match / w;
            }
            for (int x = 0; x < w; x++)
            {
                int match = 0;
                for (int y = 0; y < h; y++)
                {
                    int idx = y * stride + x * 4;
                    int dr = Math.Abs(pixels[idx + 2] - bestR);
                    int dg = Math.Abs(pixels[idx + 1] - bestG);
                    int db = Math.Abs(pixels[idx] - bestB);
                    if (dr + dg + db < maxDist) match++;
                }
                colScore[x] = (float)match / h;
            }

            float r85 = PercentileThreshold(rowScore, 0.85);
            float rowThresh = Math.Max(0.05f, r85);
            float c85 = PercentileThreshold(colScore, 0.85);
            float colThresh = Math.Max(0.05f, c85);

            int y1 = ScanEdge(rowScore, 0, (int)(h * 0.4), 1, rowThresh) ?? 0;
            int y2 = ScanEdge(rowScore, h - 1, (int)(h * 0.6), -1, rowThresh) ?? (h - 1);
            int x1 = ScanEdge(colScore, 0, (int)(w * 0.4), 1, colThresh) ?? 0;
            int x2 = ScanEdge(colScore, w - 1, (int)(w * 0.6), -1, colThresh) ?? (w - 1);

            if (y1 >= y2 || x1 >= x2) return null;

            int rw = x2 - x1 + 1, rh = y2 - y1 + 1;
            if (rw < w * 0.08 || rh < h * 0.08) return null;

            int pad = Math.Max(4, Math.Min(w, h) / 80);
            return new SKRectI(Math.Max(0, x1 - pad), Math.Max(0, y1 - pad),
                               Math.Min(w, x2 + pad + 1), Math.Min(h, y2 + pad + 1));
        }

        private SKRectI? DetectByContentScore(byte[] pixels, int w, int h, int stride)
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

            int[] edgeMag = new int[w * h];
            int maxMag = 0;
            for (int y = 1; y < h - 1; y++)
            {
                int row = y * w;
                int up = (y - 1) * w;
                int dn = (y + 1) * w;
                for (int x = 1; x < w - 1; x++)
                {
                    int gx = Math.Abs(gray[row + x + 1] - gray[row + x - 1]);
                    int gy = Math.Abs(gray[dn + x] - gray[up + x]);
                    int mag = gx + gy;
                    edgeMag[row + x] = mag;
                    if (mag > maxMag) maxMag = mag;
                }
            }

            if (maxMag == 0) return null;

            float edgeThresh = maxMag * 0.10f;
            float[] rowDensity = ComputeRowDensity(edgeMag, w, h, edgeThresh);
            float[] colDensity = ComputeColDensity(edgeMag, w, h, edgeThresh);

            float rThresh = PercentileThreshold(rowDensity, 0.70);
            float cThresh = PercentileThreshold(colDensity, 0.70);
            rThresh = Math.Max(0.02f, rThresh);
            cThresh = Math.Max(0.02f, cThresh);

            int bestY1 = 0, bestY2 = h - 1, bestX1 = 0, bestX2 = w - 1;
            double bestScore = -1;
            int maxShift = Math.Min(w, h) / 10;
            int steps = Math.Max(5, maxShift / 15);

            for (int shift = 0; shift <= maxShift; shift += Math.Max(1, steps))
            {
                int sy1 = ScanEdge(rowDensity, shift, (int)(h * 0.45), 1, rThresh) ?? shift;
                int sy2 = ScanEdge(rowDensity, h - 1 - shift, (int)(h * 0.55), -1, rThresh) ?? (h - 1 - shift);
                int sx1 = ScanEdge(colDensity, shift, (int)(w * 0.45), 1, cThresh) ?? shift;
                int sx2 = ScanEdge(colDensity, w - 1 - shift, (int)(w * 0.55), -1, cThresh) ?? (w - 1 - shift);

                if (sy1 >= sy2 || sx1 >= sx2) continue;

                double avgScore = 0;
                int count = 0;
                for (int y = sy1; y <= sy2; y++)
                    for (int x = sx1; x <= sx2; x++)
                    {
                        avgScore += edgeMag[y * w + x];
                        count++;
                    }
                avgScore /= count;

                int rw = sx2 - sx1 + 1, rh = sy2 - sy1 + 1;
                double areaRatio = (double)(rw * rh) / (w * h);
                double score = avgScore * areaRatio;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestY1 = sy1; bestY2 = sy2;
                    bestX1 = sx1; bestX2 = sx2;
                }
            }

            if (bestScore < 0) return null;

            int pw = bestX2 - bestX1 + 1, ph = bestY2 - bestY1 + 1;
            if (pw < w * 0.06 || ph < h * 0.06) return null;

            int pad = Math.Max(3, Math.Min(w, h) / 100);
            bestX1 = Math.Max(0, bestX1 - pad);
            bestY1 = Math.Max(0, bestY1 - pad);
            bestX2 = Math.Min(w - 1, bestX2 + pad);
            bestY2 = Math.Min(h - 1, bestY2 + pad);

            return new SKRectI(bestX1, bestY1, bestX2 + 1, bestY2 + 1);
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
