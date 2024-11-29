#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupDualResponse : PopupPage
{

TaskCompletionSource<string> _taskCompletionSource;
public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
public string ReturnValue { get; set; }

    public PopupDualResponse(string title, string okText = "Ok", string cancelText = "Abbrechen")
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<string>();
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
        ReturnValue = "Ok";
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }
}