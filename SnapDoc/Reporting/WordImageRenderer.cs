#nullable enable
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SnapDoc.Reporting;

/// <summary>
/// Erzeugt alle Bild-Elemente: Titelbild, Pin-Icon, Pin-Position auf dem Plan, Fotos.
/// Bilder werden als Inline-Runs genau an der Platzhalterposition eingefügt.
/// </summary>
public sealed class WordImageRenderer(ExportSession session)
{
    private ExportAssetCache Assets => session.Assets;
    private ExportOptions Options => session.Options;

    // ------------------------------------------------------------------ Platzhalter-Resolver

    public PlaceholderResult TitleImage(PlaceholderCall call)
    {
        var project = call.Data.Project;
        if (string.IsNullOrEmpty(project.TitleImageFile))
        {
            session.Warn("Die Vorlage verwendet ein Titelbild, im Projekt ist aber keines hinterlegt.", call.Token.Raw);
            return PlaceholderResult.Empty;
        }

        string path = Assets.ResolveTitleImage(project);
        if (!Assets.Exists(path))
        {
            session.Warn($"Titelbild nicht gefunden: {project.TitleImageFile}", call.Token.Raw);
            return PlaceholderResult.Empty;
        }

        SizeF size = ReportGeometry.ScaleToFit(project.TitleImageSize, GetSize(call, 160, 120));
        return PlaceholderResult.FromElement(InlineImageRun(call, path, size));
    }

    public PlaceholderResult PinIcon(PlaceholderCall call)
    {
        var pin = call.Scope.Pin!;
        if (pin.IsCustomPin)
            return PlaceholderResult.Empty;

        string path = Assets.ResolvePinIcon(pin);
        if (!Assets.Exists(path))
            return PlaceholderResult.Empty;

        return PlaceholderResult.FromElements(InlineImageRun(call, path, GetSize(call, 14, 14)));
    }

    /// <summary>Ausschnitt des Plans um den Pin (oder Kartenbild) mit dem Pin-Icon darüber.</summary>
    public PlaceholderResult PinPositionImage(PlaceholderCall call)
    {
        var pin = call.Scope.Pin!;
        var plan = call.Scope.Plan!;

        if (!Options.ExportPosImages || pin.IsCustomPin)
            return PlaceholderResult.Empty;

        SizeF exportSize = GetSize(call, 25, 25);
        string? backgroundPath;
        A.SourceRectangle? crop = null;

        if (pin.IsWebMapPin)
        {
            backgroundPath = Assets.ResolveMapImage(pin);
        }
        else
        {
            backgroundPath = Assets.ResolvePlanImage(plan);

            float cropSize = Options.PinPosCropSize * 10f;
            double factorX = (1.0 / plan.ImageSize.Width * cropSize) / 2.0;
            double factorY = (1.0 / plan.ImageSize.Height * cropSize) / 2.0;
            crop = new A.SourceRectangle
            {
                Left = (int)((pin.Position.X - factorX) * 100000),
                Top = (int)((pin.Position.Y - factorY) * 100000),
                Right = (int)((1 - pin.Position.X - factorX) * 100000),
                Bottom = (int)((1 - pin.Position.Y - factorY) * 100000),
            };
        }

        if (!Assets.Exists(backgroundPath))
            return PlaceholderResult.Empty;

        SizeF scaledPinSize;
        if (pin.IsLockAutoScale || pin.IsCustomPin)
        {
            float cropSize = Options.PinPosCropSize * 10f;
            scaledPinSize = new SizeF(
                pin.Size.Width * exportSize.Width / cropSize * pin.PinScale,
                pin.Size.Height * exportSize.Height / cropSize * pin.PinScale);
        }
        else
        {
            scaledPinSize = ReportGeometry.ScaleToFit(pin.Size, new SizeF(Options.PinExportSize, Options.PinExportSize));
        }

        PointF posOnPlan = ReportGeometry.PivotRecalc(new PointF(0.5f, 0.5f), pin.Rotation, pin.Anchor, scaledPinSize, exportSize);

        var elements = new List<OpenXmlElement>();

        var background = call.NewRun();
        background.Append(CreateDrawing(call.Paragraph, backgroundPath!, exportSize, new PointF(0, 0), 0, anchored: true, crop, plan.IsGrayscale));
        elements.Add(background);

        string iconPath = Assets.ResolvePinIcon(pin);
        if (Assets.Exists(iconPath))
        {
            var icon = call.NewRun();
            icon.Append(CreateDrawing(call.Paragraph, iconPath, scaledPinSize, posOnPlan, pin.Rotation, anchored: true));
            elements.Add(icon);
        }

        return PlaceholderResult.FromElements(elements);
    }

