#nullable disable

using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupAlert : Popup
{
    public PopupAlert(string title, string okText = "Ok")
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }
}