namespace SnapDoc;

public class CombinedDrawable : IDrawable
{
    public required InteractivePolylineDrawable PolyDrawable { get; set; }
    public required InteractiveFreehandDrawable FreeDrawable { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // zuerst Freihand zeichnen
        FreeDrawable?.Draw(canvas, dirtyRect);

        // dann Polylinien zeichnen
        PolyDrawable?.Draw(canvas, dirtyRect);
    }

    public void Reset()
    {
        PolyDrawable?.Reset();
        FreeDrawable?.Reset();
    }
}