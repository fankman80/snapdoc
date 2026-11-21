#nullable enable

namespace SnapDoc.Reporting;

public static class ReportGeometry
{
    public static long MmToEmu(double millimeters) => Convert.ToInt64(millimeters * 36000);

    /// <summary>Skaliert proportional in die Zielgrösse. Eine Zielachse von 0 gilt als unbegrenzt.</summary>
    public static SizeF ScaleToFit(SizeF original, SizeF maxTarget)
    {
        if (original.Width <= 0 || original.Height <= 0)
            return maxTarget;

        double widthScale = maxTarget.Width != 0 ? (double)maxTarget.Width / original.Width : double.PositiveInfinity;
        double heightScale = maxTarget.Height != 0 ? (double)maxTarget.Height / original.Height : double.PositiveInfinity;
        double scale = Math.Min(widthScale, heightScale);

        if (double.IsInfinity(scale))
            return original;

        return new SizeF((int)(original.Width * scale), (int)(original.Height * scale));
    }

    public static PointF PivotRecalc(PointF pos, float angle, PointF anchor, SizeF scaledPinSize, SizeF scaledPlanSize)
    {
        float w = scaledPinSize.Width;
        float h = scaledPinSize.Height;

        float px = anchor.X * w;
        float py = anchor.Y * h;
        float cx = w / 2;
        float cy = h / 2;

        float dx = px - cx;
        float dy = py - cy;

        float rad = angle * (float)Math.PI / 180;
        float dxRot = dx * MathF.Cos(rad) - dy * MathF.Sin(rad);
        float dyRot = dx * MathF.Sin(rad) + dy * MathF.Cos(rad);

        float finalX = pos.X * scaledPlanSize.Width - cx - dxRot + cx - (w / 2);
        float finalY = pos.Y * scaledPlanSize.Height - cy - dyRot + cy - (h / 2);
        return new PointF(finalX, finalY);
    }
}
