namespace bsm24.Models;

internal class SettingsModel
{
    public double PinMinScaleLimit { get; set; }
    public double PinMaxScaleLimit { get; set; }
    public bool IsPlanRotateLocked { get; set; }
    public int PdfQuality { get; set; }
    public string? SelectedTheme { get; set; }
    public string? SelectedDarkMode { get; set; }
}
