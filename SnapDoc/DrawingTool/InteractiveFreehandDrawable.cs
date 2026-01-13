using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveFreehandDrawable
{
    public List<List<SKPoint>> Points { get; set; } = [];
    public float LineThickness { get; set; } = 3f;
    public SKColor LineColor { get; set; } = SKColors.Black;
    private List<SKPoint>? _currentStroke;
    public bool HasContent => Points.Any(stroke => stroke.Count > 1);
    public void StartStroke()
    {
        _currentStroke = [];
        Points.Add(_currentStroke);
    }

    public void AddPoint(SKPoint point)
    {
        _currentStroke?.Add(point);
    }

    public void EndStroke()
    {
        _currentStroke = null;
    }

    public void Draw(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = LineColor,
            StrokeWidth = LineThickness,
            IsStroke = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        foreach (var point in Points)
        {
            if (point.Count < 2) continue;
            for (int i = 0; i < point.Count - 1; i++)
            {
                canvas.DrawLine(point[i], point[i + 1], paint);
            }
        }
    }

    public void Reset()
    {
        Points.Clear();
    }
}