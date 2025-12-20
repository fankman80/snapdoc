#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupProjectEdit : Popup<string>
{
    private readonly string _entry;
    public PopupProjectEdit(string entry, string okText = null, string cancelText = null)
    {
        InitializeComponent();
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        text_entry.Text = entry;

        _entry = entry; 
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(_entry!=text_entry.Text?text_entry.Text:null);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        await CloseAsync("Delete");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await CloseAsync("Zip");
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        await CloseAsync("Folder");
    }
}
