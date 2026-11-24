#nullable disable
using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupMapPreview : Popup<bool>
{
    public PopupMapPreview(byte[] imageBytes)
    {
        InitializeComponent();
        PreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(imageBytes));
    }

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(true); }
        catch (InvalidOperationException) { }
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(false); }
        catch (InvalidOperationException) { }
    }
}