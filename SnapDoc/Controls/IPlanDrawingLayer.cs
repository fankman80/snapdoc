using SkiaSharp;
namespace SnapDoc.Controls;

/// <summary>Zeichenebene, die TileImageView in Plan-Koordinaten rendert und mit Touch versorgt.</summary>
public interface IPlanDrawingLayer
{
    bool IsActive { get; }
    SKPoint? ActiveHandle { get; }
    event EventHandler Invalidated;
    void SetViewTransform(float scale, float rotationDeg);
    void Draw(SKCanvas canvas);          // Canvas steht bereits in Plan-Koordinaten
    void DrawWithoutHandles(SKCanvas canvas);
    bool OnPressed(SKPoint plan);        // true = Geste gehört der Zeichnung
    void OnMoved(SKPoint plan);
    void OnReleased();
    void OnTap(SKPoint plan);            // kurzer Tap ohne Verschieben (z. B. Poly-Punkt setzen)
    void OnCancel();                     // z. B. zweiter Finger kommt dazu
}