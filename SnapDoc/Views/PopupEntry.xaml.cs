#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupEntry : Popup<string>
{
    public PopupEntry(string desc, string header = null, string title = null, string input = null, string okText = null, string cancelText = null)
    {
        InitializeComponent();

        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        headerText.IsVisible = header != null;
        headerText.Text = header;
        titleText.Text = desc;
        textEntry.Text = input;
        textEntry.Title = title;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(textEntry.Text);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }
}