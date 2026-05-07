using Codeuctivity.OpenXmlPowerTools;
using DocumentFormat.OpenXml.Drawing.Charts;
using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractivePolylineDrawable
{
    public bool HasContent => Points != null && Points.Count > 1;
    public List<SKPoint> Points { get; set; } = [];
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;
    public string StrokeStyle { get; set; } = "";
    public bool DisplayHandles { get; set; } = true;
    public bool IsClosed { get; set; } = false;
    public SKColor FillColor { get; set; } = SKColors.LightGreen.WithAlpha(128);
    public SKColor LineColor { get; set; } = SKColors.DarkGreen;
    public SKColor PointColor { get; set; } = SKColors.White.WithAlpha(160);
    public SKColor StartPointColor { get; set; } = SKColors.Green;
    private readonly float density = (float)Settings.DisplayDensity;

    public void Draw(SKCanvas canvas)
    {
        if (Points.Count < 2)
            return;

        // 1. Erstelle den Builder
        var builder = new SKPathBuilder();

        if (Points.Count > 0)
        {
            builder.MoveTo(Points[0]);
            for (int i = 1; i < Points.Count; i++)
            {
                builder.LineTo(Points[i]);
            }

            if (IsClosed)
                builder.Close();
        }

        using var path = builder.Detach();
        if (IsClosed)
        {
            using var fillPaint = new SKPaint
            {
                Color = FillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawPath(path, fillPaint);
        }

        if (LineThickness > 0)
        {
            using var linePaint = new SKPaint
            {
                Color = LineColor,
                StrokeWidth = LineThickness * density,
                IsStroke = true,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round, // Schöne Ecken!
                PathEffect = string.IsNullOrWhiteSpace(StrokeStyle)
                    ? null
                    : SKPathEffect.CreateDash(
                        Helper.ParseDashArray(StrokeStyle, density, LineThickness),
                        0f)
            };
            canvas.DrawPath(path, linePaint);
        }

        if (DisplayHandles)
        {
            using var handleStroke = new SKPaint
            {
                Color = SKColors.DarkGray,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };

            for (int i = 0; i < Points.Count; i++)
            {
                // Wenn das Polygon geschlossen ist, alle Punkte grau, sonst erster Punkt grün
                var color = (i == 0 && !IsClosed) ? StartPointColor : PointColor;

                using var pointPaint = new SKPaint
                {
                    Color = color,
                    Style = SKPaintStyle.Fill,
                    IsStroke = false,
                    IsAntialias = true
                };

                canvas.DrawCircle(Points[i], PointRadius * density, pointPaint);
                canvas.DrawCircle(Points[i], PointRadius * density, handleStroke);
            }
        }
    }

    public int? FindPointIndex(float x, float y)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var dx = Points[i].X - x;
            var dy = Points[i].Y - y;
            if (Math.Sqrt(dx * dx + dy * dy) <= HandleRadius * density)
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

        if (Math.Sqrt(dx * dx + dy * dy) <= HandleRadius * density)
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