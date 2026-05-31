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

    // --- Eigenschaften für den Wolken-Modus ---
    public bool IsCloud { get; set; } = false;
    public float CloudRadius { get; set; } = 20f;
    public float CloudOverlap { get; set; } = 0.8333f;
    public float CloudInciseDeg { get; set; } = 15f;

    public SKColor FillColor { get; set; } = SKColors.LightGreen.WithAlpha(128);
    public SKColor LineColor { get; set; } = SKColors.DarkGreen;
    public SKColor PointColor { get; set; } = SKColors.White.WithAlpha(160);
    public SKColor StartPointColor { get; set; } = SKColors.Green;
    private readonly float density = (float)Settings.DisplayDensity;

    private class CloudNode
    {
        public SKPoint Center { get; set; }
        public float BeginAngle { get; set; }
        public float EndAngle { get; set; }
    }

    public void Draw(SKCanvas canvas)
    {
        if (Points.Count < 2)
            return;

        // Entweder als Wolke oder als normale Linie zeichnen
        if (IsCloud)
            DrawCloudPath(canvas);
        else
            DrawNormalPath(canvas);

        // Handles werden in beiden Modi exakt gleich gezeichnet
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

    private void DrawNormalPath(SKCanvas canvas)
    {
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
                StrokeJoin = SKStrokeJoin.Round,
                PathEffect = string.IsNullOrWhiteSpace(StrokeStyle)
                    ? null
                    : SKPathEffect.CreateDash(
                        Helper.ParseDashArray(StrokeStyle, density, LineThickness),
                        0f)
            };
            canvas.DrawPath(path, linePaint);
        }
    }

    private void DrawCloudPath(SKCanvas canvas)
    {
        float radius = CloudRadius * density;
        float delta = 2f * radius * CloudOverlap;

        var workingPoints = new List<SKPoint>(Points);
        if (IsClosed && IsClockwise(workingPoints))
            workingPoints.Reverse();

        var nodes = new List<CloudNode>();
        SKPoint prev = IsClosed ? workingPoints[^1] : workingPoints[0];
        int startIndex = IsClosed ? 0 : 1;

        // Wenn nicht geschlossen, fügen wir den Startpunkt explizit hinzu
        if (!IsClosed)
            nodes.Add(new CloudNode { Center = prev });

        // Kreis-Zentren berechnen
        for (int i = startIndex; i < workingPoints.Count; i++)
        {
            SKPoint curr = workingPoints[i];
            float dx = curr.X - prev.X;
            float dy = curr.Y - prev.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);

            if (len > 0)
            {
                dx /= len;
                dy /= len;

                int n = (int)(len / delta + 0.5f);
                if (n < 1) n = 1;
                float d = len / n;

                float startA = (!IsClosed && i == startIndex) ? d : 0f;

                for (float a = startA; a + 0.1f * d < len; a += d)
                {
                    nodes.Add(new CloudNode { Center = new SKPoint(prev.X + a * dx, prev.Y + a * dy) });
                }
            }
            prev = curr;
        }

        // Wenn nicht geschlossen, den allerletzten Punkt hinzufügen
        if (!IsClosed && workingPoints.Count > 1)
            nodes.Add(new CloudNode { Center = workingPoints[^1] });

        // Schnittwinkel der benachbarten Kreise berechnen
        if (nodes.Count > 1)
        {
            CloudNode prevNode = IsClosed ? nodes[^1] : nodes[0];
            int startNodeIdx = IsClosed ? 0 : 1;

            if (!IsClosed)
                nodes[0].BeginAngle = 0;

            for (int i = startNodeIdx; i < nodes.Count; i++)
            {
                CloudNode currNode = nodes[i];
                var (end, begin) = CalculateIntersectAngles(prevNode.Center, currNode.Center, radius);

                prevNode.EndAngle = end;
                currNode.BeginAngle = begin;
                prevNode = currNode;
            }

            if (!IsClosed)
            {
                nodes[0].BeginAngle = nodes[0].EndAngle - MathF.PI;
                nodes[^1].EndAngle = nodes[^1].BeginAngle + MathF.PI;
            }
        }

        if (IsClosed)
        {
            using var fillPaint = new SKPaint
            {
                Color = FillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            var fillBuilder = new SKPathBuilder();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                float startDeg = (node.BeginAngle * 180f / MathF.PI) % 360f;
                if (startDeg < 0) startDeg += 360f;
                float endDeg = (node.EndAngle * 180f / MathF.PI) % 360f;
                if (endDeg < 0) endDeg += 360f;
                float sweepDeg = endDeg - startDeg;
                if (sweepDeg < 0) sweepDeg += 360f;

                var rect = new SKRect(node.Center.X - radius, node.Center.Y - radius, node.Center.X + radius, node.Center.Y + radius);
                fillBuilder.ArcTo(rect, startDeg, sweepDeg, false);
            }
            fillBuilder.Close();

            using var fillPath = fillBuilder.Detach();
            canvas.DrawPath(fillPath, fillPaint);
        }

        if (LineThickness > 0)
        {
            using var linePaint = new SKPaint
            {
                Color = LineColor,
                StrokeWidth = LineThickness * density,
                IsStroke = true,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round,
                PathEffect = string.IsNullOrWhiteSpace(StrokeStyle)
                    ? null
                    : SKPathEffect.CreateDash(Helper.ParseDashArray(StrokeStyle, density, LineThickness), 0f)
            };

            var outlineBuilder = new SKPathBuilder();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                float startDeg = (node.BeginAngle * 180f / MathF.PI) % 360f;
                if (startDeg < 0) startDeg += 360f;
                float endDeg = (node.EndAngle * 180f / MathF.PI) % 360f;
                if (endDeg < 0) endDeg += 360f;
                float sweepDeg = endDeg - startDeg;
                if (sweepDeg < 0) sweepDeg += 360f;

                var rect = new SKRect(node.Center.X - radius, node.Center.Y - radius, node.Center.X + radius, node.Center.Y + radius);

                outlineBuilder.AddArc(rect, startDeg, sweepDeg + CloudInciseDeg);
            }

            using var outlinePath = outlineBuilder.Detach();
            canvas.DrawPath(outlinePath, linePaint);
        }
    }

    // Berechnet die Winkel der Schnittpunkte zweier Kreise
    private static (float endAngle, float beginAngle) CalculateIntersectAngles(SKPoint p, SKPoint q, float r)
    {
        float dx = q.X - p.X;
        float dy = q.Y - p.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float a = 0.5f * len / r;

        if (a < -1f) a = -1f;
        if (a > 1f) a = 1f;

        float phi = MathF.Atan2(dy, dx);
        float gamma = MathF.Acos(a);

        return (phi - gamma, MathF.PI + phi + gamma);
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

    private static bool IsClockwise(List<SKPoint> points)
    {
        if (points.Count < 3)
            return false;

        float sum = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];

            sum += (p2.X - p1.X) * (p2.Y + p1.Y);
        }

        return sum > 0f;
    }

    public void Reset()
    {
        IsClosed = false;
        Points.Clear();
    }
}