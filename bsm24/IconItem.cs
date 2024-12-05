namespace bsm24;

public class IconItem(string fileName, string displayName, Point anchorPoint, Size iconSize, bool isRotationLocked)
{
    public string FileName { get; set; } = fileName;
    public string DisplayName { get; set; } = displayName;
    public Point AnchorPoint { get; set; } = anchorPoint;
    public Size IconSize { get; set; } = iconSize;
    public bool IsRotationLocked { get; set; } = isRotationLocked;
    public bool IsListMode { get; set; } = true;
}