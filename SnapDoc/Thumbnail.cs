using SkiaSharp;
using SnapDoc.Services;

namespace SnapDoc;

public class Thumbnail
{
    public static void Generate(string originalFilePath, string thumbnailPath)
    {
        var originalStream = File.OpenRead(originalFilePath);
        var skBitmap = SKBitmap.Decode(originalStream);
        string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath);
        int minSize = SettingsService.Instance.FotoThumbSize;
        float scale = minSize / (float)Math.Min(skBitmap.Width, skBitmap.Height);
        int targetWidth = (int)(skBitmap.Width * scale);
        int targetHeight = (int)(skBitmap.Height * scale);
        var resizedBitmap = new SKBitmap(targetWidth, targetHeight);
        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear);
        skBitmap.ScalePixels(resizedBitmap, samplingOptions);
        var image = SKImage.FromBitmap(resizedBitmap);
        var data = image.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoThumbQuality);
        var newStream = File.Create(thumbFilePath);
        data.SaveTo(newStream);
        newStream.Close();
        originalStream.Close();
    }
}
