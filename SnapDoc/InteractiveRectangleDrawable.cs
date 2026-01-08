using SkiaSharp;

namespace SnapDoc;

public class InteractiveRectangleDrawable
{
    public SKPoint Start { get; private set; }
    public SKPoint End { get; private set; }
    public bool HasContent => Start != End;
    public float HandleRadius { get; set; } = 15f;
    public float PointRadius { get; set; } = 8f;
    public float LineThickness { get; set; } = 3f;
    public bool DisplayHandles { get; set; } = true;
    public SKColor FillColor { get; set; } = SKColors.Blue.WithAlpha(100);
    public SKColor LineColor { get; set; } = SKColors.Blue;
    public SKColor PointColor { get; set; } = SKColors.Gray.WithAlpha(160);

    public SKPoint[] Points
    {
        get
        {
            return
            [
                new SKPoint(Start.X, Start.Y), // TL
                new SKPoint(End.X, Start.Y),   // TR
                new SKPoint(End.X, End.Y),     // BR
                new SKPoint(Start.X, End.Y)    // BL
            ];
        }
    }

    public void SetFromDrag(SKPoint start, SKPoint end)
    {
        Start = start;
        End = end;
    }

    public void Draw(SKCanvas canvas)
    {
        if (!HasContent)
            return;

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

        if (DisplayHandles)
        {
            using var handlePaint = new SKPaint
            {
                Color = PointColor,
                Style = SKPaintStyle.StrokeAndFill,
                StrokeWidth = 2,
                IsAntialias = true
            };

            foreach (var p in pts)
                canvas.DrawCircle(p, PointRadius, handlePaint);
        }
    }

    public int? FindPointIndex(float x, float y)
    {
        var pts = Points;
        var p = new SKPoint(x, y);

        for (int i = 0; i < pts.Length; i++)
        {
            if (SKPoint.Distance(p, pts[i]) <= HandleRadius)
                return i;
        }

        return null;
    }

    public void MovePoint(int index, SKPoint newPos)
    {
        float left = Start.X;
        float right = End.X;
        float top = Start.Y;
        float bottom = End.Y;

        switch (index)
        {
            case 0: // TL
                left = newPos.X;
                top = newPos.Y;
                break;
            case 1: // TR
                right = newPos.X;
                top = newPos.Y;
                break;
            case 2: // BR
                right = newPos.X;
                bottom = newPos.Y;
                break;
            case 3: // BL
                left = newPos.X;
                bottom = newPos.Y;
                break;
        }

        Start = new SKPoint(left, top);
        End = new SKPoint(right, bottom);
    }

    public void Reset()
    {
        Start = End = default;
    }
}
