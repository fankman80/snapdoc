using SkiaSharp;

namespace SnapDoc.DrawingTool;

public static class DrawingMapper
{
    public static DrawingFileDto ToDto(CombinedDrawable d, float initialRotation)
    {
        var style = new DrawingStyleDto
        {
            LineColor = d.PolyDrawable.LineColor.ToString(),
            FillColor = d.PolyDrawable.FillColor.ToString(),
            LineThickness = d.PolyDrawable.LineThickness,
            StrokeStyle = d.PolyDrawable.StrokeStyle,
        };

        var bounds = CalculateBounds(d);

        return new DrawingFileDto
        {
            Style = style,

            Bounds = new BoundsDto
            {
                Width = bounds.Width,
                Height = bounds.Height
            },

            InitialRotation = initialRotation,

            // ---------------- POLY ----------------
            Poly = d.PolyDrawable?.Points.Count > 0
                ? new PolyDto
                {
                    IsClosed = d.PolyDrawable.IsClosed,
                    IsCloud = d.PolyDrawable.IsCloud,
                    Points = [.. d.PolyDrawable.Points
                    .Select(p => new PointDto(
                        p.X - bounds.Left,
                        p.Y - bounds.Top))]
                }
                : null,

            // ---------------- FREE ----------------
            Free = d.FreeDrawable?.Points.Count > 0
                ? new FreeDto
                {
                    Strokes = [.. d.FreeDrawable.Points
                    .Select(stroke =>
                        stroke.Select(p =>
                            new PointDto(
                                p.X - bounds.Left,
                                p.Y - bounds.Top)).ToList())]
                }
                : null,

            // ---------------- RECT ----------------
            Rect = d.RectDrawable?.IsDrawn == true
                ? new RectDto
                {
                    RotationDeg = NormalizeAngleDeg(d.RectDrawable.AllowedAngleDeg - initialRotation),
                    Text = d.RectDrawable.Text,
                    IsCloud = d.RectDrawable.IsCloud,
                    Points = [.. d.RectDrawable.Points
                        .Select(p => new PointDto(
                            p.X - bounds.Left,
                            p.Y - bounds.Top))],
                    TextStyle = new TextStyleDto
                    {
                        TextColor = d.RectDrawable.TextColor.ToString(),
                        TextSize = d.RectDrawable.TextSize,
                        TextAlignment = (int)d.RectDrawable.TextAlignment,
                        TextStyle = (int)d.RectDrawable.TextStyle,
                        AutoSizeText = d.RectDrawable.AutoSizeText,
                        TextPadding = d.RectDrawable.TextPadding
                    }
                }
                : null,

            // ---------------- OVAL ----------------
            Oval = d.OvalDrawable?.IsDrawn == true
                ? new OvalDto
                {
                    RotationDeg = NormalizeAngleDeg(d.OvalDrawable.AllowedAngleDeg - initialRotation),
                    Text = d.OvalDrawable.Text,
                    IsCloud = d.OvalDrawable.IsCloud,
                    Points = [.. d.OvalDrawable.Points
                        .Select(p => new PointDto(
                            p.X - bounds.Left,
                            p.Y - bounds.Top))],
                    TextStyle = new TextStyleDto
                    {
                        TextColor = d.OvalDrawable.TextColor.ToString(),
                        TextSize = d.OvalDrawable.TextSize,
                        TextAlignment = (int)d.OvalDrawable.TextAlignment,
                        TextStyle = (int)d.OvalDrawable.TextStyle,
                        AutoSizeText = d.OvalDrawable.AutoSizeText,
                        TextPadding = d.OvalDrawable.TextPadding
                    }
                }
                : null,

            // ---------------- ARROW ----------------
            Arrow = d.ArrowDrawable?.IsDrawn == true
                ? new ArrowDto
                {
                    RotationDeg = NormalizeAngleDeg(d.ArrowDrawable.AllowedAngleDeg - initialRotation),
                    Points = [.. d.ArrowDrawable.Points
                        .Select(p => new PointDto(
                            p.X - bounds.Left,
                            p.Y - bounds.Top))]
                }
                : null
        };
    }

