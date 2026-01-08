using SkiaSharp;

namespace SnapDoc;

public class InteractiveRectangleDrawable
{
    // Eckpunkte (immer 4)
    public List<SKPoint> Points { get; private set; } = [];

    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;

    public bool DisplayHandles { get; set; } = true;

    public SKColor FillColor { get; set; } = SKColors.Blue.WithAlpha(100);
    public SKColor LineColor { get; set; } = SKColors.Blue;
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(160);

    public bool HasContent => Points.Count == 4;

    /// <summary>
    /// Erzeugt ein Rechteck aus zwei Punkten (Drag Start → End)
    /// </summary>
    public void SetFromDrag(SKPoint start, SKPoint end)
    {
        Points.Clear();

        var left   = Math.Min(start.X, end.X);
        var right  = Math.Max(start.X, end.X);
        var top    = Math.Min(start.Y, end.Y);
        var bottom = Math.Max(start.Y, end.Y);

        Points.Add(new SKPoint(left,  top));    // 0: oben links
        Points.Add(new SKPoint(right, top));    // 1: oben rechts
        Points.Add(new SKPoint(right, bottom)); // 2: unten rechts
        Points.Add(new SKPoint(left,  bottom)); // 3: unten links
    }

    public void Draw(SKCanvas canvas)
    {
        if (!HasContent)
            return;

        // Füllen
        using var fillPaint = new SKPaint
        {
            Color = FillColor,
            IsStroke = false,
            IsAntialias = true
        };

        using var path = new SKPath();
        path.MoveTo(Points[0]);
        for (int i = 1; i < Points.Count; i++)
            path.LineTo(Points[i]);
        path.Close();

        canvas.DrawPath(path, fillPaint);

        // Rahmen
        using var linePaint = new SKPaint
        {
            Color = LineColor,
            StrokeWidth = LineThickness,
            IsStroke = true,
            IsAntialias = true
        };

        for (int i = 0; i < Points.Count; i++)
        {
            var next = (i + 1) % Points.Count;
            canvas.DrawLine(Points[i], Points[next], linePaint);
        }

        // Handles
        if (DisplayHandles)
        {
            using var handlePaint = new SKPaint
            {
                Color = PointColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            foreach (var p in Points)
                canvas.DrawCircle(p, PointRadius, handlePaint);
        }
    }

    /// <summary>
    /// Liefert den Index des angeklickten Eckpunkts
    /// </summary>
    public int? FindPointIndex(float x, float y)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var dx = Points[i].X - x;
            var dy = Points[i].Y - y;

            if (Math.Sqrt(dx * dx + dy * dy) <= HandleRadius)
                return i;
        }
        return null;
    }

    /// <summary>
    /// Verschiebt einen Eckpunkt und hält das Rechteck korrekt
    /// </summary>
    public void MovePoint(int index, SKPoint newPosition)
    {
        if (!HasContent || index < 0 || index > 3)
            return;

        // gegenüberliegender Punkt
        int opposite = (index + 2) % 4;

        Points[index] = newPosition;

        // X/Y der angrenzenden Punkte korrigieren
        Points[(index + 1) % 4] = new SKPoint(
            Points[opposite].X,
            newPosition.Y);

        Points[(index + 3) % 4] = new SKPoint(
            newPosition.X,
            Points[opposite].Y);
    }

    public void Reset()
    {
        Points.Clear();
    }
}
