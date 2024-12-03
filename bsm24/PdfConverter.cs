using PDFtoImage;
using SkiaSharp;

namespace bsm24;

class PdfConverter
{
    public static List<Image> Convert(FileResult path)
    {
        var root = GlobalJson.Data;
        byte[] bytearray = File.ReadAllBytes(path.FullPath);
        int pagecount = Conversion.GetPageCount(bytearray);
        var imageList = new List<Image>();

        for (int i = 0; i < pagecount; i++)
        {
            string imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, "plan_" + i + ".jpg");
            Conversion.SaveJpeg(imgPath, bytearray, i, options: new RenderOptions(Dpi: 300));

            // Bildgrösse auslesen
            var stream = File.OpenRead(imgPath);
            var skBitmap = SKBitmap.Decode(stream);
            Size _imgSize = new(skBitmap.Width, skBitmap.Height);
        }

        return imageList;

    }
}
