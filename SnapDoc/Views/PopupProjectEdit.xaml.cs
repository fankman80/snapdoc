#nullable disable
using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupProjectEdit : Popup<string>
{
    public PopupProjectEdit(bool isActive = false, string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        UploadButton.IsVisible = SettingsService.Instance.IsCloudLoggedIn && isActive;
    }
    
    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        try { await CloseAsync("Delete"); }
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
