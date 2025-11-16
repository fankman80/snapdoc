namespace SnapDoc;

public class InteractiveFreehandDrawable : IDrawable
{
    public List<List<PointF>> Strokes { get; set; } = [];
    public float LineThickness { get; set; } = 3f;
    public Color LineColor { get; set; } = Colors.Black;

    private List<PointF>? _currentStroke;

    public void StartStroke()
    {
        _currentStroke = new List<PointF>();
        Strokes.Add(_currentStroke);
    }

    public void AddPoint(PointF point)
    {
        _currentStroke?.Add(point);
    }

    public void EndStroke()
    {
        _currentStroke = null;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = LineThickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var stroke in Strokes)
        {
            if (stroke.Count < 2) continue;
            for (int i = 0; i < stroke.Count - 1; i++)
            {
                canvas.DrawLine(stroke[i], stroke[i + 1]);
            }
        }
    }

    public void Reset()
    {
        Strokes.Clear();
    }
}