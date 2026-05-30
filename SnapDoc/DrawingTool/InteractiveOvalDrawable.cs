using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveOvalDrawable
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
    public SKColor PointColor { get; set; } = SKColors.White.WithAlpha(160);
    public SKPoint Center { get; private set; }
    public float Width { get; private set; }
    public float Height { get; set; }

    private static SKImage? _rotationHandleImage;
    private static bool _isLoading;
    private readonly float density = (float)Settings.DisplayDensity;
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

    public InteractiveOvalDrawable()
    {
        InteractiveOvalDrawable.EnsureRotationHandleLoaded().Wait();
    }

    public SKPoint[] Points
    {
        get
        {
            float hw = Width / 2f;
            float hh = Height / 2f;

            var local = new[]
            {
                new SKPoint(0, -hh),
                new SKPoint(hw, 0),
                new SKPoint(0, hh),
                new SKPoint(-hw, 0),
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

    public void Draw(SKCanvas canvas)
    {
        if (!HasContent) return;

        canvas.Save();
        canvas.Translate(Center.X, Center.Y);
        canvas.RotateDegrees(AllowedAngleDeg);

        var ovalRect = new SKRect(-Width / 2f, -Height / 2f, Width / 2f, Height / 2f);

        using var fillPaint = new SKPaint
        {
            Color = FillColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawOval(ovalRect, fillPaint);

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
            canvas.DrawOval(ovalRect, linePaint);
        }

        canvas.Restore();

        if (!DisplayHandles) return;

        var pts = Points;

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

        foreach (var p in pts)
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

    public void ResizeFromHandle(int handleIndex, SKPoint currentTouch)
    {
        var dx = currentTouch.X - Center.X;
        var dy = currentTouch.Y - Center.Y;

        float cos = MathF.Cos(-AllowedAngleRad);
        float sin = MathF.Sin(-AllowedAngleRad);

        float localX = dx * cos - dy * sin;
        float localY = dx * sin + dy * cos;

        const float minSize = 10f;
        float localShiftX = 0f;
        float localShiftY = 0f;

        switch (handleIndex)
        {
            case 0:
                {
                    float newHeight = Height / 2f - localY;
                    if (newHeight < minSize) newHeight = minSize;
                    float deltaH = newHeight - Height;
                    Height = newHeight;
                    localShiftY = -deltaH / 2f;
                }
                break;

            case 1:
                {
                    float newWidth = localX + Width / 2f;
                    if (newWidth < minSize) newWidth = minSize;
                    float deltaW = newWidth - Width;
                    Width = newWidth;
                    localShiftX = deltaW / 2f;
                }
                break;

            case 2:
                {
                    float newHeight = localY + Height / 2f;
                    if (newHeight < minSize) newHeight = minSize;
                    float deltaH = newHeight - Height;
                    Height = newHeight;
                    localShiftY = deltaH / 2f;
                }
                break;

            case 3:
                {
                    float newWidth = Width / 2f - localX;
                    if (newWidth < minSize) newWidth = minSize;
                    float deltaW = newWidth - Width;
                    Width = newWidth;
                    localShiftX = -deltaW / 2f;
                }
                break;
        }

        Center = new SKPoint(
            Center.X + localShiftX * MathF.Cos(AllowedAngleRad) - localShiftY * MathF.Sin(AllowedAngleRad),
            Center.Y + localShiftX * MathF.Sin(AllowedAngleRad) + localShiftY * MathF.Cos(AllowedAngleRad)
        );
    }

    public IEnumerable<SKPoint> GetBoundingCorners()
    {
        float hw = Width / 2f;
        float hh = Height / 2f;

        var localCorners = new[]
        {
        new SKPoint(-hw, -hh), // Oben-Links
        new SKPoint(hw, -hh),  // Oben-Rechts
        new SKPoint(hw, hh),   // Unten-Rechts
        new SKPoint(-hw, hh)   // Unten-Links
        };

        float cos = MathF.Cos(AllowedAngleRad);
        float sin = MathF.Sin(AllowedAngleRad);

        var corners = new SKPoint[4];
        for (int i = 0; i < 4; i++)
        {
            corners[i] = new SKPoint(
                Center.X + localCorners[i].X * cos - localCorners[i].Y * sin,
                Center.Y + localCorners[i].X * sin + localCorners[i].Y * cos
            );
        }

        return corners;
    }

    public void Reset()
    {
        Width = 0;
        Height = 0;
        Center = default;
        IsDrawn = false;
    }
}