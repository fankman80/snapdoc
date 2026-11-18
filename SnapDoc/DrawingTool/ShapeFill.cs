using SkiaSharp;

namespace SnapDoc.DrawingTool;

public static class ShapeFill
{
    public static void Draw(
        SKCanvas canvas,
        SKPath path,
        SKColor fillColor,
        bool isHatch,
        float hatchStrokeWidth,
        float hatchStrokeSpace,
        float hatchRotation,
        float density,
        float angleOffset = 0f)
    {
        if (canvas == null || path == null || path.IsEmpty || fillColor.Alpha == 0)
            return;

        using var hatchEffect = isHatch
            ? CreateHatchEffect(hatchStrokeWidth, hatchStrokeSpace, hatchRotation + angleOffset, density)
            : null;

        using var fillPaint = new SKPaint
        {
            Color = fillColor,
            Style = SKPaintStyle.Fill,
            PathEffect = hatchEffect,
            IsAntialias = true
        };

        // Vollfläche: Pfad direkt zeichnen (exakt)
        if (hatchEffect == null)
        {
            canvas.DrawPath(path, fillPaint);
            return;
        }

        // Schraffur: an die Kontur clippen und vergrössertes Rechteck füllen
        float spacing = GetSpacing(hatchStrokeWidth, hatchStrokeSpace, density);

        canvas.Save();
        canvas.ClipPath(path, SKClipOperation.Intersect, true);

        var bounds = path.TightBounds;
        bounds.Inflate(spacing, spacing);
        canvas.DrawRect(bounds, fillPaint);

        canvas.Restore();
    }

    /// <summary>
    /// Erzeugt den Schraffur-Effekt. Der Aufrufer ist für Dispose verantwortlich.
    /// </summary>
    public static SKPathEffect CreateHatchEffect(
        float hatchStrokeWidth,
        float hatchStrokeSpace,
        float hatchRotation,
        float density)
    {
        float lineWidth = MathF.Max(0.5f, hatchStrokeWidth * density);
        float spacing = GetSpacing(hatchStrokeWidth, hatchStrokeSpace, density);

        var scale = SKMatrix.CreateScale(spacing, spacing);
        var rotation = SKMatrix.CreateRotationDegrees(hatchRotation);
        var matrix = SKMatrix.Concat(scale, rotation);

        return SKPathEffect.Create2DLine(lineWidth, matrix);
    }

    private static float GetSpacing(float hatchStrokeWidth, float hatchStrokeSpace, float density)
    {
        float lineWidth = MathF.Max(0.5f, hatchStrokeWidth * density);
        // Mindestabstand verhindert extrem viele Linien bei Spacing ≈ 0
        return MathF.Max(2f, hatchStrokeSpace * density + lineWidth);
    }
}