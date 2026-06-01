using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveRectangleDrawable
{
    public bool HasContent => Width > 1f && Height > 1f;
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;
    public string StrokeStyle { get; set; } = "";
    public bool DisplayHandles { get; set; } = true;
    public bool IsDrawn { get; set; } = false;
    public SKColor FillColor { get; set; } = SKColors.LightGreen.WithAlpha(128);
    public SKColor LineColor { get; set; } = SKColors.DarkGreen;
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor PointColor { get; set; } = SKColors.White.WithAlpha(160);
    public SKPoint Center { get; private set; }
    public float Width { get; private set; }
    public float Height { get; set; }
    public string Text { get; set; } = "";
    public float TextSize { get; set; } = 60;
    public float MinTextSize { get; set; } = 6f;
    public float MaxTextSize { get; set; } = 200f;
    public RectangleTextAlignment TextAlignment { get; set; } = RectangleTextAlignment.Center;
    public RectangleTextStyle TextStyle { get; set; } = RectangleTextStyle.Normal;
    public bool AutoSizeText { get; set; } = true;
    public int TextPadding { get; set; } = 10;

    private static SKImage? _rotationHandleImage;
    private static bool _isLoading;
    private readonly float density = (float)Settings.DisplayDensity;
    private float _allowedAngleRad;

    // --- Eigenschaften für den Wolken-Modus ---
    public bool IsCloud { get; set; } = false;
    public float CloudRadius { get; set; } = 20f;
    public float CloudOverlap { get; set; } = 0.8333f;
    public float CloudInciseDeg { get; set; } = 15f;

    public float AllowedAngleRad
    {
        get => _allowedAngleRad;
        set => _allowedAngleRad = value;
    }
    public float AllowedAngleDeg
    {
        get => AllowedAngleRad * 180f / MathF.PI;
        set => AllowedAngleRad = value * MathF.PI / 180f;
    }

    public InteractiveRectangleDrawable()
    {
        InteractiveRectangleDrawable.EnsureRotationHandleLoaded().Wait();
    }

    public SKPoint[] Points
    {
        get
        {
            float hw = Width / 2f;
            float hh = Height / 2f;

            var local = new[]
            {
                new SKPoint(-hw, -hh),
                new SKPoint( hw, -hh),
                new SKPoint( hw,  hh),
                new SKPoint(-hw,  hh),
            };

            float cos = MathF.Cos(AllowedAngleRad);
            float sin = MathF.Sin(AllowedAngleRad);

            var pts = new SKPoint[4];
            for (int i = 0; i < 4; i++)
            {
                pts[i] = new SKPoint(
                    Center.X + local[i].X * cos - local[i].Y * sin,
                    Center.Y + local[i].X * sin + local[i].Y * cos
                );
            }

            return pts;
        }
    }

    private static async Task EnsureRotationHandleLoaded()
    {
        if (_rotationHandleImage != null || _isLoading)
            return;

        _isLoading = true;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("rotate_option.png");
            using var tempBitmap = SKBitmap.Decode(stream);
            _rotationHandleImage = SKImage.FromBitmap(tempBitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden des Handles: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    public SKPoint RotationHandle
    {
        get
        {
            float handleDistance = PointRadius * density * 3;
            float yLocal = -Height / 2f - handleDistance;

            float cos = MathF.Cos(AllowedAngleRad);
            float sin = MathF.Sin(AllowedAngleRad);

            return new SKPoint(
                Center.X - yLocal * sin,
                Center.Y + yLocal * cos
            );
        }
    }

    private class CloudNode
    {
        public SKPoint Center { get; set; }
        public float BeginAngle { get; set; }
        public float EndAngle { get; set; }
    }

    public void Draw(SKCanvas canvas)
    {
        if (!HasContent) return;

        // Entweder als Wolke oder als normales Rechteck zeichnen
        if (IsCloud)
        {
            DrawCloudPath(canvas);
        }
        else
        {
            var pts = Points;

            var builder = new SKPathBuilder();
            builder.MoveTo(pts[0]);
            builder.LineTo(pts[1]);
            builder.LineTo(pts[2]);
            builder.LineTo(pts[3]);
            builder.Close();

            using var path = builder.Detach();
            using var fillPaint = new SKPaint
            {
                Color = FillColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };

            canvas.DrawPath(path, fillPaint);

            if (LineThickness > 0)
            {
                using var linePaint = new SKPaint
                {
                    Color = LineColor,
                    StrokeWidth = LineThickness * density,
                    IsStroke = true,
                    IsAntialias = true,
                    PathEffect = string.IsNullOrWhiteSpace(StrokeStyle)
                    ? null
                    : SKPathEffect.CreateDash(
                        Helper.ParseDashArray(StrokeStyle, density, LineThickness),
                        0f)
                };
                canvas.DrawPath(path, linePaint);
            }
        }

        // Text wird unabhaengig vom Modus gezeichnet
        if (!string.IsNullOrEmpty(Text))
            DrawMultilineText(canvas);

        if (!DisplayHandles) return;

        using var handlePaint = new SKPaint
        {
            Color = PointColor,
            Style = SKPaintStyle.Fill,
            IsStroke = false,
            IsAntialias = true
        };

        using var handleStroke = new SKPaint
        {
            Color = SKColors.DarkGray,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        foreach (var p in Points)
        {
            canvas.DrawCircle(p, PointRadius * density, handlePaint);
            canvas.DrawCircle(p, PointRadius * density, handleStroke);
        }

        if (_rotationHandleImage != null)
        {
            using var paint = new SKPaint { IsAntialias = true };

            float size = PointRadius * density * 4;
            var destRect = new SKRect(
                RotationHandle.X - size / 2f,
                RotationHandle.Y - size / 2f,
                RotationHandle.X + size / 2f,
                RotationHandle.Y + size / 2f
            );

            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

            canvas.DrawImage(_rotationHandleImage, destRect, sampling, paint);
        }
    }

    private void DrawCloudPath(SKCanvas canvas)
    {
        float radius = CloudRadius * density;
        float delta = 2f * radius * CloudOverlap;

        var workingPoints = new List<SKPoint>(Points);
        if (IsClockwise(workingPoints))
            workingPoints.Reverse();

        var nodes = new List<CloudNode>();
        SKPoint prev = workingPoints[^1];

        // Kreis-Zentren entlang der 4 Kanten berechnen
        for (int i = 0; i < workingPoints.Count; i++)
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

                for (float a = 0f; a + 0.1f * d < len; a += d)
                {
                    nodes.Add(new CloudNode { Center = new SKPoint(prev.X + a * dx, prev.Y + a * dy) });
                }
            }
            prev = curr;
        }

        // Schnittwinkel der benachbarten Kreise berechnen
        if (nodes.Count > 1)
        {
            CloudNode prevNode = nodes[^1];

            for (int i = 0; i < nodes.Count; i++)
            {
                CloudNode currNode = nodes[i];
                var (end, begin) = CalculateIntersectAngles(prevNode.Center, currNode.Center, radius);

                prevNode.EndAngle = end;
                currNode.BeginAngle = begin;
                prevNode = currNode;
            }
        }

        if (FillColor.Alpha > 0)
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

    private void DrawMultilineText(SKCanvas canvas)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;

        canvas.Save();
        canvas.Translate(Center.X, Center.Y);
        canvas.RotateDegrees(AllowedAngleDeg);

        float maxTextWidth = Width / 100 * (100 - TextPadding);
        float maxTextHeight = Height / 100 * (100 - TextPadding);

        TextSize = AutoSizeText
            ? CalculateAutoFontSize(Text, maxTextWidth, maxTextHeight)
            : TextSize;

        var font = new SKFont(TextStyle.ToTypeface())
        {
            Size = TextSize
        };

        var paint = new SKPaint
        {
            IsAntialias = true,
            Color = TextColor,
            IsStroke = false
        };

        var lines = BreakTextIntoLines(Text, font, maxTextWidth);

        var metrics = font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent;
        float totalHeight = lines.Count * lineHeight;
        float y = -totalHeight / 2f - metrics.Ascent;

        canvas.ClipRect(new SKRect(
            -Width / 2f,
            -Height / 2f,
                Width / 2f,
                Height / 2f
        ));

        foreach (var line in lines)
        {
            float x = GetAlignedX(line, font);
            canvas.DrawText(line, x, y, font, paint);
            y += lineHeight;
        }

        canvas.Restore();
    }

    private static List<string> BreakTextIntoLines(string text, SKFont font, float maxWidth)
    {
        var result = new List<string>();
        var hardLines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        foreach (var hardLine in hardLines)
        {
            if (string.IsNullOrEmpty(hardLine))
            {
                result.Add(string.Empty);
                continue;
            }

            var words = hardLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string line = "";

            foreach (var word in words)
            {
                var test = string.IsNullOrEmpty(line) ? word : $"{line} {word}";

                if (font.MeasureText(test) <= maxWidth)
                {
                    line = test;
                }
                else
                {
                    if (!string.IsNullOrEmpty(line))
                        result.Add(line);

                    line = word;
                }
            }

            if (!string.IsNullOrEmpty(line))
                result.Add(line);
        }
        return result;
    }

    float GetAlignedX(string line, SKFont font)
    {
        float lineWidth = font.MeasureText(line);

        return TextAlignment switch
        {
            RectangleTextAlignment.Left =>
                -Width / 2f + TextPadding * density,

            RectangleTextAlignment.Right =>
                Width / 2f - TextPadding * density - lineWidth,

            _ => // Center
                -lineWidth / 2f
        };
    }

    private float CalculateAutoFontSize(string text, float maxWidth, float maxHeight)
    {
        float low = MinTextSize;
        float high = MaxTextSize;
        float best = low;

        while (high - low > 0.5f)
        {
            float mid = (low + high) / 2f;

            if (DoesTextFit(text, mid, maxWidth, maxHeight))
            {
                best = mid;
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return best;
    }

    private bool DoesTextFit(string text, float fontSize, float maxWidth, float maxHeight)
    {
        var font = new SKFont(TextStyle.ToTypeface())
        {
            Size = fontSize
        };

        var lines = BreakTextIntoLines(text, font, maxWidth);

        var metrics = font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent;
        float totalHeight = lines.Count * lineHeight;

        if (totalHeight > maxHeight)
            return false;

        foreach (var line in lines)
        {
            if (font.MeasureText(line) > maxWidth)
                return false;
        }

        return true;
    }

    public bool IsOverRotationHandle(SKPoint p) => SKPoint.Distance(p, RotationHandle) <= HandleRadius * density;

    public void SetRotationFromPoint(SKPoint p)
    {
        var dx = p.X - Center.X;
        var dy = p.Y - Center.Y;
        AllowedAngleRad = MathF.Atan2(dy, dx) + MathF.PI / 2;
    }

    public int? FindPointIndex(float x, float y)
    {
        var p = new SKPoint(x, y);
        var pts = Points;

        for (int i = 0; i < pts.Length; i++)
        {
            if (SKPoint.Distance(p, pts[i]) <= HandleRadius * density)
                return i;
        }

        return null;
    }

    public SKPoint GetOppositePoint(int index)
    {
        int opposite = (index + 2) % 4;
        return Points[opposite];
    }

    public void SetFromDrag(SKPoint start, SKPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;

        float cos = MathF.Cos(-AllowedAngleRad);
        float sin = MathF.Sin(-AllowedAngleRad);

        float localX = dx * cos - dy * sin;
        float localY = dx * sin + dy * cos;

        const float minSize = 2f;
        Width = MathF.Max(minSize, MathF.Abs(localX));
        Height = MathF.Max(minSize, MathF.Abs(localY));

        Center = new SKPoint(
            start.X + localX / 2f * MathF.Cos(AllowedAngleRad) - localY / 2f * MathF.Sin(AllowedAngleRad),
            start.Y + localX / 2f * MathF.Sin(AllowedAngleRad) + localY / 2f * MathF.Cos(AllowedAngleRad)
        );
    }

    public void Reset()
    {
        Width = 0;
        Height = 0;
        Center = default;
        IsDrawn = false;
    }
}