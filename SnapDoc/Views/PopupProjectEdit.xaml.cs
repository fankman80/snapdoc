#nullable disable

using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupProjectEdit : Popup<string>
{
    private readonly string _entry;
    public PopupProjectEdit(string entry, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
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
        await CloseAsync("delete");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await CloseAsync("zip");
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        await CloseAsync("folder");
    }
}
