#nullable disable

using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupEntry : Popup<string>
{
    public PopupEntry(string title, string inputTxt = "", string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
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