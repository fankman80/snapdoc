#nullable disable
using static SnapDoc.Helper;

namespace SnapDoc;

// =====================================================================
//  Rueckgabetypen der Popups (reine DTOs)
// =====================================================================

public class ColorPickerReturn(string colorHex, byte fillOpacity)
{
    public string ColorHex { get; set; } = colorHex;
    public byte FillOpacity { get; set; } = fillOpacity;
}

public class PopupStyleReturn(
    string borderColorHex, string fillColorHex, string textColorHex, int width,
    string strokeStyle, bool isHatchEffect, float hatchStrokeWitdh, float hatchStrokeSpace,
    float hatchRotation, float cloudRadius, float cloudInciseDeg)
{
    public string BorderColorHex { get; set; } = borderColorHex;
    public string FillColorHex { get; set; } = fillColorHex;
    public string TextColorHex { get; set; } = textColorHex;
    public int PenWidth { get; set; } = width;
    public string StrokeStyle { get; set; } = strokeStyle;
    public bool IsHatchEffect { get; set; } = isHatchEffect;
    public float HatchStrokeWitdh { get; set; } = hatchStrokeWitdh;
    public float HatchStrokeSpace { get; set; } = hatchStrokeSpace;
    public float HatchRotation { get; set; } = hatchRotation;
    public float CloudRadius { get; set; } = cloudRadius;
    public float CloudInciseDeg { get; set; } = cloudInciseDeg;
}

public class PlanSelectorReturn(string planTarget, bool isPinCopy)
{
    public string PlanTarget { get; set; } = planTarget;
    public bool IsPinCopy { get; set; } = isPinCopy;
}

public class PlanEditReturn(
    string nameEntry, string descEntry, bool allowExport,
    int planRotate, string planColor, bool? lockAction)
{
    public string NameEntry { get; set; } = nameEntry;
    public string DescEntry { get; set; } = descEntry;
    public bool AllowExport { get; set; } = allowExport;
    public int PlanRotate { get; set; } = planRotate;
    public string PlanColor { get; set; } = planColor;
    public bool? LockAction { get; set; } = lockAction;
}

public class TextEditReturn(
    float fontSize, RectangleTextAlignment alignment, RectangleTextStyle style,
    bool autoSize, string inputTxt, int textPadding)
{
    public float FontSize { get; set; } = fontSize;
    public RectangleTextAlignment Alignment { get; set; } = alignment;
    public RectangleTextStyle Style { get; set; } = style;
    public bool AutoSize { get; set; } = autoSize;
    public string InputTxt { get; set; } = inputTxt;
    public int TextPadding { get; set; } = textPadding;
}
