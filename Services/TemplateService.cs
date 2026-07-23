using ImageAdjust.Models;
using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;

namespace ImageAdjust.Services
{
    public class TemplateService
    {
        private readonly string _folderPath;
        private CardTemplateProfile? _cachedProfile;

        public TemplateService(string folderPath)
        {
            _folderPath = folderPath;
        }

        public CardTemplateProfile LoadProfile()
        {
            if (_cachedProfile != null) return _cachedProfile;

            if (!Directory.Exists(_folderPath))
            {
                _cachedProfile = new CardTemplateProfile();
                return _cachedProfile;
            }

            var files = Directory.GetFiles(_folderPath, "*.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
            {
                _cachedProfile = new CardTemplateProfile();
                return _cachedProfile;
            }

            double totalRedness = 0, totalIntensity = 0;
            double totalAspectRatio = 0;
            int count = 0;

            foreach (var file in files)
            {
                using var bmp = SKBitmap.Decode(file);
                if (bmp == null) continue;

                var (avgRedness, avgIntensity) = AnalyzeBorderPixels(bmp);
                if (avgRedness > 0)
                {
                    totalRedness += avgRedness;
                    totalIntensity += avgIntensity;
                    totalAspectRatio += (double)bmp.Width / bmp.Height;
                    count++;
                }
            }

            if (count == 0)
            {
                _cachedProfile = new CardTemplateProfile();
                return _cachedProfile;
            }

            _cachedProfile = new CardTemplateProfile
            {
                MinRedness = (totalRedness / count) * 0.65,
                MinRedIntensity = (totalIntensity / count) * 0.7,
                CardAspectRatio = totalAspectRatio / count,
                TemplateCount = count
            };

            return _cachedProfile;
        }

        private static (double avgRedness, double avgIntensity) AnalyzeBorderPixels(SKBitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            Marshal.Copy(bmp.GetPixels(), pixels, 0, pixels.Length);

            int marginW = Math.Max(4, w / 14);
            int marginH = Math.Max(4, h / 14);

            double totalRedness = 0, totalIntensity = 0;
            int count = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool onBorder = y < marginH || y >= h - marginH ||
                                    x < marginW || x >= w - marginW;
                    if (!onBorder) continue;

                    int idx = y * stride + x * 4;
                    int r = pixels[idx + 2];
                    int g = pixels[idx + 1];
                    int b = pixels[idx];
                    int maxOther = Math.Max(g, b);
                    int redness = r - maxOther;

                    if (redness > 10)
                    {
                        totalRedness += redness;
                        totalIntensity += r;
                        count++;
                    }
                }
            }

            if (count < 50) return (0, 0);
            return (totalRedness / count, totalIntensity / count);
        }
    }
}
