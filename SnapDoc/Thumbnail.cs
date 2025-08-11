using SkiaSharp;

namespace SnapDoc;

class Thumbnail
{
    public static void Generate(string originalFilePath, string thumbnailPath)
    {
        var originalStream = File.OpenRead(originalFilePath);
        var skBitmap = SKBitmap.Decode(originalStream);
        string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath);

        // Zielgröße festlegen (keine Kante kürzer als 150 Pixel)
        int minSize = Settings.ThumbSize;

        // Berechne den Skalierungsfaktor basierend auf der kürzeren Seite
        float scale = minSize / (float)Math.Min(skBitmap.Width, skBitmap.Height);

        // Berechne die neue Breite und Höhe unter Beibehaltung des Seitenverhältnisses
        int targetWidth = (int)(skBitmap.Width * scale);
        int targetHeight = (int)(skBitmap.Height * scale);

        // Erstelle eine neue Bitmap mit den verkleinerten Abmessungen
        var resizedBitmap = new SKBitmap(targetWidth, targetHeight);
        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear);
        skBitmap.ScalePixels(resizedBitmap, samplingOptions);

        // Speichere das verkleinerte Bild als JPEG
        var image = SKImage.FromBitmap(resizedBitmap);
        var data = image.Encode(SKEncodedImageFormat.Jpeg, 90); // 90 = Qualität
        var newStream = File.Create(thumbFilePath);
        data.SaveTo(newStream);
        newStream.Close();
        originalStream.Close();
    }
}
