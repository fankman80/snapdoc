#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupPlanEdit : PopupPage
{
    TaskCompletionSource<(string, string)> _taskCompletionSource;
    public Task<(string, string)> PopupDismissedTask => _taskCompletionSource.Task;
    public (string, string) ReturnValue { get; set; }

    public PopupPlanEdit(string name, string desc, bool gray, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        name_entry.Text = name;
        desc_entry.Text = desc;

        if (gray)
            grayscaleButtonText.Text = "Farben hinzufügen";
        else
            grayscaleButtonText.Text = "Farben entfernen";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<(string, string)>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = (null, null);
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = (name_entry.Text, desc_entry.Text);
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = (null, null);
        await MopupService.Instance.PopAsync();
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        ReturnValue = ("delete", "delete");
        await MopupService.Instance.PopAsync();
    }
    private async void OnGrayscaleClicked(object sender, EventArgs e)
    {
        ReturnValue = ("grayscale", "grayscale");
        await MopupService.Instance.PopAsync();
    }
}
