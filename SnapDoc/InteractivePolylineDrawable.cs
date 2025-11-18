using SkiaSharp;

namespace SnapDoc;

public class InteractivePolylineDrawable
{
    public List<SKPoint> Points { get; set; } = [];
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public bool IsClosed { get; private set; } = false;
    public SKColor FillColor { get; set; } = SKColors.Red.WithAlpha(128);
    public SKColor LineColor { get; set; } = SKColors.Red;
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(128);
    public SKColor StartPointColor { get; set; } = SKColors.Green;
    public float LineThickness { get; set; } = 3f;
    public bool HasContent => Points.Count > 1;

    public void Draw(SKCanvas canvas)
    {
        if (Points.Count < 2) return;

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

        // Punkte
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