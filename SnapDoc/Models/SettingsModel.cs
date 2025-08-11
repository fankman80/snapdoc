namespace SnapDoc.Models;

internal class SettingsModel
{
    public double PinMinScaleLimit { get; set; }
    public double PinMaxScaleLimit { get; set; }
    public int MapIconSize { get; set; }
    public int MapIcon { get; set; }
    public int IconSortCrit { get; set; }
    public int PinSortCrit { get; set; }
    public int IconCategory { get; set; }
    public bool IsPlanRotateLocked { get; set; }
    public int MaxPdfPixelCount { get; set; }
    public int SelectedColorTheme { get; set; }
    public int SelectedAppTheme { get; set; }
    public bool IsFotoCompressed { get; set; }
    public int FotoCompressValue { get; set; }
    public string? IconGalleryMode { get; set; }
    public List<string>? ColorList { get; set; }
}
