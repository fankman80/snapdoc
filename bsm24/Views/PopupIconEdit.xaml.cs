#nullable disable

using CommunityToolkit.Maui.Views;
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
        BindingContext = this;
        iconItem = _iconItem;
        var file = iconItem.FileName;

        iconImage.Source = file;
        iconName.Text = iconItem.DisplayName;
        iconCategory.Text = iconItem.Category;
        anchorX.Text = iconItem.AnchorPoint.X.ToString(CultureInfo.InvariantCulture);
        anchorY.Text = iconItem.AnchorPoint.Y.ToString(CultureInfo.InvariantCulture);
        iconScale.Value = iconItem.IconScale * 100;
        sliderText.Text = "Voreinstellung Skalierung: " + (iconItem.IconScale * 100).ToString() + "%";
        allowRotate.IsChecked = iconItem.IsRotationLocked;
        SelectedColor = new Color(iconItem.PinColor.Red, iconItem.PinColor.Green, iconItem.PinColor.Blue);

        if (file.Contains("customicons", StringComparison.OrdinalIgnoreCase))
            deleteIconContainer.IsVisible = true;
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

    private Color selectedColor;
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

    private async void OnOkClicked(object sender, EventArgs e)
    {
        // Falls CustomIcon, dann wird Pfad relativ gesetzt
        var file = iconItem.FileName;
        int index = file.IndexOf("customicons", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
            file = file[index..];

        if (deleteIcon.IsChecked == false)
        {
            var updatedItem = new IconItem(
                file,
                iconName.Text,
                new Point(string.IsNullOrEmpty(anchorX.Text) ? 0.0 : double.Parse(anchorX.Text, CultureInfo.InvariantCulture),
                          string.IsNullOrEmpty(anchorY.Text) ? 0.0 : double.Parse(anchorY.Text, CultureInfo.InvariantCulture)),
                iconItem.IconSize,
                allowRotate.IsChecked,
                new SKColor((byte)(SelectedColor.Red * 255), (byte)(SelectedColor.Green * 255), (byte)(SelectedColor.Blue * 255)),
                Math.Round(iconScale.Value / 100, 1),
                iconCategory.Text
            );
            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), updatedItem);
            ReturnValue = file;
        }
        else
        {
            var iconFile = Path.Combine(Settings.DataDirectory, file);
            if (File.Exists(iconFile))
            {
                File.Delete(iconFile);
                Helper.DeleteIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), file);
                ReturnValue = "deleted";
            }
        }
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
        sliderText.Text = "Skalierung:" + Math.Round(sliderValue, 0).ToString() + "%";
    }


    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(0, SelectedColor, lineWidthVisibility: false);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        if (result.Item1 != null)
            SelectedColor = result.Item1;
    }
}
