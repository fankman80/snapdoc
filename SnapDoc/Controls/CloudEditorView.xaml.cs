#nullable disable
namespace SnapDoc.Controls;

public partial class CloudEditorView : ContentView
{
    public event EventHandler StyleChanged;
    private bool _loading;

    private bool isCloudActive;
    public bool IsCloudActive
    {
        get => isCloudActive;
        set { if (isCloudActive != value) { isCloudActive = value; OnPropertyChanged(); Raise(); } }
    }

    private float cloudRadius = 20;
    public float CloudRadius
    {
        get => cloudRadius;
        set { if (cloudRadius != value) { cloudRadius = value; OnPropertyChanged(); Raise(); } }
    }

    private float cloudInciseDeg = 15;
    public float CloudInciseDeg
    {
        get => cloudInciseDeg;
        set { if (cloudInciseDeg != value) { cloudInciseDeg = value; OnPropertyChanged(); Raise(); } }
    }

    public CloudEditorView()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public void Load(bool isCloud, float radius, float incise)
    {
        _loading = true;
        IsCloudActive = isCloud;
        CloudRadius = radius;
        CloudInciseDeg = incise;
        _loading = false;
    }

    private void Raise()
    {
        if (!_loading)
            StyleChanged?.Invoke(this, EventArgs.Empty);
    }
}
