using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveArrowDrawable
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
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(160);
    public SKPoint Center { get; private set; }
    public float Width { get; private set; }
    public float Height { get; set; }
    public float ShaftFactor { get; set; } = 0.5f; // Dicke des Hinterteils (0 bis 1)
    public float TipFactor { get; set; } = 0.3f;   // Länge der Spitze (0 bis 1)
    private static SKImage? _rotationHandleImage;
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
    public InteractiveArrowDrawable()
    {
        InteractiveArrowDrawable.EnsureRotationHandleLoaded().Wait();
    }

    public SKPoint[] BoundingPoints
    {
        get
        {
            float hw = Width / 2f;
            float hh = Height / 2f;
            var local = new[] {
                new SKPoint(-hw, -hh), new SKPoint(hw, -hh),
                new SKPoint(hw, hh),   new SKPoint(-hw, hh)
            };
            return TransformPoints(local);
        }
    }

    public SKPoint ShapeHandle
    {
        get
        {
            float shoulderX = (Width / 2f) - (Width * TipFactor);
            float shoulderY = -(Height / 2f * ShaftFactor);

            return TransformPoint(new SKPoint(shoulderX, shoulderY));
        }
    }

    private SKPoint[] TransformPoints(SKPoint[] localPts)
    {
        float cos = MathF.Cos(AllowedAngleRad);
        float sin = MathF.Sin(AllowedAngleRad);
        var pts = new SKPoint[localPts.Length];

        for (int i = 0; i < localPts.Length; i++)
        {
            // Standard Rotationsmatrix + Translation zum Center
            pts[i] = new SKPoint(
                Center.X + (localPts[i].X * cos - localPts[i].Y * sin),
                Center.Y + (localPts[i].X * sin + localPts[i].Y * cos)
            );
        }
        return pts;
    }

    private SKPoint TransformPoint(SKPoint p)
        => TransformPoints([p])[0];

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
            float handleDistance = PointRadius * (float)Settings.DisplayDensity * 3;
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

        // 1. Erstelle den Builder
        var builder = new SKPathBuilder();

        // Pfeil-Geometrie berechnen (lokal)
        float hw = Width / 2f;
        float hh = Height / 2f;
        float tipLength = Width * TipFactor;
        float shaftHeight = Height * ShaftFactor;
        float sh2 = shaftHeight / 2f;

        var arrowLocalPath = new[] {
        new SKPoint(-hw, -sh2),                  // Heck oben
        new SKPoint(hw - tipLength, -sh2),       // Schulter oben
        new SKPoint(hw - tipLength, -hh),        // Flügel oben
        new SKPoint(hw, 0),                      // Spitze
        new SKPoint(hw - tipLength, hh),         // Flügel unten
        new SKPoint(hw - tipLength, sh2),        // Schulter unten
        new SKPoint(-hw, sh2)                    // Heck unten
    };

        var worldPts = TransformPoints(arrowLocalPath);

        builder.MoveTo(worldPts[0]);
        for (int i = 1; i < worldPts.Length; i++)
        {
            builder.LineTo(worldPts[i]);
        }
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
                StrokeWidth = LineThickness * (float)Settings.DisplayDensity,
                IsStroke = true,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                PathEffect = string.IsNullOrWhiteSpace(StrokeStyle)
                    ? null
                    : SKPathEffect.CreateDash(
                        Helper.ParseDashArray(StrokeStyle, (float)Settings.DisplayDensity, LineThickness),
                        0f)
            };
            canvas.DrawPath(path, linePaint);
        }

        if (DisplayHandles) DrawHandles(canvas);
    }

    private void DrawHandles(SKCanvas canvas)
    {
        float density = (float)Settings.DisplayDensity;
        using var handlePaint = new SKPaint { Color = PointColor, Style = SKPaintStyle.Fill, IsAntialias = true };

        // Eck-Handles
        foreach (var p in BoundingPoints)
            canvas.DrawCircle(p, PointRadius * density, handlePaint);

        // Form-Handle (Schaft/Spitze)
        canvas.DrawCircle(ShapeHandle, PointRadius * density, handlePaint);

        if (_rotationHandleImage != null) // Prüfung auf das geladene Image
        {
            using var paint = new SKPaint { IsAntialias = true };

            float size = PointRadius * (float)Settings.DisplayDensity * 4;
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

    public int? FindPointIndex(float x, float y)
    {
        float density = (float)Settings.DisplayDensity;
        var p = new SKPoint(x, y);

        // Check Bounding Points (0-3)
        var pts = BoundingPoints;
        for (int i = 0; i < pts.Length; i++)
            if (SKPoint.Distance(p, pts[i]) <= HandleRadius * density) return i;

        // Check Shape Handle (Index 4)
        if (SKPoint.Distance(p, ShapeHandle) <= HandleRadius * density) return 4;

        return null;
    }

    public void UpdateShape(SKPoint worldPoint)
    {
        // 1. Weltkoordinaten in lokale Koordinaten umrechnen
        var dx = worldPoint.X - Center.X;
        var dy = worldPoint.Y - Center.Y;

        float cos = MathF.Cos(-AllowedAngleRad);
        float sin = MathF.Sin(-AllowedAngleRad);

        float localX = dx * cos - dy * sin;
        float localY = dx * sin + dy * cos;

        // 2. TipFactor berechnen (Horizontale Bewegung)
        // Wie weit ist der Punkt von der rechten Kante entfernt?
        float distFromRight = (Width / 2f) - localX;
        TipFactor = Math.Clamp(distFromRight / Width, 0f, 1f);

        // 3. ShaftFactor berechnen (Vertikale Bewegung)
        // Wie weit ist der Punkt von der horizontalen Mittellinie entfernt?
        ShaftFactor = Math.Clamp(Math.Abs(localY) / (Height / 2f), 0.05f, 1f);
    }

    public bool IsOverRotationHandle(SKPoint p) => SKPoint.Distance(p, RotationHandle) <= HandleRadius * (float)Settings.DisplayDensity;

    public void SetRotationFromPoint(SKPoint p)
    {
        var dx = p.X - Center.X;
        var dy = p.Y - Center.Y;
        AllowedAngleRad = MathF.Atan2(dy, dx) + MathF.PI / 2;
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
