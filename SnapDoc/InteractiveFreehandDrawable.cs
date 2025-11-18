using SkiaSharp;

namespace SnapDoc;

public class InteractiveFreehandDrawable
{
    public List<List<SKPoint>> Strokes { get; set; } = [];
    public float LineThickness { get; set; } = 3f;
    public SKColor LineColor { get; set; } = SKColors.Black;

    private List<SKPoint>? _currentStroke;

    public void StartStroke()
    {
        _currentStroke = new List<SKPoint>();
        Strokes.Add(_currentStroke);
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

        foreach (var stroke in Strokes)
        {
            if (stroke.Count < 2) continue;
            for (int i = 0; i < stroke.Count - 1; i++)
            {
                canvas.DrawLine(stroke[i], stroke[i + 1], paint);
            }
        }
    }

    public void Reset()
    {
        Strokes.Clear();
    }
}
