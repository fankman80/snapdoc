using Microsoft.Maui.Graphics;

namespace SnapDoc;

public class InteractivePolylineDrawable : IDrawable
{
    public List<PointF> Points { get; set; } = [];
    public float HandleRadius { get; set; }
    public float PointRadius { get; set; }
    public bool IsClosed { get; private set; } = false;
    public Color FillColor { get; set; }
    public Color LineColor { get; set; }
    public Color PointColor { get; set; }
    public Color StartPointColor { get; set; }
    public float LineThickness { get; set; }

    public InteractivePolylineDrawable(
    Color? fillColor = null,
    Color? lineColor = null,
    Color? pointColor = null,
    Color? startPointColor = null,
    float lineThickness = 3f,
    float handleRadius = 15f,
    float pointRadius = 8f)
    {
        FillColor = fillColor ?? Colors.LightBlue.WithAlpha(0.3f);
        LineColor = lineColor ?? Colors.Blue.WithAlpha(0.5f);
        PointColor = pointColor ?? Colors.Blue.WithAlpha(0.5f);
        StartPointColor = startPointColor ?? Colors.Green;
        LineThickness = lineThickness;
        HandleRadius = handleRadius;
        PointRadius = pointRadius;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points.Count < 2)
            return;

        // Polygon füllen, falls geschlossen
        if (IsClosed)
        {
            using var path = new PathF();
            path.MoveTo(Points[0]);
            for (int i = 1; i < Points.Count; i++)
            {
                path.LineTo(Points[i]);
            }
            path.Close();

            canvas.FillColor = FillColor;
            canvas.FillPath(path);
        }

        // Linien
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = LineThickness;
        canvas.StrokeLineCap = LineCap.Round;

        for (int i = 0; i < Points.Count; i++)
        {
            var nextIndex = (i + 1) % Points.Count;
            if (!IsClosed && nextIndex == 0) break;
            canvas.DrawLine(Points[i], Points[nextIndex]);
        }

        // Punkte zeichnen
        for (int i = 0; i < Points.Count; i++)
        {
            if (i == 0)
                canvas.FillColor = StartPointColor;
            else
                canvas.FillColor = PointColor;

            canvas.FillCircle(Points[i], PointRadius);
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