#nullable disable
using SkiaSharp;
using SnapDoc.Services;

namespace SnapDoc;

public class Thumbnail
{
    /// <summary>
    /// Generiert ein skaliertes und korrekt rotiertes Thumbnail basierend auf einem Stream.
    /// </summary>
    public static async Task Generate(Stream sourceStream, string thumbFilePath)
    {
        if (sourceStream == null) return;

        try
        {
            string thumbDir = Path.GetDirectoryName(thumbFilePath);
            if (!Directory.Exists(thumbDir)) Directory.CreateDirectory(thumbDir);

            sourceStream.Position = 0;
            using var managedThumbStream = new SKManagedStream(sourceStream, false);
            using var codec = SKCodec.Create(managedThumbStream);

            if (codec != null)
            {
                // 1. Zielmasse ZUERST berechnen
                int maxThumbSize = SettingsService.Instance.FotoThumbSize;
                int origWidth = codec.Info.Width;
                int origHeight = codec.Info.Height;
                int thumbWidth, thumbHeight;

                if (origWidth > origHeight)
                {
                    thumbWidth = maxThumbSize;
                    thumbHeight = (int)((float)origHeight / origWidth * maxThumbSize);
                }
                else
                {
                    thumbHeight = maxThumbSize;
                    thumbWidth = (int)((float)origWidth / origHeight * maxThumbSize);
                }

                // 2. Nativ unterstützte Untergrösse vom Codec abfragen (Subsampling)
                float scaleFactor = (float)thumbWidth / origWidth;
                SKSizeI supportedScale = codec.GetScaledDimensions(scaleFactor);

                var decodeInfo = new SKImageInfo(supportedScale.Width, supportedScale.Height);

                // Dekodiert das Bild direkt extrem speicherschonend in der kleinen Zwischengrösse
                using var originalMinBitmap = SKBitmap.Decode(codec, decodeInfo);

                if (originalMinBitmap != null)
                {
                    var samplingOptions = new SKSamplingOptions(SKCubicResampler.CatmullRom);

                    SKBitmap baseBitmap = originalMinBitmap;
                    SKBitmap resizedBitmap = null;

                    // 3. Falls die Codec-Grösse nicht exakt mit den Zielmassen übereinstimmt, fein-skalieren
                    if (originalMinBitmap.Width != thumbWidth || originalMinBitmap.Height != thumbHeight)
                    {
                        resizedBitmap = originalMinBitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), samplingOptions);
                        baseBitmap = resizedBitmap;
                    }

                    if (baseBitmap != null)
                    {
                        var orientation = codec.EncodedOrigin;
                        SKBitmap finalMiniBitmap = baseBitmap;

                        if (orientation != SKEncodedOrigin.TopLeft)
                            finalMiniBitmap = RotateBitmap(baseBitmap, orientation);

                        using (var thumbImage = SKImage.FromBitmap(finalMiniBitmap))
                        using (var thumbData = thumbImage.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoThumbQuality))
                        {
                            using var fs = File.Create(thumbFilePath);
                            thumbData.SaveTo(fs);
                        }

                        // Sauberes Ressourcen-Management ohne Double-Dispose-Gefahr
                        if (finalMiniBitmap != baseBitmap) finalMiniBitmap.Dispose();
                        resizedBitmap?.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thumbnail generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Ueberladung fuer bestehende Codestellen, die einen Dateipfad uebergeben.
    /// </summary>
    public static async Task Generate(string originalFilePath, string thumbFilePath)
    {
        if (!File.Exists(originalFilePath)) return;

        try
        {
            using var fileStream = File.OpenRead(originalFilePath);
            await Generate(fileStream, thumbFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thumbnail from file path failed: {ex.Message}");
        }
    }

    private static SKBitmap RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
    {
        SKBitmap rotated;
        switch (orientation)
        {
            case SKEncodedOrigin.BottomRight: // 180°
                rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            case SKEncodedOrigin.RightTop: // 90° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(bitmap.Height, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            case SKEncodedOrigin.LeftBottom: // 270° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(0, bitmap.Width);
                    canvas.RotateDegrees(270);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            default:
                return bitmap;
        }
        return rotated;
    }
}