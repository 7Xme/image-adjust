using System;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageAdjust.Services
{
    public class PrintService
    {
        public void PrintPdfBytes(byte[] pdfBytes, string jobName = "Image Adjust - ID Card")
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "ImageAdjust");
            Directory.CreateDirectory(tempPath);
            string pdfPath = Path.Combine(tempPath, "ID_Card_Print.pdf");
            File.WriteAllBytes(pdfPath, pdfBytes);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pdfPath,
                    Verb = "print",
                    UseShellExecute = true
                });
            }
            catch
            {
                PrintWithFallback(pdfBytes, jobName);
            }
        }

        private void PrintWithFallback(byte[] pdfBytes, string jobName)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                var document = new FixedDocument();
                document.DocumentPaginator.PageSize = new Size(96 * 8.56, 96 * 5.4);

                using var ms = new MemoryStream(pdfBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                for (int i = 0; i < 2; i++)
                {
                    var page = new FixedPage();
                    page.Width = document.DocumentPaginator.PageSize.Width;
                    page.Height = document.DocumentPaginator.PageSize.Height;

                    var img = new System.Windows.Controls.Image
                    {
                        Source = bitmap,
                        Width = page.Width,
                        Height = page.Height,
                        Stretch = Stretch.Uniform
                    };

                    page.Children.Add(img);
                    var pageContent = new PageContent();
                    ((IAddChild)pageContent).AddChild(page);
                    document.Pages.Add(pageContent);
                }

                dlg.PrintDocument(document.DocumentPaginator, jobName);
            }
        }

        public void PrintImageDirect(BitmapSource image, string jobName = "Image Adjust - Print")
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() == true)
            {
                var visual = new DrawingVisual();
                using var dc = visual.RenderOpen();
                dc.DrawImage(image, new Rect(0, 0, dlg.PrintableAreaWidth, dlg.PrintableAreaHeight));
                dc.Close();

                dlg.PrintVisual(visual, jobName);
            }
        }
    }
}
