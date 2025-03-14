#nullable disable

using Mopups.Pages;
using Mopups.Services;
using SkiaSharp;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace bsm24.Views;

public partial class PopupIconEdit : PopupPage, INotifyPropertyChanged
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }
    public IconItem iconItem;
    public PopupIconEdit(IconItem _iconItem)
    {
        InitializeComponent();

        iconItem = _iconItem;
        var file = iconItem.FileName;

        iconImage.Source = file;
        iconName.Text = iconItem.DisplayName;
        anchorX.Text = iconItem.AnchorPoint.X.ToString(CultureInfo.InvariantCulture);
        anchorY.Text = iconItem.AnchorPoint.Y.ToString(CultureInfo.InvariantCulture);
        iconScale.Value = iconItem.IconScale * 100;
        sliderText.Text = "Voreinstellung Skalierung: " + (iconItem.IconScale * 100).ToString() + "%";
        allowRotate.IsToggled = iconItem.IsRotationLocked;
        RedValue = iconItem.PinColor.Red;
        GreenValue = iconItem.PinColor.Green;
        BlueValue = iconItem.PinColor.Blue;
        SelectedColor = new Color(iconItem.PinColor.Red, iconItem.PinColor.Green, iconItem.PinColor.Blue);

        if (file.Contains("customicons", StringComparison.OrdinalIgnoreCase))
            deleteIcon.IsEnabled = true;

        BindingContext = this;
        UpdateSelectedColor();
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

    private async void OnOkClicked(object sender, EventArgs e)
    {
        // Falls CustomIcon, dann wird Pfad relativ gesetzt
        var file = iconItem.FileName;
        int index = file.IndexOf("customicons", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
            file = file[index..];

        if (deleteIcon.IsToggled == false)
        {
            var updatedItem = new IconItem(
                file,
                iconName.Text,
                new Point(string.IsNullOrEmpty(anchorX.Text) ? 0.0 : double.Parse(anchorX.Text, CultureInfo.InvariantCulture),
                          string.IsNullOrEmpty(anchorY.Text) ? 0.0 : double.Parse(anchorY.Text, CultureInfo.InvariantCulture)),
                iconItem.IconSize,
                allowRotate.IsToggled,
                new SKColor((byte)RedValue, (byte)GreenValue, (byte)BlueValue),
                Math.Round(iconScale.Value / 100, 1)
            );
            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), updatedItem);
            ReturnValue = file;
        }
        else
        {
            var iconFile = Path.Combine(FileSystem.AppDataDirectory, file);
            if (File.Exists(iconFile))
            {
                File.Delete(iconFile);
                Helper.DeleteIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), file);
            }
        }

        // Icon-Daten einlesen
        var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"));
        Settings.PinData = iconItems;
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

    private int redValue = 255;
    public int RedValue
    {
        get => redValue;
        set
        {
            if (redValue != value)
            {
                redValue = value;
                OnPropertyChanged();
                UpdateSelectedColor();
            }
        }
    }

    private int greenValue = 0;
    public int GreenValue
    {
        get => greenValue;
        set
        {
            if (greenValue != value)
            {
                greenValue = value;
                OnPropertyChanged();
                UpdateSelectedColor();
            }
        }
    }

    private int blueValue = 0;
    public int BlueValue
    {
        get => blueValue;
        set
        {
            if (blueValue != value)
            {
                blueValue = value;
                OnPropertyChanged();
                UpdateSelectedColor();
            }
        }
    }

    private Color selectedColor = new(255, 0, 0);
    public Color SelectedColor
    {
        get => selectedColor;
        set
        {
            if (selectedColor != value)
            {
                selectedColor = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateSelectedColor()
    {
        SelectedColor = Color.FromRgb(RedValue, GreenValue, BlueValue);
    }

    public new event PropertyChangedEventHandler PropertyChanged;

    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
