using PdfSharp.Pdf;
using PdfSharp.Drawing;
using SkiaSharp;
using System.IO;

namespace ImageAdjust.Services
{
    public class PdfService
    {
        private const double CardWidthMm = 85.6;
        private const double CardHeightMm = 54.0;
        private const double MmToPoint = 72.0 / 25.4;

        public double CardWidthPt => CardWidthMm * MmToPoint;
        public double CardHeightPt => CardHeightMm * MmToPoint;

        public byte[] GenerateCardPdf(SKBitmap frontImage, SKBitmap backImage)
        {
            using var doc = new PdfDocument();

            AddCardPage(doc, frontImage);
            AddCardPage(doc, backImage);

            using var ms = new MemoryStream();
            doc.Save(ms, false);
            return ms.ToArray();
        }

        private void AddCardPage(PdfDocument doc, SKBitmap cardImage)
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromMillimeter(CardWidthMm);
            page.Height = XUnit.FromMillimeter(CardHeightMm);

            using var gfx = XGraphics.FromPdfPage(page);

            using var image = SKImage.FromBitmap(cardImage);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            byte[] imageBytes = data.ToArray();

            using var ms = new MemoryStream(imageBytes);
            var xImage = XImage.FromStream(ms);

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
