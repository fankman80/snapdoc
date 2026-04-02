# nullable disable
#if IOS
using UIKit;
using CoreGraphics;
using PdfKit;
using Foundation;
#elif ANDROID
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
#elif WINDOWS
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
#endif

namespace SnapDoc
{
    public static class NativePdfRenderer
    {
        public static async Task<NativePdfDocument> OpenDocumentAsync(byte[] pdfData)
        {
            var doc = new NativePdfDocument();
#if IOS
        doc.Document = new PdfKit.PdfDocument(NSData.FromArray(pdfData));
#elif ANDROID
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, Guid.NewGuid().ToString() + ".pdf");
        await File.WriteAllBytesAsync(path, pdfData);
        doc.TempFilePath = path;
        doc.Fd = ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly);
        doc.Renderer = new PdfRenderer(doc.Fd);
#elif WINDOWS
            var ms = new InMemoryRandomAccessStream();
            await ms.WriteAsync(pdfData.AsBuffer());
            ms.Seek(0);
            doc.Document = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(ms);
#endif
            return doc;
        }

        public static async Task SavePageAsync(NativePdfDocument doc, string imgPath, int pageIndex, int dpi)
        {
            float scale = dpi / 72f;
#if WINDOWS
            if (doc.Document == null)
                return;
            using var page = doc.Document.GetPage((uint)pageIndex);
            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imgPath));
            var file = await folder.CreateFileAsync(Path.GetFileName(imgPath), Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using var outStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);

            var options = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)Math.Max(1, Math.Round(page.Size.Width * scale))
            };
            await page.RenderToStreamAsync(outStream, options);
            await outStream.FlushAsync();
#elif IOS
            if (doc.Document == null || (nint)doc.Document.PageCount <= pageIndex)
                return;
            using var page = doc.Document.GetPage(pageIndex);
            var pageRect = page.GetBoundsForBox(PdfDisplayBox.Media);
            var size = new CGSize(pageRect.Width * scale, pageRect.Height * scale);
            var renderer = new UIGraphicsImageRenderer(size);
            var image = renderer.CreateImage((context) =>
            {
                context.CGContext.SetFillColor(UIColor.White.CGColor);
                context.CGContext.FillRect(new CGRect(CGPoint.Empty, size));
                context.CGContext.TranslateCTM(0, size.Height);
                context.CGContext.ScaleCTM(scale, -scale);
                page.Draw(PdfDisplayBox.Media, context.CGContext);
            });
            using var jpegData = image.AsJPEG(0.8f);
            if (jpegData != null)
                File.WriteAllBytes(imgPath, [.. jpegData]);
#elif ANDROID
            if (doc.Renderer == null) return;
            using var page = doc.Renderer.OpenPage(pageIndex);
            int width = (int)(page.Width * scale);
            int height = (int)(page.Height * scale);
            using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
            using var canvas = new Canvas(bitmap);
            canvas.DrawColor(Android.Graphics.Color.White);
            page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
            using var stream = File.OpenWrite(imgPath);
            await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 80, stream);
#endif
        }
    }
}

namespace SnapDoc
{
    public partial class NativePdfDocument : IDisposable
    {
    #if IOS
        public PdfKit.PdfDocument Document { get; set; }
    #elif ANDROID
        public Android.Graphics.Pdf.PdfRenderer Renderer { get; set; }
        public Android.OS.ParcelFileDescriptor Fd { get; set; }
        public string TempFilePath { get; set; } // Interner Pfad für Android-Cleanup
    #elif WINDOWS
        public Windows.Data.Pdf.PdfDocument Document { get; set; }
    #endif

        public int PageCount
        {
            get
            {
    #if IOS
                return (int?)(Document?.PageCount) ?? 0;
    #elif ANDROID
                return Renderer?.PageCount ?? 0;
    #elif WINDOWS
                return (int)(Document?.PageCount ?? 0);
    #else
                return 0;
    #endif
            }
        }

        public void Dispose()
        {
            Dispose(true);
            // Dies ist der Teil, den deine Analyse eingefordert hat:
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Bereinigung der verwalteten Ressourcen (Managed Resources)
    #if IOS
                Document?.Dispose();
                Document = null;
    #elif ANDROID
                Renderer?.Dispose();
                Renderer = null;
                Fd?.Dispose();
                Fd = null;

                if (!string.IsNullOrEmpty(TempFilePath) && File.Exists(TempFilePath))
                {
                    try { File.Delete(TempFilePath); } catch { /* Ignore */ }
                }
    #elif WINDOWS
                // Das Windows PdfDocument selbst ist ein WinRT-Objekt ohne explizites IDisposable,
                // aber wir setzen die Referenz auf null, um den RC-Count zu senken.
                Document = null;
    #endif
            }
        }
    }
}