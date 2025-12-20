#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupEntry : Popup<string>
{
    public PopupEntry(string title, string inputTxt = "", string okText = null, string cancelText = null)
    {
        InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        text_entry.Text = inputTxt;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(text_entry.Text);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }
}