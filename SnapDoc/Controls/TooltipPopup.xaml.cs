using Mopups.Pages;

namespace SnapDoc.Controls;

public partial class TooltipPopup : PopupPage
{
    public TooltipPopup()
    {
        InitializeComponent();

        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;

        // Wir berechnen die Breite und Höhe in MAUI-Einheiten
        double screenWidth = displayInfo.Width / displayInfo.Density;
        double screenHeight = displayInfo.Height / displayInfo.Density;

        // Wir setzen die Anforderung für die Größe des Popups
        this.WidthRequest = screenWidth;
        this.HeightRequest = screenHeight;

        // Falls das Toolkit die Size-Property doch unterstützt (als Read-Only), 
        // ist der Weg über Width/HeightRequest immer der sicherere in MAUI.
    }

    public void SetTooltipData(Point targetPoint, string text)
    {
        // "SkiaOverlay" ist der x:Name, den wir im XAML des Popups vergeben haben
        SkiaOverlay.TargetPoint = targetPoint;
        SkiaOverlay.Text = text;
    }
}