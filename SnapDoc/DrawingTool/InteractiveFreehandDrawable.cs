using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveFreehandDrawable
{
    public List<List<SKPoint>> Points { get; set; } = [];
    public float LineThickness { get; set; } = 3f;
    public SKColor LineColor { get; set; } = SKColors.DarkGreen;
    public bool HasContent => Points.Any(stroke => stroke.Count > 1);
    private List<SKPoint>? _currentStroke;

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
            StrokeWidth = LineThickness * (float)Settings.DisplayDensity,
            IsStroke = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        foreach (var stroke in Points)
        {
            if (stroke.Count < 2) continue;

            using var path = new SKPath();

            path.MoveTo(stroke[0]);

            if (stroke.Count == 2)
            {
                path.LineTo(stroke[1]);
            }
            else
            {
                for (int i = 1; i < stroke.Count - 1; i++)
                {
                    var current = stroke[i];
                    var next = stroke[i + 1];
                    var midPoint = new SKPoint((current.X + next.X) / 2, (current.Y + next.Y) / 2);
                    path.QuadTo(current, midPoint);
                }
                path.LineTo(stroke.Last());
            }
            canvas.DrawPath(path, paint);
        }
    }

    public void Reset()
    {
        Points.Clear();
    }
}