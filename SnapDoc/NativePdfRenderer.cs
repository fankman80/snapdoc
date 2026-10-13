#nullable disable
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
            await Task.CompletedTask;
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
#else
            await Task.CompletedTask;
#endif
            return doc;
        }

        /// <summary>
        /// Seitengroesse in PDF-Punkten (1/72"), inkl. Rotation.
        /// </summary>
        public static (double Width, double Height) GetPageSizePoints(NativePdfDocument doc, int pageIndex)
        {
#if WINDOWS
            if (doc.Document == null) return (0, 0);
            using var page = doc.Document.GetPage((uint)pageIndex);
            // page.Size ist in DIPs (1/96")
            return (page.Size.Width * 72.0 / 96.0, page.Size.Height * 72.0 / 96.0);
#elif IOS
            if (doc.Document == null || (nint)doc.Document.PageCount <= pageIndex) return (0, 0);
            using var page = doc.Document.GetPage(pageIndex);
            var r = page.GetBoundsForBox(PdfDisplayBox.Media);
            int rot = (int)page.Rotation;
            return (rot == 90 || rot == 270) ? (r.Height, r.Width) : (r.Width, r.Height);
#elif ANDROID
            if (doc.Renderer == null) return (0, 0);
            Android.Graphics.Pdf.PdfRenderer.Page page = null;
            try
            {
                page = doc.Renderer.OpenPage(pageIndex);
                return (page.Width, page.Height);
            }
            finally
            {
                page?.Close();
                page?.Dispose();
            }
#else
            return (0, 0);
#endif
        }

        /// <summary>
        /// Rendert eine Seite so, dass die laengste Kante maxPx Pixel hat.
        /// </summary>
        public static Task<(int Width, int Height)> SaveThumbnailAsync(
            NativePdfDocument doc, string imgPath, int pageIndex, int maxPx)
        {
            var (ptW, ptH) = GetPageSizePoints(doc, pageIndex);
            double longest = Math.Max(ptW, ptH);
            float dpi = longest > 0 ? (float)(72.0 * maxPx / longest) : 72f;
            return SavePageInternalAsync(doc, imgPath, pageIndex, dpi);
        }

        public static Task<(int Width, int Height)> SavePageAsync(
            NativePdfDocument doc, string imgPath, int pageIndex, int dpi)
            => SavePageInternalAsync(doc, imgPath, pageIndex, dpi);

        private static async Task<(int Width, int Height)> SavePageInternalAsync(
            NativePdfDocument doc, string imgPath, int pageIndex, float dpi)
        {
#if WINDOWS
            if (doc.Document == null) return (0, 0);
            using var page = doc.Document.GetPage((uint)pageIndex);

            // page.Size ist in DIPs (1/96") -> durch 96 teilen, nicht durch 72
            float scale = dpi / 96f;
            int width = (int)Math.Max(1, Math.Round(page.Size.Width * scale));
            int height = (int)Math.Max(1, Math.Round(page.Size.Height * scale));

            var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imgPath));
            var file = await folder.CreateFileAsync(Path.GetFileName(imgPath), Windows.Storage.CreationCollisionOption.ReplaceExisting);
            using var outStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);

            var options = new PdfPageRenderOptions
            {
                DestinationWidth = (uint)width,
                DestinationHeight = (uint)height,
                BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId
            };
            await page.RenderToStreamAsync(outStream, options);
            await outStream.FlushAsync();
            return (width, height);

#elif IOS
            if (doc.Document == null || (nint)doc.Document.PageCount <= pageIndex) return (0, 0);
            using var page = doc.Document.GetPage(pageIndex);

            float scale = dpi / 72f;
            var pageRect = page.GetBoundsForBox(PdfDisplayBox.Media);
            int rotation = (int)page.Rotation;
            bool isLandscape = rotation == 90 || rotation == 270;

            int width = (int)Math.Max(1, Math.Round((isLandscape ? pageRect.Height : pageRect.Width) * scale));
            int height = (int)Math.Max(1, Math.Round((isLandscape ? pageRect.Width : pageRect.Height) * scale));

            var format = new UIGraphicsImageRendererFormat { Opaque = true, Scale = 1.0f };
            var renderer = new UIGraphicsImageRenderer(new CGSize(width, height), format);

            var img = renderer.CreateImage(rendererContext =>
            {
                var context = rendererContext.CGContext;
                context.SetFillColor(UIColor.White.CGColor);
                context.FillRect(new CGRect(0, 0, width, height));
                context.TranslateCTM(0, height);
                context.ScaleCTM(1.0f, -1.0f);
                context.ScaleCTM(scale, scale);
                page.Draw(PdfDisplayBox.Media, context);
            });

            // File.Create statt OpenWrite: kuerzt eine bestehende Datei
            using var stream = File.Create(imgPath);
            using var jpegData = img.AsJPEG(0.8f);
            await jpegData.AsStream().CopyToAsync(stream);
            return (width, height);

#elif ANDROID
            if (doc.Renderer == null) return (0, 0);

            float scale = dpi / 72f;
            int width = 0;
            int height = 0;
            Android.Graphics.Pdf.PdfRenderer.Page page = null;
            try
            {
                page = doc.Renderer.OpenPage(pageIndex);
                width = (int)Math.Max(1, Math.Round(page.Width * scale));
                height = (int)Math.Max(1, Math.Round(page.Height * scale));

                using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
                using (var canvas = new Canvas(bitmap))
                {
                    canvas.DrawColor(Android.Graphics.Color.White);
                }
                page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);

                using (var stream = new FileStream(imgPath, FileMode.Create, FileAccess.Write))
                {
                    await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 80, stream);
                    await stream.FlushAsync();
                }
                bitmap.Recycle();
            }
            finally
            {
                page?.Close();
                page?.Dispose();
            }
            return (width, height);
#else
            await Task.CompletedTask;
            return (0, 0);
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
        public string TempFilePath { get; set; }
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
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
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
                try { File.Delete(TempFilePath); } catch { }
            }
#elif WINDOWS
            Document = null;
#endif
        }
    }
}