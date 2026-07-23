using PdfSharp.Pdf;
using PdfSharp.Drawing;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageAdjust.Services
{
    public class PdfService
    {
        private const double CardWidthMm = 85.6;
        private const double CardHeightMm = 54.0;
        private const double MmToPoint = 72.0 / 25.4;

        private static double CardWidthPt => CardWidthMm * MmToPoint;
        private static double CardHeightPt => CardHeightMm * MmToPoint;

        public byte[] GenerateCardPdf(SKBitmap frontImage, SKBitmap backImage)
        {
            using var doc = new PdfDocument();
            var streams = new List<MemoryStream>();
            var images = new List<XImage>();

            try
            {
                images.Add(BuildXImage(frontImage, streams));
                images.Add(BuildXImage(backImage, streams));

                foreach (var img in images)
                    WriteCardPage(doc, img);

                var ms = new MemoryStream();
                doc.Save(ms, false);
                return ms.ToArray();
            }
            finally
            {
                foreach (var img in images) img.Dispose();
                foreach (var s in streams) s.Dispose();
            }
        }

        private static XImage BuildXImage(SKBitmap bitmap, List<MemoryStream> streams)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);

            var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

            var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));
            encoder.Save(ms);
            ms.Position = 0;
            streams.Add(ms);

            return XImage.FromStream(ms);
        }

        private static void WriteCardPage(PdfDocument doc, XImage xImage)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(CardWidthMm);
            page.Height = XUnit.FromMillimeter(CardHeightMm);

            using var gfx = XGraphics.FromPdfPage(page);

            double imgW = xImage.PixelWidth;
            double imgH = xImage.PixelHeight;
            double scale = Math.Min(CardWidthPt / imgW, CardHeightPt / imgH);
            double drawW = imgW * scale;
            double drawH = imgH * scale;
            double drawX = (CardWidthPt - drawW) / 2;
            double drawY = (CardHeightPt - drawH) / 2;

            gfx.DrawImage(xImage, drawX, drawY, drawW, drawH);
        }
    }
}
