#nullable disable

using DocumentFormat.OpenXml.Presentation;
using Mopups.Pages;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace bsm24.Views;

public partial class PopupColorPicker : PopupPage
{
    TaskCompletionSource<(Color, int)> _taskCompletionSource;
    public Task<(Color, int)> PopupDismissedTask => _taskCompletionSource.Task;
    public (Color, int) ReturnValue { get; set; }
    private int LineWidth { get; set; }
    private Color SelectedColor { get; set; }
    public ObservableCollection<ColorItem> Colors { get; set; }

    public PopupColorPicker(int lineWidth, Color selectedColor, string okText = "Ok")
    {
	    InitializeComponent();
        okButtonText.Text = okText;
        LineWidth = lineWidth;
        SelectedColor = selectedColor;
        Colors = new ObservableCollection<ColorItem>(Settings.ColorData.Select(c => new ColorItem { BackgroundColor = c, StrokeColor = c }));
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        foreach (var colorItem in Colors)
            if (colorItem.BackgroundColor.Equals(SelectedColor))
                colorItem.StrokeColor = Microsoft.Maui.Graphics.Colors.Black;
        sliderText.Text = "Pinselgrösse: " + LineWidth.ToString();
        LineWidthSlider.Value = LineWidth;
        _taskCompletionSource = new TaskCompletionSource<(Color, int)>();
    }


    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ColorItem selectedItem)
        {
            foreach (var item in Colors)
                item.StrokeColor = item.BackgroundColor;
            selectedItem.StrokeColor = Microsoft.Maui.Graphics.Colors.Black;
            SelectedColor = selectedItem.BackgroundColor;
        }
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

public partial class ColorItem : INotifyPropertyChanged
{
    private Color backgroundcolor;
    private Color strokecolor;

    public Color BackgroundColor
    {
        get => backgroundcolor;
        set
        {
            backgroundcolor = value;
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    public Color StrokeColor
    {
        get => strokecolor;
        set
        {
            strokecolor = value;
            OnPropertyChanged(nameof(StrokeColor));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


