using MR.Gestures;
using SnapDoc.Services;

namespace SnapDoc.ViewModels;

public partial class TransformViewModel : CustomEventArgsViewModel
{
    #region Properties
    public bool IsPanningEnabled = true;
    public bool IsPinchingEnabled = true;
    public bool IsRotatingEnabled = true;
    protected double anchorX = 0.5;

    public double AnchorX
    {
        get { return anchorX; }
        set { SetProperty(ref anchorX, value); }
    }

    protected double anchorY = 0.5;
    public double AnchorY
    {
        get { return anchorY; }
        set { SetProperty(ref anchorY, value); }
    }

    protected double rotation = 0;
    public double Rotation
    {
        get { return rotation; }
        set { SetProperty(ref rotation, value); }
    }

    protected double scale = 1;
    public double Scale
    {
        get { return scale; }
        set { SetProperty(ref scale, value); }
    }

    protected double scaleX = 1;
    public double ScaleX
    {
        get { return scaleX; }
        set { SetProperty(ref scaleX, value); }
    }

    protected double scaleY = 1;
    public double ScaleY
    {
        get { return scaleY; }
        set { SetProperty(ref scaleY, value); }
    }

    protected double translationX = 0;
    public double TranslationX
    {
        get { return translationX; }
        set { SetProperty(ref translationX, value); }
    }

    protected double translationY = 0;
    public double TranslationY
    {
        get { return translationY; }
        set { SetProperty(ref translationY, value); }
    }
    #endregion

    protected override void OnPanning(PanEventArgs e)
    {
        base.OnPanning(e);

        if (!IsPanningEnabled) return;  // panning nur, wenn panning aktiviert ist
        TranslationX += e.DeltaDistance.X;
        TranslationY += e.DeltaDistance.Y;
    }

    protected override void OnPinching(PinchEventArgs e)
    {
        base.OnPinching(e);

        if (!IsPinchingEnabled) return;
        var newScale = Scale * e.DeltaScale;
        Scale = Math.Min(10, Math.Max(0.05, newScale));
        var newScaleX = ScaleX * e.DeltaScaleX;
        ScaleX = Math.Min(10, Math.Max(0.05, newScaleX));
        var newScaleY = ScaleY * e.DeltaScaleY;
        ScaleY = Math.Min(10, Math.Max(0.05, newScaleY));
    }

    protected override void OnRotating(RotateEventArgs e)
    {
        base.OnRotating(e);

        if (!IsRotatingEnabled || SettingsService.Instance.IsPlanRotateLocked) return;   // rotating nur, wenn rotating aktiviert is

        Rotation += e.DeltaAngle;
    }
}
