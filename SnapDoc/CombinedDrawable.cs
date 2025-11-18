using SkiaSharp;

namespace SnapDoc;

public class CombinedDrawable
{
    public required InteractivePolylineDrawable PolyDrawable { get; set; }
    public required InteractiveFreehandDrawable FreeDrawable { get; set; }

    public void Draw(SKCanvas canvas)
    {
        // zuerst Freihand zeichnen
        FreeDrawable?.Draw(canvas);

        // dann Polylinien zeichnen
        PolyDrawable?.Draw(canvas);
    }

    public void Reset()
    {
        PolyDrawable?.Reset();
        FreeDrawable?.Reset();
    }
}
