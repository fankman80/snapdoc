#nullable disable

using DocumentFormat.OpenXml.Drawing.Diagrams;
using Mopups.Pages;
using Mopups.Services;
using System.Collections.ObjectModel;

namespace bsm24.Views;

public partial class PopupColorPicker : PopupPage
{
    TaskCompletionSource<(Color, int)> _taskCompletionSource;
    public Task<(Color, int)> PopupDismissedTask => _taskCompletionSource.Task;
    public (Color, int) ReturnValue { get; set; }
    private int LineWidth { get; set; }
    private Color SelectedColor { get; set; }
    public ObservableCollection<Color> Colors { get; set; }

    public PopupColorPicker(int lineWidth, Color selectedColor, string okText = "Ok")
    {
	InitializeComponent();
        okButtonText.Text = okText;
        LineWidth = lineWidth;
        SelectedColor = selectedColor;
        Colors = new ObservableCollection<Color>(Settings.ColorData);
        BindingContext = this;
    }

    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is Border border)
            SelectedColor = border.BackgroundColor;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        sliderText.Text = "Pinselgrösse: " + LineWidth.ToString();
        LineWidthSlider.Value = LineWidth;
        _taskCompletionSource = new TaskCompletionSource<(Color, int)>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult((SelectedColor, (int)LineWidthSlider.Value));
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = (SelectedColor, (int)LineWidthSlider.Value);
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = (SelectedColor, (int)LineWidthSlider.Value);
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = (SelectedColor, (int)LineWidthSlider.Value);
        await MopupService.Instance.PopAsync();
    }

    private void OnSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Slider)sender).Value;
        sliderText.Text = "Pinselgrösse: " + ((int)sliderValue).ToString();
    }

}
