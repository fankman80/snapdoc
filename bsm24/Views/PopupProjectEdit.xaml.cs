#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupProjectEdit : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }

    public PopupProjectEdit(string entry, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        text_entry.Text = entry;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<string>();

#if WINDOWS
        openFolderBtn.IsVisible = true;
# endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = text_entry.Text;
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        ReturnValue = "delete";
        await MopupService.Instance.PopAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        ReturnValue = "zip";
        await MopupService.Instance.PopAsync();
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        ReturnValue = "folder";
        await MopupService.Instance.PopAsync();
    }
}
