#nullable disable
using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupProjectEdit : Popup<string>
{
    private readonly string _entry;

    public PopupProjectEdit(string entry, bool isActive = false, string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        text_entry.Text = entry;

        _entry = entry;

        UploadButton.IsVisible = SettingsService.Instance.IsCloudLoggedIn && isActive;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(_entry != text_entry.Text ? text_entry.Text : null); }
        catch (InvalidOperationException) { }
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