    public static void FromDto(DrawingFileDto dto, CombinedDrawable d, SKPoint targetCenter, DrawingController controller)
    {
        d.Reset();

        controller.InitialRotation = dto.InitialRotation;

        ApplyStyle(dto.Style, d);

        var offset = new SKPoint(
            targetCenter.X - dto.Bounds!.Width / 2,
            targetCenter.Y - dto.Bounds.Height / 2
        );

        // ---------------- POLY ----------------
        if (dto.Poly != null)
        {
            d.PolyDrawable.Reset();
            d.PolyDrawable.IsClosed = dto.Poly.IsClosed;
            d.PolyDrawable.IsCloud = dto.Poly.IsCloud;
            d.PolyDrawable.Points.AddRange(
                dto.Poly.Points.Select(p =>
                    new SKPoint(p.X + offset.X, p.Y + offset.Y))
            );
        }

        // ---------------- FREE ----------------
        if (dto.Free != null)
        {
            d.FreeDrawable.Points.Clear();
            foreach (var stroke in dto.Free.Strokes)
            {
                d.FreeDrawable.StartStroke();
                foreach (var p in stroke)
                    d.FreeDrawable.AddPoint(
                        new SKPoint(p.X + offset.X, p.Y + offset.Y));
                d.FreeDrawable.EndStroke();
            }
        }

        // ---------------- RECT ----------------
        if (dto.Rect != null)
        {
            var r = d.RectDrawable;
            r.Reset();

            r.Text = dto.Rect.Text ?? "";
            r.AllowedAngleDeg = dto.InitialRotation + dto.Rect.RotationDeg;
            r.IsCloud = dto.Rect.IsCloud;

            if (dto.Rect.TextStyle != null)
            {
                r.TextColor = SKColor.Parse(dto.Rect.TextStyle.TextColor);
                r.TextSize = dto.Rect.TextStyle.TextSize;
                r.TextAlignment = (RectangleTextAlignment)dto.Rect.TextStyle.TextAlignment;
                r.TextStyle = (RectangleTextStyle)dto.Rect.TextStyle.TextStyle;
                r.AutoSizeText = dto.Rect.TextStyle.AutoSizeText;
                r.TextPadding = dto.Rect.TextStyle.TextPadding;
            }

            var p0 = new SKPoint(
                dto.Rect.Points[0].X + offset.X,
                dto.Rect.Points[0].Y + offset.Y);

            var p2 = new SKPoint(
                dto.Rect.Points[2].X + offset.X,
                dto.Rect.Points[2].Y + offset.Y);

            r.SetFromDrag(p0, p2);
            r.IsDrawn = true;
        }

        // ---------------- OVAL ----------------
        if (dto.Oval != null)
        {
            var o = d.OvalDrawable;
            o.Reset();

            o.Text = dto.Oval.Text ?? "";
            o.AllowedAngleDeg = dto.InitialRotation + dto.Oval.RotationDeg;
            o.IsCloud = dto.Oval.IsCloud;

            if (dto.Oval.TextStyle != null)
            {
                o.TextColor = SKColor.Parse(dto.Oval.TextStyle.TextColor);
                o.TextSize = dto.Oval.TextStyle.TextSize;
                o.TextAlignment = (RectangleTextAlignment)dto.Oval.TextStyle.TextAlignment;
                o.TextStyle = (RectangleTextStyle)dto.Oval.TextStyle.TextStyle;
                o.AutoSizeText = dto.Oval.TextStyle.AutoSizeText;
                o.TextPadding = dto.Oval.TextStyle.TextPadding;
            }

            var p0 = new SKPoint(
                dto.Oval.Points[0].X + offset.X,
                dto.Oval.Points[0].Y + offset.Y);

            var p1 = new SKPoint(
                dto.Oval.Points[1].X + offset.X,
                dto.Oval.Points[1].Y + offset.Y);

            var p2 = new SKPoint(
                dto.Oval.Points[2].X + offset.X,
                dto.Oval.Points[2].Y + offset.Y);

            var p3 = new SKPoint(
                dto.Oval.Points[3].X + offset.X,
                dto.Oval.Points[3].Y + offset.Y);

            var center = new SKPoint((p0.X + p2.X) / 2f, (p0.Y + p2.Y) / 2f);
            var localXVec = new SKPoint(p1.X - center.X, p1.Y - center.Y);
            var localYVec = new SKPoint(p2.X - center.X, p2.Y - center.Y);
            var topLeft = new SKPoint(center.X - localXVec.X - localYVec.X, center.Y - localXVec.Y - localYVec.Y);
            var bottomRight = new SKPoint(center.X + localXVec.X + localYVec.X, center.Y + localXVec.Y + localYVec.Y);

            o.SetFromDrag(topLeft, bottomRight);
            o.IsDrawn = true;
        }

        // ---------------- ARROW ----------------
        if (dto.Arrow != null)
        {
            var r = d.ArrowDrawable;
            r.Reset();

            r.AllowedAngleDeg = dto.InitialRotation + dto.Arrow.RotationDeg;

            var p0 = new SKPoint(
                dto.Arrow.Points[0].X + offset.X,
                dto.Arrow.Points[0].Y + offset.Y);

            var p2 = new SKPoint(
                dto.Arrow.Points[2].X + offset.X,
                dto.Arrow.Points[2].Y + offset.Y);

            r.SetFromDrag(p0, p2);
            r.IsDrawn = true;
        }
    }

