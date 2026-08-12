using CommunityToolkit.Mvvm.ComponentModel;

namespace SnapDoc.ViewModels;

public partial class TransformViewModel : ObservableObject
{
    #region Properties
    private bool isPanningEnabled = true;
    public bool IsPanningEnabled
    {
        get => isPanningEnabled;
        set => SetProperty(ref isPanningEnabled, value);
    }

    private bool isPinchingEnabled = true;
    public bool IsPinchingEnabled
    {
        get => isPinchingEnabled;
        set => SetProperty(ref isPinchingEnabled, value);
    }

    private bool isRotatingEnabled = true;
    public bool IsRotatingEnabled
    {
        get => isRotatingEnabled;
        set => SetProperty(ref isRotatingEnabled, value);
    }

    private double anchorX = 0.5;
    public double AnchorX
    {
        get => anchorX;
        set => SetProperty(ref anchorX, value);
    }

    private double anchorY = 0.5;
    public double AnchorY
    {
        get => anchorY;
        set => SetProperty(ref anchorY, value);
    }

    private double rotation = 0;
    public double Rotation
    {
        get => rotation;
        set => SetProperty(ref rotation, value);
    }

    private double scale = 1;
    public double Scale
    {
        get => scale;
        set => SetProperty(ref scale, value);
    }

    private double scaleX = 1;
    public double ScaleX
    {
        get => scaleX;
        set => SetProperty(ref scaleX, value);
    }

    private double scaleY = 1;
    public double ScaleY
    {
        get => scaleY;
        set => SetProperty(ref scaleY, value);
    }

    private double translationX = 0;
    public double TranslationX
    {
        get => translationX;
        set => SetProperty(ref translationX, value);
    }

    private double translationY = 0;
    public double TranslationY
    {
        get => translationY;
        set => SetProperty(ref translationY, value);
    }
    #endregion

    public void ResetTransforms()
    {
        AnchorX = 0.5;
        AnchorY = 0.5;
        Rotation = 0;
        Scale = 1;
        ScaleX = 1;
        ScaleY = 1;
        TranslationX = 0;
        TranslationY = 0;
    }
}