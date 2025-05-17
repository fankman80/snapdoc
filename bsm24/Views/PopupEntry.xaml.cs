#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupEntry : Popup
{
    public string ReturnValue { get; set; }

    public PopupEntry(string title, string inputTxt = "", string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        text_entry.Text = inputTxt;
    }

    private void OnOkClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = text_entry.Text;
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = null;
        CloseAsync(ReturnValue, cts.Token);
    }
}
