using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveRectangleDrawable
{
    public bool HasContent => Width > 1f && Height > 1f;
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;
    public bool DisplayHandles { get; set; } = true;
    public bool IsDrawn { get; set; } = false;
    public SKColor FillColor { get; set; } = SKColors.Blue;
    public SKColor LineColor { get; set; } = SKColors.Blue;
    public SKColor TextColor { get; set; } = SKColors.Black;
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(160);
    public SKPoint Center { get; private set; }
    public float Width { get; private set; }
    public float Height { get; set; }
    public string Text { get; set; } = "";
    public int TextSize { get; set; } = 24;
    public float MinTextSize { get; set; } = 6f;
    public float MaxTextSize { get; set; } = 200f;
    public RectangleTextAlignment TextAlignment { get; set; } = RectangleTextAlignment.Center;
    public bool AutoSizeText { get; set; } = false;
    public float TextPadding { get; set; } = 8f;
    private static SKBitmap? _rotationHandleBitmap;
    private static bool _isLoading;
    private float _allowedAngleRad;
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
        InteractiveRectangleDrawable.EnsureRotationHandleLoaded();
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

    private static async void EnsureRotationHandleLoaded()
    {
        if (_rotationHandleBitmap != null || _isLoading)
            return;

        _isLoading = true;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("rotate_option.png");
            _rotationHandleBitmap = SKBitmap.Decode(stream);
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
            float handleDistance = PointRadius * 3;
            float yLocal = -Height / 2f - handleDistance;

            float cos = MathF.Cos(AllowedAngleRad);
            float sin = MathF.Sin(AllowedAngleRad);

            return new SKPoint(
                Center.X - yLocal * sin,
                Center.Y + yLocal * cos
            );
        }
    }

    public void Draw(SKCanvas canvas)
    {
        if (!HasContent) return;

        var pts = Points;

        using var path = new SKPath();
        path.MoveTo(pts[0]);
        path.LineTo(pts[1]);
        path.LineTo(pts[2]);
        path.LineTo(pts[3]);
        path.Close();

        using var fillPaint = new SKPaint
        {
            Color = FillColor,
            IsStroke = false,
            IsAntialias = true
        };
        canvas.DrawPath(path, fillPaint);

        using var linePaint = new SKPaint
        {
            Color = LineColor,
            StrokeWidth = LineThickness,
            IsStroke = true,
            IsAntialias = true
        };
        canvas.DrawPath(path, linePaint);

        // Draw text if available
        if (!string.IsNullOrEmpty(Text))
            DrawMultilineText(canvas);

        if (!DisplayHandles) return;

        using var handlePaint = new SKPaint
        {
            Color = PointColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        foreach (var p in pts)
            canvas.DrawCircle(p, PointRadius, handlePaint);

        if (_rotationHandleBitmap != null)
        {
#pragma warning disable CS0618 // FilterQuality ist veraltet, aber nötig
            var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
#pragma warning restore CS0618
            float size = PointRadius * 4;
            var destRect = new SKRect(
                RotationHandle.X - size / 2f,
                RotationHandle.Y - size / 2f,
                RotationHandle.X + size / 2f,
                RotationHandle.Y + size / 2f
            );
            canvas.DrawBitmap(_rotationHandleBitmap, destRect, paint);
        }
    }

    private void DrawMultilineText(SKCanvas canvas)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;

        canvas.Save();
        canvas.Translate(Center.X, Center.Y);
        canvas.RotateDegrees(AllowedAngleDeg);

        // Calculate max text area
        float maxTextWidth = Width - 2 * TextPadding;
        float maxTextHeight = Height - 2 * TextPadding;

        // Determine font size
        float fontSize = AutoSizeText
            ? CalculateAutoFontSize(Text, maxTextWidth, maxTextHeight)
            : TextSize;

        var font = new SKFont
        {
            Size = fontSize
        };

        var paint = new SKPaint
        {
            IsAntialias = true,
            Color = TextColor,
            IsStroke = false
        };

        float maxWidth = Width - 2 * TextPadding;
        var lines = BreakTextIntoLines(Text, font, maxWidth);

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
        var words = text.Split(' ');
        string line = "";

        foreach (var word in words)
        {
            var test = string.IsNullOrEmpty(line) ? word : $"{line} {word}";

            if (font.MeasureText(test) <= maxWidth)
                line = test;
            else
            {
                if (!string.IsNullOrEmpty(line))
                    result.Add(line);

                line = word;
            }
        }

        if (!string.IsNullOrEmpty(line))
            result.Add(line);

        return result;
    }

    float GetAlignedX(string line, SKFont font)
    {
        float lineWidth = font.MeasureText(line);

        return TextAlignment switch
        {
            RectangleTextAlignment.Left =>
                -Width / 2f + TextPadding,

            RectangleTextAlignment.Right =>
                Width / 2f - TextPadding - lineWidth,

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

    private static bool DoesTextFit(string text, float fontSize, float maxWidth, float maxHeight)
    {
        var font = new SKFont { Size = fontSize };

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

    public bool IsOverRotationHandle(SKPoint p) => SKPoint.Distance(p, RotationHandle) <= HandleRadius;

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
            if (SKPoint.Distance(p, pts[i]) <= HandleRadius)
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
        AllowedAngleRad = 0f;
        IsDrawn = false;
    }
}
