using SkiaSharp;
namespace SnapDoc.DrawingTool;

public class MeasureSettings
{
    public bool IsMeasureActive { get; set; } = false;

    private float measureFactor = 1f;
    /// <summary>Umrechnungsfaktor Pixel → Einheit (z.B. 0.01 für 1px = 1cm → m).</summary>
    public float MeasureFactor
    {
        get => measureFactor;
        set => measureFactor = (float.IsFinite(value) && value > 0f) ? value : 1f;
    }

    public string MeasureUnit { get; set; } = "px";
    public int MeasureDecimals { get; set; } = 2;
    public float MeasureTextSize { get; set; } = 12f;
    public SKColor MeasureTextColor { get; set; } = SKColors.Black;
    public SKColor MeasureBackgroundColor { get; set; } = SKColors.White.WithAlpha(210);

    public string FormatLength(float px) =>
        $"{(px * MeasureFactor).ToString("N" + MeasureDecimals)} {MeasureUnit}";

    public string FormatArea(float px2) =>
        $"{(px2 * MeasureFactor * MeasureFactor).ToString("N" + MeasureDecimals)} {MeasureUnit}²";
}