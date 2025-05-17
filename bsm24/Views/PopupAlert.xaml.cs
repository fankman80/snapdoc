#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupAlert : Popup
{
    public PopupAlert(string title, string okText = "Ok")
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
    }

    private void OnOkClicked(object sender, EventArgs e)
    {
        Close();
    }
}