using SkiaSharp;

namespace SnapDoc;

public class InteractivePolylineDrawable
{
    public List<SKPoint> Points { get; set; } = [];
    public float HandleRadius { get; set; }
    public float PointRadius { get; set; }
    public bool IsClosed { get; private set; } = false;
    public SKColor FillColor { get; set; }
    public SKColor LineColor { get; set; }
    public SKColor PointColor { get; set; }
    public SKColor StartPointColor { get; set; }
    public float LineThickness { get; set; }

    public InteractivePolylineDrawable(
        SKColor? fillColor = null,
        SKColor? lineColor = null,
        SKColor? pointColor = null,
        SKColor? startPointColor = null,
        float lineThickness = 3f,
        float handleRadius = 15f,
        float pointRadius = 8f)
    {
        FillColor = fillColor ?? new SKColor(173, 216, 230, 77); // LightBlue 30%
        LineColor = lineColor ?? new SKColor(30, 144, 255, 128); // Blue 50%
        PointColor = pointColor ?? new SKColor(30, 144, 255, 128);
        StartPointColor = startPointColor ?? SKColors.Green;
        LineThickness = lineThickness;
        HandleRadius = handleRadius;
        PointRadius = pointRadius;
    }

    public void Draw(SKCanvas canvas)
    {
        if (Points.Count < 2)
            return;

        // Polygon füllen, falls geschlossen
        if (IsClosed)
        {
            using var path = new SKPath();
            path.MoveTo(Points[0]);
            for (int i = 1; i < Points.Count; i++)
            {
                path.LineTo(Points[i]);
            }
            path.Close();

            using var fillPaint = new SKPaint
            {
                Color = FillColor,
                IsStroke = false,
                IsAntialias = true
            };

            canvas.DrawPath(path, fillPaint);
        }

        // Linien
        using var linePaint = new SKPaint
        {
            Color = LineColor,
            StrokeWidth = LineThickness,
            IsStroke = true,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        for (int i = 0; i < Points.Count; i++)
        {
            var nextIndex = (i + 1) % Points.Count;
            if (!IsClosed && nextIndex == 0) break;
            canvas.DrawLine(Points[i], Points[nextIndex], linePaint);
        }

        // Punkte zeichnen
        for (int i = 0; i < Points.Count; i++)
        {
            var color = i == 0 ? StartPointColor : PointColor;
            using var pointPaint = new SKPaint
            {
                Color = color,
                IsStroke = false,
                IsAntialias = true
            };
            canvas.DrawCircle(Points[i], PointRadius, pointPaint);
        }
    }

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

    public bool TryClosePolygon(float x, float y)
    {
        if (IsClosed || Points.Count < 3)
            return false;

        var start = Points[0];
        var dx = start.X - x;
        var dy = start.Y - y;

        if (Math.Sqrt(dx * dx + dy * dy) <= HandleRadius)
        {
            IsClosed = true;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        IsClosed = false;
        Points.Clear();
    }
}
