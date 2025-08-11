namespace SnapDoc;

public partial class BusyOverlayView : ContentView
{
    public BusyOverlayView()
    {
        InitializeComponent();
    }

    // Öffentliche Eigenschaft, um die Sichtbarkeit des Overlays zu steuern
    public bool IsOverlayVisible
    {
        get => OverlayGrid.IsVisible;
        set => OverlayGrid.IsVisible = value;
    }

    // Öffentliche Eigenschaft, um den ActivityIndicator zu steuern
    public bool IsActivityRunning
    {
        get => activityIndicator.IsRunning;
        set => activityIndicator.IsRunning = value;
    }

    // Öffentliche Eigenschaft, um den Text des BusyIndicators zu setzen
    public string BusyMessage
    {
        get => busyText.Text;
        set => busyText.Text = value;
    }
}