    /// <summary>Alle exportierbaren Fotos des Pins nebeneinander.</summary>
    public PlaceholderResult PinPhotoList(PlaceholderCall call)
    {
        if (!Options.ExportPhotos)
            return PlaceholderResult.Empty;

        SizeF maxSize = GetSize(call, 40, 40);
        var elements = new List<OpenXmlElement>();

        foreach (var photo in call.Scope.Pin!.Photos)
        {
            var run = PhotoRun(call, photo, maxSize);
            if (run != null)
                elements.Add(run);
        }

        return PlaceholderResult.FromElements(elements);
    }

    /// <summary>Einzelnes Foto (für ${repeat:photos}).</summary>
    public PlaceholderResult Photo(PlaceholderCall call)
    {
        var run = PhotoRun(call, call.Scope.Photo!, GetSize(call, 40, 40));
        return run == null ? PlaceholderResult.Empty : PlaceholderResult.FromElement(run);
    }

    private W.Run? PhotoRun(PlaceholderCall call, ExportFoto photo, SizeF maxSize)
    {
        string path = Assets.ResolvePhoto(photo);
        if (!Assets.Exists(path))
        {
            session.Warn($"Foto nicht gefunden: {photo.File}", call.Token.Raw);
            return null;
        }

        SizeF size = ReportGeometry.ScaleToFit(photo.ImageSize, maxSize);
        var run = call.NewRun();
        run.Append(CreateDrawing(call.Paragraph, path, size, new PointF(0, 0), 0, anchored: false));
        run.Append(new W.Text(" ") { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    // ------------------------------------------------------------------ Helfer für andere Renderer

    public W.Run InlineImageRun(PlaceholderCall call, string path, SizeF size)
    {
        var run = call.NewRun();
        run.Append(CreateDrawing(call.Paragraph, path, size, new PointF(0, 0), 0, anchored: false));
        return run;
    }

    /// <summary>Grösse aus width/height (mm). Fehlen beide, gilt der Standardwert.</summary>
    public static SizeF GetSize(PlaceholderCall call, float defaultWidth, float defaultHeight)
    {
        float w = call.Args.TryGetFloat("width", out var width) && width > 0 ? width : 0;
        float h = call.Args.TryGetFloat("height", out var height) && height > 0 ? height : 0;
        return w == 0 && h == 0 ? new SizeF(defaultWidth, defaultHeight) : new SizeF(w, h);
    }

    // ------------------------------------------------------------------ DrawingML

    /// <param name="host">Element im Zieldokumentteil; bestimmt, in welchem Part das Bild eingebettet wird.</param>
    public W.Drawing CreateDrawing(
        OpenXmlElement host,
        string imagePath,
        SizeF size,
        PointF position,
        float rotationDegrees,
        bool anchored,
        A.SourceRectangle? crop = null,
        bool grayscale = false)
    {
        string relationshipId = session.GetImageRelationshipId(host, imagePath);
        var graphic = CreateGraphic(relationshipId, size, rotationDegrees, crop, grayscale);

        uint id = session.NextDrawingId();
        var docProps = new WP.DocProperties { Id = id, Name = $"Picture {id}" };
        var drawing = new W.Drawing();

        if (!anchored)
        {
            var inline = new WP.Inline
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U,
                Extent = new WP.Extent { Cx = ReportGeometry.MmToEmu(size.Width), Cy = ReportGeometry.MmToEmu(size.Height) },
                EffectExtent = new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                DocProperties = docProps,
                NonVisualGraphicFrameDrawingProperties = new WP.NonVisualGraphicFrameDrawingProperties()
            };
            inline.Append(graphic);
            drawing.Append(inline);
            return drawing;
        }

        var anchor = new WP.Anchor
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U,
            SimplePos = false,
            RelativeHeight = 0U,
            BehindDoc = true,
            Locked = false,
            LayoutInCell = true,
            AllowOverlap = true
        };

        var horizontal = new WP.HorizontalPosition { RelativeFrom = WP.HorizontalRelativePositionValues.Column };
        horizontal.Append(new WP.PositionOffset { Text = ReportGeometry.MmToEmu(position.X).ToString() });
        var vertical = new WP.VerticalPosition { RelativeFrom = WP.VerticalRelativePositionValues.Paragraph };
        vertical.Append(new WP.PositionOffset { Text = ReportGeometry.MmToEmu(position.Y).ToString() });

        var wrapPolygon = new WP.WrapPolygon { Edited = false };
        wrapPolygon.Append(new WP.StartPoint { X = 0L, Y = 0L });
        for (int i = 0; i < 4; i++)
            wrapPolygon.Append(new WP.LineTo { X = 0L, Y = 0L });
        var wrapSquare = new WP.WrapSquare { WrapText = WP.WrapTextValues.BothSides };
        wrapSquare.Append(wrapPolygon);

        anchor.Append(new WP.SimplePosition { X = ReportGeometry.MmToEmu(position.X), Y = ReportGeometry.MmToEmu(position.Y) });
        anchor.Append(horizontal);
        anchor.Append(vertical);
        anchor.Append(new WP.Extent { Cx = ReportGeometry.MmToEmu(size.Width), Cy = ReportGeometry.MmToEmu(size.Height) });
        anchor.Append(new WP.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L });
        anchor.Append(wrapSquare);
        anchor.Append(docProps);
        anchor.Append(graphic);
        drawing.Append(anchor);
        return drawing;
    }

