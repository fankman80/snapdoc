#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupSlider : PopupPage
{
    TaskCompletionSource<double> _taskCompletionSource;
    public Task<double> PopupDismissedTask => _taskCompletionSource.Task;
    public double ReturnValue { get; set; }
    private readonly double ScaleValue;

    public PopupSlider(double scaleValue, string okText = "Ok")
    {
	InitializeComponent();
        okButtonText.Text = okText;
        ScaleValue = scaleValue;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        sliderText.Text = "Skalierung: " + Math.Round(ScaleValue * 100, 0).ToString() + "%";
        PinSizeSlider.Value = ScaleValue * 100;
        _taskCompletionSource = new TaskCompletionSource<double>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
	ReturnValue = ScaleValue;
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = ScaleValue;
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = PinSizeSlider.Value / 100;
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = ScaleValue;
        await MopupService.Instance.PopAsync();
    }

    private void OnSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Slider)sender).Value;
        sliderText.Text = "Skalierung: " + Math.Round(sliderValue, 0).ToString() + "%";
    }

}
