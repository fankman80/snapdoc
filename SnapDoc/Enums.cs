namespace SnapDoc
{
    public enum DrawMode
    {
        None,
        Free,
        Poly,
        Rect,
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
}
