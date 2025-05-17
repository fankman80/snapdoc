#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupProjectEdit : Popup
{
    public string ReturnValue { get; set; }

    public PopupProjectEdit(string entry, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        text_entry.Text = entry;
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

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = "delete";
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = "zip";
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnOpenFolderClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = "folder";
        CloseAsync(ReturnValue, cts.Token);
    }
}
