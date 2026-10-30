using SkiaSharp;
using System.Globalization;
namespace SnapDoc.DrawingTool;

public class MeasureSettings
{
    private float measureFactor = 1f;
    private static readonly NumberFormatInfo MeasureFormat = new()
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = "'",
        NumberGroupSizes = [3]
    };
    public float ViewScale { get; set; } = 1f;
    public bool IsMeasureActive { get; set; } = false;

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
    public string FormatLength(float px) => $"{(px * MeasureFactor).ToString("N" + MeasureDecimals, MeasureFormat)} {MeasureUnit}";
    public string FormatArea(float px2) => $"{(px2 * MeasureFactor * MeasureFactor).ToString("N" + MeasureDecimals, MeasureFormat)} {MeasureUnit}²";
}