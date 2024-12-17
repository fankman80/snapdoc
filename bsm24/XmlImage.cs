#nullable disable

using bsm24.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using A = DocumentFormat.OpenXml.Drawing;
using D = DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using Size = Microsoft.Maui.Graphics.Size;

namespace bsm24;

public partial class XmlImage
{
    public static async Task<D.Drawing> GenerateImage(MainDocumentPart mainPart,
                                                FileResult imagePath,
                                                double scaleFactor,
                                                SKPoint? crop_center = null,
                                                SKSize? crop_size = null,
                                                double widthMilimeters = 0,
                                                double heightMilimeters = 0,
                                                int imageQuality = 90,
                                                List<(string, SKPoint, string, SKPoint, SKColor)> overlayImages = null)
    // Item1 = Image
    // Item2 = Position
    // Item3 = Text
    // Item4 = Anchor
    {
        Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "imagecache"));
        var originalStream = File.OpenRead(imagePath.FullPath);
        var skBitmap = SKBitmap.Decode(originalStream);
        string newImagePath = Path.Combine(FileSystem.AppDataDirectory, "imagecache", imagePath.FileName);

        // Schneide das Bild zu
        if (crop_center != null)
        {
            SKPoint _crop_center = (SKPoint)crop_center;
            SKSize _crop_size = (SKSize)crop_size;

            SKRectI cropRect = new((int)(skBitmap.Width * _crop_center.X) - (int)(_crop_size.Width / 2),
                                   (int)(skBitmap.Height * _crop_center.Y) - (int)(_crop_size.Height / 2),
                                   (int)(skBitmap.Width * _crop_center.X) + (int)(_crop_size.Width / 2),
                                   (int)(skBitmap.Height * _crop_center.Y) + (int)(_crop_size.Height / 2));
            SKBitmap croppedBitmap = new(cropRect.Width, cropRect.Height);
            skBitmap.ExtractSubset(croppedBitmap, cropRect);
            skBitmap = croppedBitmap;
        }

        // Add Overlay-Image
        if (overlayImages != null)
        {
            // Font definition
            var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
            var font = new SKFont { Size = SettingsService.Instance.PlanLabelSize, Edging = SKFontEdging.Antialias, Typeface = typeface };

            foreach ((string, SKPoint, string, SKPoint, SKColor) overlayImage in overlayImages.Select(v => v))
            {
                var stream = await LoadImageStreamAsync(overlayImage.Item1);
                var skStream = new SKManagedStream(stream);
                var overlay = SKBitmap.Decode(skStream);
#if ANDROID
                var context = Android.App.Application.Context;
                var resources = context.Resources;
                var resourceId = resources.GetIdentifier(Path.GetFileNameWithoutExtension(overlayImage.Item1), "drawable", context.PackageName);
                if (resourceId != 0)  // Convert Android.Graphics.Bitmap to SKBitmap if resource is not Null
                {
                    var drawable = MainApplication.Current.GetDrawable(resourceId);
                    Android.Graphics.Drawables.BitmapDrawable bitmapDrawable = (Android.Graphics.Drawables.BitmapDrawable)drawable;
                    Android.Graphics.Bitmap androidBitmap = bitmapDrawable.Bitmap;
                    overlay = new SKBitmap(androidBitmap.Width, androidBitmap.Height);
                    int[] pixels = new int[androidBitmap.Width * androidBitmap.Height];
                    androidBitmap.GetPixels(pixels, 0, androidBitmap.Width, 0, 0, androidBitmap.Width, androidBitmap.Height);
                    overlay.Pixels = pixels.Select(p => new SKColor((uint)p)).ToArray();
                }
#endif
                SKBitmap combinedBitmap = new(skBitmap.Width, skBitmap.Height);
                using (SKCanvas canvas = new(combinedBitmap))
                {
                    var _pos = new SKPoint(
                        (skBitmap.Width * overlayImage.Item2.X) - (overlay.Width * overlayImage.Item4.X),
                        (skBitmap.Height * overlayImage.Item2.Y) - (overlay.Height * overlayImage.Item4.Y));

                    canvas.DrawBitmap(skBitmap, new SKPoint(0, 0));
                    canvas.DrawBitmap(overlay, _pos);

                    var textPos = new SKPoint(_pos.X + overlay.Width + Settings.PinTextPadding + Settings.PinTextDistance,
					      _pos.Y - Settings.PinTextPadding + Settings.PinTextDistance); // Position des Textes
                    var textWidth = font.MeasureText(overlayImage.Item3); // Breite des Textes
                    var fontMetrics = font.Metrics;
                    var textHeight = fontMetrics.Descent - fontMetrics.Ascent; // Höhe des Textes

                    if (overlayImage.Item3 != "")
                    {
                        using (var backgroundPaint = new SKPaint { Color = SKColors.White }) // weisser Hintergrund
                        {
                            var backgroundRect = new SKRect(
                                textPos.X - Settings.PinTextPadding + 1,
                                textPos.Y + fontMetrics.Ascent - Settings.PinTextPadding + 1,
                                textPos.X + textWidth + Settings.PinTextPadding - 1,
                                textPos.Y + fontMetrics.Descent + Settings.PinTextPadding - 1);
                            canvas.DrawRect(backgroundRect, backgroundPaint);
                        }

                        using (var backgroundPaint = new SKPaint { Color = overlayImage.Item5, Style = SKPaintStyle.Stroke, StrokeWidth = 2 }) // Rahmen
                        {
                            var backgroundRect = new SKRect(
                                textPos.X - Settings.PinTextPadding,
                                textPos.Y + fontMetrics.Ascent - Settings.PinTextPadding,
                                textPos.X + textWidth + Settings.PinTextPadding,
                                textPos.Y + fontMetrics.Descent + Settings.PinTextPadding);
                            canvas.DrawRect(backgroundRect, backgroundPaint);
                        }

                        using (var backgroundPaint = new SKPaint { Color = overlayImage.Item5 }) // Text
                        {
                            canvas.DrawText(overlayImage.Item3, textPos, SKTextAlign.Left, font, backgroundPaint);
                        }
                    }
                }

                skBitmap = combinedBitmap;
            }
        }

        // Berechne die neue Breite und Höhe unter Beibehaltung des Seitenverhältnisses
        int targetWidth = (int)(skBitmap.Width * scaleFactor);
        int targetHeight = (int)(skBitmap.Height * scaleFactor);

        // Erstelle eine neue Bitmap mit den verkleinerten Abmessungen
        var resizedBitmap = new SKBitmap(targetWidth, targetHeight);
        skBitmap.ScalePixels(resizedBitmap, SKSamplingOptions.Default);

        // Berechne die neue Breite und Höhe in Milimeter
        if (widthMilimeters == 0)
        {
            widthMilimeters = heightMilimeters * ((double)skBitmap.Width / skBitmap.Height);
        }
        if (heightMilimeters == 0)
        {
            heightMilimeters = widthMilimeters * ((double)skBitmap.Height / skBitmap.Width);
        }
        if (heightMilimeters == 0 & widthMilimeters == 0)
        {
            widthMilimeters = 60;  // wenn beide Längen Null sind, nehme Standardwert
            heightMilimeters = widthMilimeters * ((double)skBitmap.Height / skBitmap.Width);
        }

        // Speichere das verkleinerte Bild als JPEG
        var image = SKImage.FromBitmap(resizedBitmap);
        var data = image.Encode(SKEncodedImageFormat.Jpeg, imageQuality);
        var newStream = File.Create(newImagePath);
        data.SaveTo(newStream);
        newStream.Close();
        originalStream.Close();

        // Füge das Bild als ImagePart hinzu
        ImagePart planPart = mainPart.AddImagePart(ImagePartType.Jpeg);

        // Lade das Bild vom Dateisystem und schreibe es in den ImagePart
        using (FileStream stream = new(newImagePath, FileMode.Open))
        {
            planPart.FeedData(stream);
        }

        // Hole die Relationship ID des eingebetteten Bildes
        string relationshipId = mainPart.GetIdOfPart(planPart);

        // lösche den Bild-Cache
        Directory.Delete(Path.Combine(FileSystem.AppDataDirectory, "imagecache"), true);

        return AddImage(relationshipId, newImagePath, new Size(widthMilimeters, heightMilimeters));
    }

    private static D.Drawing AddImage(string relationshipId, string img_name, Size size)
    {
        long widthInEmus = (long)(size.Width / 25.4 * 914400);  // Breite des Bildes in Millimetern
        long heightInEmus = (long)(size.Height / 25.4 * 914400);  // Höhe des Bildes in Millimetern

        var element =
          new D.Drawing(
            new DW.Inline(
              new DW.Extent() { Cx = widthInEmus, Cy = heightInEmus },
              new DW.EffectExtent()
              {
                  LeftEdge = 0L,
                  TopEdge = 0L,
                  RightEdge = 0L,
                  BottomEdge = 0L
              },
              new DW.DocProperties()
              {
                  Id = (UInt32Value)1U,
                  Name = img_name
              },
              new DW.NonVisualGraphicFrameDrawingProperties(
                  new A.GraphicFrameLocks() { NoChangeAspect = true }),
              new A.Graphic(
        new A.GraphicData(
                  new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                      new PIC.NonVisualDrawingProperties()
                      {
                          Id = (UInt32Value)0U,
                          Name = img_name
                      },
                      new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                      new A.Blip(
                        new A.BlipExtensionList(
                          new A.BlipExtension()
                          {
                              Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}"
                          })
                       )
                      {
                          Embed = relationshipId,
                          CompressionState = A.BlipCompressionValues.Print
                      },
                      new A.Stretch(
                        new A.FillRectangle())),
                      new PIC.ShapeProperties(
                        new A.Transform2D(
                          new A.Offset() { X = 0L, Y = 0L },
                          new A.Extents() { Cx = widthInEmus, Cy = heightInEmus }),
                        new A.PresetGeometry(
                          new A.AdjustValueList()
                        )
                        { Preset = A.ShapeTypeValues.Rectangle }))
                )
        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
            )
            {
                DistanceFromTop = (UInt32Value)0U,
                DistanceFromBottom = (UInt32Value)0U,
                DistanceFromLeft = (UInt32Value)0U,
                DistanceFromRight = (UInt32Value)0U
            });

        return element;
    }

    public static async Task<Stream> LoadImageStreamAsync(string file)
    {
        if (Path.IsPathRooted(file) && File.Exists(file))
            return File.OpenRead(file);
#if ANDROID
        var context = Android.App.Application.Context;
        var resources = context.Resources;

        var resourceId = resources.GetIdentifier(Path.GetFileNameWithoutExtension(file), "drawable", context.PackageName);
        if (resourceId > 0)
        {
            var imageUri = new Android.Net.Uri.Builder()
                .Scheme(Android.Content.ContentResolver.SchemeAndroidResource)
                .Authority(resources.GetResourcePackageName(resourceId))
                .AppendPath(resources.GetResourceTypeName(resourceId))
                .AppendPath(resources.GetResourceEntryName(resourceId))
                .Build();

            var stream = context.ContentResolver.OpenInputStream(imageUri);
            if (stream is not null)
                return stream;
        }
        await Task.CompletedTask;
#elif WINDOWS
        try
        {
            var sf = await Windows.Storage.StorageFile.GetFileFromPathAsync(file);
            if (sf is not null)
            {
                var stream = await sf.OpenStreamForReadAsync();
                if (stream is not null)
                    return stream;
            }
        }
        catch
        {
        }

        if (AppInfo.PackagingModel == AppPackagingModel.Packaged)
        {
            var uri = new Uri("ms-appx:///" + file);
            var sf = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
            var stream = await sf.OpenStreamForReadAsync();
            if (stream is not null)
                return stream;
        }
        else
        {
            var root = AppContext.BaseDirectory;
            file = Path.Combine(root, file);
            if (File.Exists(file))
                return File.OpenRead(file);
        }
#elif IOS || MACCATALYST
		var root = Foundation.NSBundle.MainBundle.BundlePath;
#if MACCATALYST || MACOS
		root = Path.Combine(root, "Contents", "Resources");
#endif
		file = Path.Combine(root, file);
		if (File.Exists(file))
			return File.OpenRead(file);
        await Task.CompletedTask;
#endif
        return null;
    }
}
