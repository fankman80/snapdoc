using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveRulerDrawable
{
    private readonly float density = (float)Settings.DisplayDensity;
    public MeasureSettings Measure { get; set; } = new();
    public List<SKPoint> Points { get; } = [];
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 2f;
    public bool DisplayHandles { get; set; } = true;
    public SKColor PointColor { get; set; } = SKColors.White.WithAlpha(160);
    public bool HasPoints => Points.Count > 0;
    public bool HasContent => Points.Count == 2;
    public float RawLength => Points.Count == 2 ? SKPoint.Distance(Points[0], Points[1]) : 0f;
    public float Length => Points.Count == 2 ? RawLength * Measure.MeasureFactor : 0f;
    public string LengthText => Points.Count == 2 ? Measure.FormatLength(RawLength) : string.Empty;

    public void Draw(SKCanvas canvas)
    {
        if (Points.Count == 0)
            return;

        if (Points.Count == 2)
        {
            DrawLine(canvas);
            DrawMeasurement(canvas);
        }

        if (DisplayHandles)
            DrawHandles(canvas);
    }

    private void DrawLine(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = LineThickness * density * Measure.ViewScale,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        canvas.DrawLine(Points[0], Points[1], paint);
    }

    private void DrawMeasurement(SKCanvas canvas)
    {
        if (Points.Count != 2)
            return;

        float textSize = Measure.MeasureTextSize * density * Measure.ViewScale;
        using var painter = new MeasureLabelPainter(textSize, Measure.MeasureTextColor, Measure.MeasureBackgroundColor);

        painter.DrawSegment(canvas, Points[0], Points[1], Measure.FormatLength(RawLength));
    }

    private void DrawHandles(SKCanvas canvas)
    {
        using var handleStroke = new SKPaint
        {
            Color = SKColors.DarkGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f * density * Measure.ViewScale,
            IsAntialias = true
        };

        using var pointPaint = new SKPaint
        {
            Color = PointColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        foreach (var point in Points)
        {
            canvas.DrawCircle(point, PointRadius * density, pointPaint);
            canvas.DrawCircle(point, PointRadius * density, handleStroke);
        }
    }

    public int? FindPointIndex(float x, float y)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            float dx = Points[i].X - x;
            float dy = Points[i].Y - y;

            if (MathF.Sqrt(dx * dx + dy * dy) <= HandleRadius * density)
                return i;
        }

        return null;
    }

    public void Reset()
    {
        Points.Clear();
    }
}