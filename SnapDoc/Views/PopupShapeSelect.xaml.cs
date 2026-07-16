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
        try { await CloseAsync(0); }
        catch (InvalidOperationException) { }
    }

    private async void OvalBtnClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(1); }
        catch (InvalidOperationException) { }
    }

    private async void PolyBtnClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(2); }
        catch (InvalidOperationException) { }
    }

    private async void ArrowBtnClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(3); }
        catch (InvalidOperationException) { }
    }

    private async void FreeBtnClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(4); }
        catch (InvalidOperationException) { }
    }

    private async void TxtBtnClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(5); }
        catch (InvalidOperationException) { }
    }
}