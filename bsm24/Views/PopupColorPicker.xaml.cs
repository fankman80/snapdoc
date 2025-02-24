#nullable disable

using Mopups.Pages;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace bsm24.Views;

public partial class PopupColorPicker : PopupPage
{
    TaskCompletionSource<(Color, int)> _taskCompletionSource;
    public ObservableCollection<ColorItem> Colors { get; set; }
    public Task<(Color, int)> PopupDismissedTask => _taskCompletionSource.Task;
    public (Color, int) ReturnValue { get; set; }
    private Color borderColor = Microsoft.Maui.Graphics.Colors.Black;
    private readonly int _lineWidth;
    private Color _selectedColor;

    public PopupColorPicker(int lineWidth, Color selectedColor, string okText = "Ok")
    {
	    InitializeComponent();
        okButtonText.Text = okText;
        _lineWidth = lineWidth;
        _selectedColor = selectedColor;
        Colors = [.. Settings.ColorData.Select(c => new ColorItem { BackgroundColor = c, StrokeColor = popupBorder.BackgroundColor })];
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var currentTheme = Application.Current.RequestedTheme;
        if (currentTheme == AppTheme.Light)
            borderColor = Microsoft.Maui.Graphics.Colors.Black;
        else if (currentTheme == AppTheme.Dark)
            borderColor = Microsoft.Maui.Graphics.Colors.White;

        foreach (var colorItem in Colors)
            if (colorItem.BackgroundColor.Equals(_selectedColor))
                colorItem.StrokeColor = borderColor;

        sliderText.Text = "Pinselgrösse: " + _lineWidth.ToString();
        LineWidthSlider.Value = _lineWidth;
        _taskCompletionSource = new TaskCompletionSource<(Color, int)>();
    }


    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ColorItem selectedItem)
        {
            foreach (var item in Colors)
                item.StrokeColor = popupBorder.BackgroundColor;
            selectedItem.StrokeColor = borderColor;
            _selectedColor = selectedItem.BackgroundColor;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult((_selectedColor, (int)LineWidthSlider.Value));
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = (_selectedColor, (int)LineWidthSlider.Value);
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = (_selectedColor, (int)LineWidthSlider.Value);
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = (_selectedColor, (int)LineWidthSlider.Value);
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