    static SKRect CalculateBounds(CombinedDrawable d)
    {
        var points = new List<SKPoint>();

        if (d.PolyDrawable.HasContent)
            points.AddRange(d.PolyDrawable.Points);

        if (d.FreeDrawable.HasContent)
            points.AddRange(d.FreeDrawable.Points.SelectMany(s => s));

        if (d.RectDrawable.HasContent)
            points.AddRange(d.RectDrawable.Points);

        if (d.OvalDrawable.HasContent)
            points.AddRange(d.OvalDrawable.Points);

        if (d.ArrowDrawable.HasContent)
            points.AddRange(d.ArrowDrawable.Points);

        if (points.Count == 0)
            return SKRect.Empty;

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);

        // Erzeugt ein Rechteck von minX/minY bis maxX/maxY
        return SKRect.Create(minX, minY, maxX - minX, maxY - minY);
    }

    private static void ApplyStyle(DrawingStyleDto? s, CombinedDrawable d)
    {
        if (s == null)
            return;

        var lineColor = SKColor.Parse(s.LineColor);
        var fillColor = SKColor.Parse(s.FillColor);

        // ---------------- FREE ----------------
        d.FreeDrawable.LineColor = lineColor;
        d.FreeDrawable.LineThickness = s.LineThickness;

        // ---------------- POLY ----------------
        d.PolyDrawable.LineColor = lineColor;
        d.PolyDrawable.FillColor = fillColor;
        d.PolyDrawable.LineThickness = s.LineThickness;
        d.PolyDrawable.StrokeStyle = s.StrokeStyle;

        // ---------------- RECT ----------------
        d.RectDrawable.LineColor = lineColor;
        d.RectDrawable.FillColor = fillColor;
        d.RectDrawable.LineThickness = s.LineThickness;
        d.RectDrawable.StrokeStyle = s.StrokeStyle;

        // ---------------- OVAL ----------------
        d.OvalDrawable.LineColor = lineColor;
        d.OvalDrawable.FillColor = fillColor;
        d.OvalDrawable.LineThickness = s.LineThickness;
        d.OvalDrawable.StrokeStyle = s.StrokeStyle;

        // ---------------- ARROW ----------------
        d.ArrowDrawable.LineColor = lineColor;
        d.ArrowDrawable.FillColor = fillColor;
        d.ArrowDrawable.LineThickness = s.LineThickness;
        d.ArrowDrawable.StrokeStyle = s.StrokeStyle;
    }

    private static float NormalizeAngleDeg(float deg)
    {
        deg %= 360f;

        if (deg > 180f)
            deg -= 360f;

        if (deg < -180f)
            deg += 360f;

        if (Math.Abs(deg) < 0.0001f)
            return 0f;

        return deg;
    }
}