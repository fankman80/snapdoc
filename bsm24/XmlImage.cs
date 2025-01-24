#nullable disable

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
    public static D.Drawing GenerateImage(MainDocumentPart mainPart,
                                            FileResult imagePath,
                                            double scaleFactor,
                                            SKPoint? crop_center = null,
                                            SKSize? crop_size = null,
                                            double widthMilimeters = 0,
                                            double heightMilimeters = 0,
                                            int imageQuality = 90,
                                            List<(string, SKPoint, SKPoint, SKColor)> overlayImages = null)
    // Item1 = Image
    // Item2 = Position
    // Item3 = Text
    // Item4 = Anchor
    {
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
            foreach ((string, SKPoint, SKPoint, SKColor) overlayImage in overlayImages.Select(v => v))
            {
                var cacheDir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "imagecache", overlayImage.Item1);
                var stream = File.OpenRead(cacheDir);
                var skStream = new SKManagedStream(stream);
                var overlay = SKBitmap.Decode(skStream);
                SKBitmap combinedBitmap = new(skBitmap.Width, skBitmap.Height);
                using (SKCanvas canvas = new(combinedBitmap))
                {
                    var _pos = new SKPoint(
                        (skBitmap.Width * overlayImage.Item2.X) - (overlay.Width * overlayImage.Item3.X),
                        (skBitmap.Height * overlayImage.Item2.Y) - (overlay.Height * overlayImage.Item3.Y));

                    canvas.DrawBitmap(skBitmap, new SKPoint(0, 0));
                    canvas.DrawBitmap(overlay, _pos);
                }
                skBitmap = combinedBitmap;
                stream.Dispose();
                skStream.Dispose();
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
            widthMilimeters = heightMilimeters * ((double)skBitmap.Width / skBitmap.Height);
        if (heightMilimeters == 0)
            heightMilimeters = widthMilimeters * ((double)skBitmap.Height / skBitmap.Width);
        if (heightMilimeters == 0 & widthMilimeters == 0)
        {
            widthMilimeters = 60;  // wenn beide Längen Null sind, nehme Standardwert
            heightMilimeters = widthMilimeters * ((double)skBitmap.Height / skBitmap.Width);
        }

        var image = SKImage.FromBitmap(resizedBitmap);
        var data = image.Encode(SKEncodedImageFormat.Jpeg, imageQuality); // imageQuality zwischen 0 und 100
        ImagePart planPart = mainPart.AddImagePart(ImagePartType.Jpeg);
        using (Stream imagePartStream = planPart.GetStream())
        {
            data.SaveTo(imagePartStream);
        }
        string relationshipId = mainPart.GetIdOfPart(planPart);

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
}
