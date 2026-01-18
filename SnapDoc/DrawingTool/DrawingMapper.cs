using SkiaSharp;

namespace SnapDoc.DrawingTool;

public static class DrawingMapper
{
    public static DrawingFileDto ToDto(CombinedDrawable d)
    {
        var style = new DrawingStyleDto
        {
            LineColor = d.PolyDrawable.LineColor.ToString(),
            FillColor = d.PolyDrawable.FillColor.ToString(),
            LineThickness = d.PolyDrawable.LineThickness,

            TextColor = d.RectDrawable.TextColor.ToString(),
            TextSize = d.RectDrawable.TextSize,
            TextAlignment = (int)d.RectDrawable.TextAlignment,
            TextStyle = (int)d.RectDrawable.TextStyle,
            AutoSizeText = d.RectDrawable.AutoSizeText
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

            // ---------------- POLY ----------------
            Poly = d.PolyDrawable?.Points.Count > 0
                ? new PolyDto
                {
                    IsClosed = d.PolyDrawable.IsClosed,
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
                    RotationDeg = d.RectDrawable.AllowedAngleDeg,
                    Text = d.RectDrawable.Text,
                    Points = [.. d.RectDrawable.Points
                    .Select(p => new PointDto(
                        p.X - bounds.Left,
                        p.Y - bounds.Top))]
                }
                : null
        };
    }

    public static void FromDto(DrawingFileDto dto, CombinedDrawable d, SKPoint targetCenter)
    {
        d.Reset();

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
        if (dto.Rect != null && dto.Rect.Points.Count == 4)
        {
            var r = d.RectDrawable;
            r.Reset();

            r.Text = dto.Rect.Text ?? "";
            r.AllowedAngleDeg = dto.Rect.RotationDeg;

            var p0 = new SKPoint(
                dto.Rect.Points[0].X + offset.X,
                dto.Rect.Points[0].Y + offset.Y);

            var p2 = new SKPoint(
                dto.Rect.Points[2].X + offset.X,
                dto.Rect.Points[2].Y + offset.Y);

            r.SetFromDrag(p0, p2);
            r.IsDrawn = true;
        }
    }

    static SKRect CalculateBounds(CombinedDrawable d)
    {
        var points = new List<SKPoint>();

        points.AddRange(d.PolyDrawable.Points);
        points.AddRange(d.FreeDrawable.Points.SelectMany(s => s));
        points.AddRange(d.RectDrawable.Points);

        if (points.Count == 0)
            return SKRect.Empty;

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);

        return SKRect.Create(minX, minY, maxX - minX, maxY - minY);
    }

    private static void ApplyStyle(DrawingStyleDto s, CombinedDrawable d)
    {
        var lineColor = SKColor.Parse(s.LineColor);
        var fillColor = SKColor.Parse(s.FillColor);
        var textColor = SKColor.Parse(s.TextColor);

        d.FreeDrawable.LineColor = lineColor;
        d.FreeDrawable.LineThickness = s.LineThickness;

        d.PolyDrawable.LineColor = lineColor;
        d.PolyDrawable.FillColor = fillColor;
        d.PolyDrawable.LineThickness = s.LineThickness;

        d.RectDrawable.LineColor = lineColor;
        d.RectDrawable.FillColor = fillColor;
        d.RectDrawable.LineThickness = s.LineThickness;
        d.RectDrawable.TextColor = textColor;
        d.RectDrawable.TextSize = s.TextSize;
        d.RectDrawable.TextAlignment = (RectangleTextAlignment)s.TextAlignment;
        d.RectDrawable.TextStyle = (RectangleTextStyle)s.TextStyle;
        d.RectDrawable.AutoSizeText = s.AutoSizeText;
    }
}