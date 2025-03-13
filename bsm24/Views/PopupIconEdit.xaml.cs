#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupIconEdit : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }

    public PopupIconEdit(string _iconName, string _iconImage, Point _anchor, double _scale)
    {
        InitializeComponent();
        iconName.Text = _iconName;
        iconImage.Source = _iconImage;
        anchorX.Text = _anchor.X.ToString();
        anchorY.Text = _anchor.Y.ToString();
        iconScale.Value = _scale * 100;
        sliderText.Text = "Voreinstellung Skalierung: " + (_scale * 100).ToString() + "%";
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
        //ReturnValue = text_entry.Text;
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private void OnSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Slider)sender).Value;
        sliderText.Text = "Voreinstellung Skalierung: " + Math.Round(sliderValue, 0).ToString() + "%";
    }
}
