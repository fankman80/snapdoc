#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupAlert : Popup
{
    public PopupAlert(string title, string okText = null)
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText ?? AppResources.ok;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync();
    }
}