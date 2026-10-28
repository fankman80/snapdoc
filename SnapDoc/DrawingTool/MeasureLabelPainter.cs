using SkiaSharp;
namespace SnapDoc.DrawingTool;

internal partial class MeasureLabelPainter : IDisposable
{
    private readonly SKFont font;
    private readonly SKPaint textPaint;
    private readonly SKPaint bgPaint;
    private readonly float pad;
    private readonly float textHeight;
    private readonly float baselineOffset;

    public float LabelHeight => textHeight + pad;

    public MeasureLabelPainter(float textSize, SKColor textColor, SKColor bgColor)
    {
        font = new SKFont(SKTypeface.Default, textSize);
        textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        bgPaint = new SKPaint { Color = bgColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        var m = font.Metrics;
        textHeight = m.Descent - m.Ascent;
        baselineOffset = -(m.Ascent + m.Descent) / 2f;
        pad = textSize * 0.35f;
    }

    /// <summary>Zeichnet ein Label (Pill-Hintergrund) zentriert an pos, gedreht um angleDeg.
    /// Gibt false zurück, wenn das Label breiter als maxWidth wäre.</summary>
    public bool Draw(SKCanvas canvas, string text, SKPoint pos, float angleDeg, float maxWidth = float.MaxValue)
    {
        float w = font.MeasureText(text);
        if (w + 2 * pad > maxWidth) return false;

        canvas.Save();
        canvas.Translate(pos.X, pos.Y);
        canvas.RotateDegrees(angleDeg);

        float halfH = textHeight / 2f + pad * 0.4f;
        var r = new SKRect(-w / 2f - pad, -halfH, w / 2f + pad, halfH);
        canvas.DrawRoundRect(r, halfH, halfH, bgPaint);
        canvas.DrawText(text, 0, baselineOffset, SKTextAlign.Center, font, textPaint);

        canvas.Restore();
        return true;
    }

    /// <summary>Label in der Mitte der Strecke a→b, parallel zur Strecke, immer lesbar.</summary>
    public void DrawSegment(SKCanvas canvas, SKPoint a, SKPoint b, string text)
    {
        float len = SKPoint.Distance(a, b);
        if (len < 1f) return;
        var mid = new SKPoint((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
        float angle = MathF.Atan2(b.Y - a.Y, b.X - a.X) * 180f / MathF.PI;
        Draw(canvas, text, mid, Upright(angle), len);
    }

    public static float Upright(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        if (deg < -180f) deg += 360f;
        if (deg > 90f) deg -= 180f;
        else if (deg < -90f) deg += 180f;
        return deg;
    }

    public static float PolygonArea(IReadOnlyList<SKPoint> pts)
    {
        double sum = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            sum += (double)p.X * q.Y - (double)q.X * p.Y;
        }
        return (float)Math.Abs(sum / 2.0);
    }

    public static SKPoint Centroid(IReadOnlyList<SKPoint> pts)
    {
        double a = 0, cx = 0, cy = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Count];
            double cross = (double)p.X * q.Y - (double)q.X * p.Y;
            a += cross;
            cx += (p.X + q.X) * cross;
            cy += (p.Y + q.Y) * cross;
        }
        if (Math.Abs(a) < 1e-6)
            return new SKPoint(pts.Average(p => p.X), pts.Average(p => p.Y));
        a *= 0.5;
        return new SKPoint((float)(cx / (6 * a)), (float)(cy / (6 * a)));
    }

    public void Dispose()
    {
        font.Dispose();
        textPaint.Dispose();
        bgPaint.Dispose();
    }
}