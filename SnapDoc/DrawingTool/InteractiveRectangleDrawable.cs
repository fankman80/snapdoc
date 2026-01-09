using SkiaSharp;

namespace SnapDoc.DrawingTool;

public class InteractiveRectangleDrawable
{
    // === Eigenschaften ===
    public bool HasContent => Width > 1f && Height > 1f;
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;
    public bool DisplayHandles { get; set; } = true;
    public SKColor FillColor { get; set; } = SKColors.Blue.WithAlpha(100);
    public SKColor LineColor { get; set; } = SKColors.Blue;
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(160);
    public SKPoint Center { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
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

    // === Eckpunkte ===
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

    // === Rotationshandle ===
    public SKPoint RotationHandle
    {
        get
        {
            float handleDistance = 30f;
            float yLocal = -Height / 2f - handleDistance;

            float cos = MathF.Cos(AllowedAngleRad);
            float sin = MathF.Sin(AllowedAngleRad);

            return new SKPoint(
                Center.X - yLocal * sin,
                Center.Y + yLocal * cos
            );
        }
    }

    // === Methoden ===
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

    public async void Draw(SKCanvas canvas)
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

        if (!DisplayHandles) return;

        using var handlePaint = new SKPaint
        {
            Color = PointColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        foreach (var p in pts)
            canvas.DrawCircle(p, PointRadius, handlePaint);

        // Rotationshandle (Bitmap oder Icon)
        using var stream = await FileSystem.OpenAppPackageFileAsync("rotate_option.png");
        using var bitmap = SKBitmap.Decode(stream);
        canvas.DrawBitmap(bitmap, new SKPoint(RotationHandle.X - bitmap.Width / 2, RotationHandle.Y - bitmap.Height / 2));
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

    public void MovePoint(int index, SKPoint newPos)
    {
        float cos = MathF.Cos(-AllowedAngleRad);
        float sin = MathF.Sin(-AllowedAngleRad);
        float lx = (newPos.X - Center.X) * cos - (newPos.Y - Center.Y) * sin;
        float ly = (newPos.X - Center.X) * sin + (newPos.Y - Center.Y) * cos;

        float hw = Width / 2f;
        float hh = Height / 2f;

        float fx = index switch
        {
            0 or 3 => hw,
            1 or 2 => -hw,
            _ => 0
        };
        float fy = index switch
        {
            0 or 1 => hh,
            2 or 3 => -hh,
            _ => 0
        };

        float minX = MathF.Min(lx, fx);
        float maxX = MathF.Max(lx, fx);
        float minY = MathF.Min(ly, fy);
        float maxY = MathF.Max(ly, fy);

        Width = MathF.Max(1, maxX - minX);
        Height = MathF.Max(1, maxY - minY);

        float cxLocal = (minX + maxX) / 2f;
        float cyLocal = (minY + maxY) / 2f;

        Center = new SKPoint(
            Center.X + cxLocal * MathF.Cos(AllowedAngleRad) - cyLocal * MathF.Sin(AllowedAngleRad),
            Center.Y + cxLocal * MathF.Sin(AllowedAngleRad) + cyLocal * MathF.Cos(AllowedAngleRad)
        );
    }

    public void Reset()
    {
        Width = Height = 0;
        AllowedAngleRad = 0f;
    }
}