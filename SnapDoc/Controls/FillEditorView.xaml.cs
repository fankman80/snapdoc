#nullable disable
namespace SnapDoc.Controls;

public partial class FillEditorView : ContentView
{
    public event EventHandler StyleChanged;
    private bool _loading;

    private Color fillColor = Colors.Transparent;
    public Color FillColor
    {
        get => fillColor;
        set
        {
            if (fillColor != value)
            {
                fillColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FillOpacity));
                Raise();
            }
        }
    }

    public double FillOpacity
    {
        get => Math.Round(FillColor.Alpha * 100);
        set
        {
            var alpha = (float)Math.Clamp(value / 100.0, 0, 1);
            if (Math.Abs(FillColor.Alpha - alpha) > 0.001f)
                FillColor = FillColor.WithAlpha(alpha);
        }
    }

    private bool isHatchEffect;
    public bool IsHatchEffect
    {
        get => isHatchEffect;
        set { if (isHatchEffect != value) { isHatchEffect = value; OnPropertyChanged(); Raise(); } }
    }

    private float hatchStrokeWitdh;
    public float HatchStrokeWitdh
    {
        get => hatchStrokeWitdh;
        set { if (hatchStrokeWitdh != value) { hatchStrokeWitdh = value; OnPropertyChanged(); Raise(); } }
    }

    private float hatchStrokeSpace;
    public float HatchStrokeSpace
    {
        get => hatchStrokeSpace;
        set { if (hatchStrokeSpace != value) { hatchStrokeSpace = value; OnPropertyChanged(); Raise(); } }
    }

    private float hatchRotation;
    public float HatchRotation
    {
        get => hatchRotation;
        set { if (hatchRotation != value) { hatchRotation = value; OnPropertyChanged(); Raise(); } }
    }

    public FillEditorView()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public void Load(Color color, bool hatch, float hatchWidth, float hatchSpace, float hatchAngle)
    {
        _loading = true;
        FillColor = color;
        IsHatchEffect = hatch;
        HatchStrokeWitdh = hatchWidth;
        HatchStrokeSpace = hatchSpace;
        HatchRotation = hatchAngle;
        _loading = false;
    }

    private void Raise()
    {
        if (!_loading)
            StyleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FlipRotationAngleClick(object sender, EventArgs e) => HatchRotation *= -1;
}
