using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupShapeSelect : Popup<object>
{
    public PopupShapeSelect()
    {
        InitializeComponent();
    }

    private async void RectBtnClicked(object sender, EventArgs e)
    {
        await CloseAsync(0);
    }

    private async void OvalBtnClicked(object sender, EventArgs e)
    {
        await CloseAsync(1);
    }

    private async void PolyBtnClicked(object sender, EventArgs e)
    {
        await CloseAsync(2);
    }

    private async void ArrowBtnClicked(object sender, EventArgs e)
    {
        await CloseAsync(3);
    }

    private async void FreeBtnClicked(object sender, EventArgs e)
    {
        await CloseAsync(4);
    }
}