    private static A.Graphic CreateGraphic(string relationshipId, SizeF size, float rotationDegrees, A.SourceRectangle? crop, bool grayscale)
    {
        var blip = new A.Blip { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Print };
        if (grayscale)
            blip.Append(new A.Grayscale());

        var stretch = new A.Stretch();
        stretch.Append(new A.FillRectangle());

        var blipFill = new PIC.BlipFill();
        blipFill.Append(blip);
        blipFill.Append(crop != null ? (A.SourceRectangle)crop.CloneNode(true) : new A.SourceRectangle());
        blipFill.Append(stretch);

        var transform = new A.Transform2D { Rotation = (int)(rotationDegrees * 60000) };
        transform.Append(new A.Offset { X = 0L, Y = 0L });
        transform.Append(new A.Extents { Cx = ReportGeometry.MmToEmu(size.Width), Cy = ReportGeometry.MmToEmu(size.Height) });

        var geometry = new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle };
        geometry.Append(new A.AdjustValueList());

        var shapeProperties = new PIC.ShapeProperties();
        shapeProperties.Append(transform);
        shapeProperties.Append(geometry);

        var nvProps = new PIC.NonVisualPictureProperties();
        nvProps.Append(new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Picture" });
        nvProps.Append(new PIC.NonVisualPictureDrawingProperties());

        var picture = new PIC.Picture();
        picture.Append(nvProps);
        picture.Append(blipFill);
        picture.Append(shapeProperties);

        var graphicData = new A.GraphicData { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        graphicData.Append(picture);

        var graphic = new A.Graphic();
        graphic.Append(graphicData);
        return graphic;
    }
}
