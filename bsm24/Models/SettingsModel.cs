namespace bsm24.Models;

internal class SettingsModel
{
    public double PinScaleLimit { get; set; }
    public bool IsPlanRotateLocked { get; set; }
    public int PdfQuality { get; set; }
    public string? SelectedTheme { get; set; }
    public string? SelectedDarkMode { get; set; }
}
