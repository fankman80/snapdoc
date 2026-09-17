#nullable disable
using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupProjectEdit : Popup<string>
{
    public PopupProjectEdit(string cancelText = null)
    {
        InitializeComponent();
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        UploadButton.IsVisible = SettingsService.Instance.IsCloudLoggedIn;
    }
    
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try { await CloseAsync("Zip"); }
        catch (InvalidOperationException) { }
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        try { await CloseAsync("Folder"); }
        catch (InvalidOperationException) { }
    }

    private async void OnCloudPickerClicked(object sender, EventArgs e)
    {
        try { await CloseAsync("Upload"); }
        catch (InvalidOperationException) { }
    }
}
