using Mopups.Pages;

namespace SnapDoc.Controls;

public partial class TooltipPopup : PopupPage
{
    public TooltipPopup()
    {
        InitializeComponent();

        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;

        double screenWidth = displayInfo.Width / displayInfo.Density;
        double screenHeight = displayInfo.Height / displayInfo.Density;

        this.WidthRequest = screenWidth;
        this.HeightRequest = screenHeight;
    }

    public void SetTooltipData(Point targetPoint, string text)
    {
        SkiaOverlay.TargetPoint = targetPoint;
        SkiaOverlay.Text = text;
    }
}