using SkiaSharp;

namespace SnapDoc;

public class CombinedDrawable
{
    public required InteractivePolylineDrawable PolyDrawable { get; set; }
    public required InteractiveFreehandDrawable FreeDrawable { get; set; }
    public InteractiveRectangleDrawable? RectangleDrawable { get; set; }

    public void Draw(SKCanvas canvas)
    {
        if (FreeDrawable?.HasContent == true)
            FreeDrawable.Draw(canvas);

        if (PolyDrawable?.HasContent == true)
            PolyDrawable.Draw(canvas);

        if (RectangleDrawable?.HasContent == true)
            RectangleDrawable.Draw(canvas);
    }

    public void Reset()
    {
        PolyDrawable?.Reset();
        FreeDrawable?.Reset();
        RectangleDrawable?.Reset();
    }
}
