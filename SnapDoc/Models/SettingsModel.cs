namespace SnapDoc.Models;

internal class SettingsModel
{
    public double PinMinScaleLimit { get; set; }
    public double PinMaxScaleLimit { get; set; }
    public int MapIconSize { get; set; }
    public int MapIcon { get; set; }
    public int PinPlaceMode { get; set; }
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
    public int MaxPdfImageSizeW { get; set; }
    public int MaxPdfImageSizeH { get; set; }
    public int ThumbSize { get; set; }
    public int PlanPreviewSize { get; set; }
    public int IconPreviewSize { get; set; }
    public int PinTextPadding { get; set; }
    public int PinTextDistance { get; set; }
    public double DefaultPinZoom { get; set; }
    public List<string>? ColorList { get; set; }
    public List<string>? MapIcons { get; set; }
    public List<string>? IconSortCrits { get; set; }
    public List<string>? PinSortCrits { get; set; }
    public List<PriorityItem>? PriorityItems { get; set; }
}
