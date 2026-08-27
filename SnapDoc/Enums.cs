namespace SnapDoc
{
    public enum DrawMode
    {
        None,
        Free,
        Poly,
        Rect,
        Oval,
        Arrow
    }

    public enum RectangleTextAlignment
    {
        Left,
        Center,
        Right
    }

    [Flags]
    public enum RectangleTextStyle
    {
        Normal = 0,
        Bold = 1 << 0,
        Italic = 1 << 1
    }

    public enum DualPopupResult
    {
        Cancel,
        Ok
    }

    public enum CloudPickerMode
    {
        SelectFolder,
        SelectJsonFile
    }

    public enum RemoteChangeType
    {
        ProjectDetailsUpdated,
        PlanListUpdated,
        PinsUpdated
    }
}
