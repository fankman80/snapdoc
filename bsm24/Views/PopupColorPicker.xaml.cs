#nullable disable

using ColorMine.ColorSpaces;
using DocumentFormat.OpenXml;
using Mopups.Pages;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bsm24.Views;

public partial class PopupColorPicker : PopupPage, INotifyPropertyChanged
{
    TaskCompletionSource<(Color, int)> _taskCompletionSource;
    public ObservableCollection<ColorBoxItem> ColorsList { get; set; }
    public Task<(Color, int)> PopupDismissedTask => _taskCompletionSource.Task;
    public (Color, int) ReturnValue { get; set; }
    public bool LineWidthVisibility { get; set; }
    private bool isUpdating = false;
    private double workR, workG, workB;
    private float workH, workS, workV;

    public PopupColorPicker(int lineWidth, Color selectedColor, bool lineWidthVisibility = true, string okText = "Ok")
    {
	    InitializeComponent();
        okButtonText.Text = okText;
        LineWidthVisibility = lineWidthVisibility;
        LineWidth = lineWidth;
        ColorsList = new ObservableCollection<ColorBoxItem>(
                    Settings.ColorData.Select(c => new ColorBoxItem
                    { BackgroundColor = c }));

        // Prüfen, ob selectedColor in der Liste vorkommt
        var matchingItem = ColorsList.FirstOrDefault(c => c.BackgroundColor.ToHex() == selectedColor.ToHex());
        if (matchingItem != null)
        {
            matchingItem.IsSelected = true;
            RedValue = matchingItem.BackgroundColor.Red;
            GreenValue = matchingItem.BackgroundColor.Green;
            BlueValue = matchingItem.BackgroundColor.Blue;
        }            
        else if (ColorsList.Count > 0)
        {
            RedValue = selectedColor.Red;
            GreenValue = selectedColor.Green;
            BlueValue = selectedColor.Blue;
        }

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<(Color, int)>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is ColorBoxItem tappedItem)
        {
            foreach (var item in ColorsList)
                item.IsSelected = false;

            tappedItem.IsSelected = true;

            RedValue = tappedItem.BackgroundColor.Red;
            GreenValue = tappedItem.BackgroundColor.Green;
            BlueValue = tappedItem.BackgroundColor.Blue;
        }
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = (null, 0);
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = (null, 0);
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = (SelectedColor, LineWidth);
        await MopupService.Instance.PopAsync();
    }

    public double RedValue
    {
        get => workR;
        set
        {
            if (workR == value || isUpdating) return;
            isUpdating = true;

            // 1) Arbeitsschritt: Arbeitsfelder updaten
            workR = value;

            // 2) Gesamtes Farbmodell umrechnen
            UpdateHSVFromRGB_Work();
            UpdateSelectedColor_Work();

            // 3) Gebundene Properties feuern
            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
    }

    public double GreenValue
    {
        get => workG;
        set
        {
            if (workG == value || isUpdating) return;
            isUpdating = true;

            // 1) Arbeitsschritt: Arbeitsfelder updaten
            workG = value;

            // 2) Gesamtes Farbmodell umrechnen
            UpdateHSVFromRGB_Work();
            UpdateSelectedColor_Work();

            // 3) Gebundene Properties feuern
            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
    }

    public double BlueValue
    {
        get => workB;
        set
        {
            if (workB == value || isUpdating) return;
            isUpdating = true;

            // 1) Arbeitsschritt: Arbeitsfelder updaten
            workB = value;

            // 2) Gesamtes Farbmodell umrechnen
            UpdateHSVFromRGB_Work();
            UpdateSelectedColor_Work();

            // 3) Gebundene Properties feuern
            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
    }

    public float BrightnessValue
    {
        get => workV;
        set
        {
            if (Math.Abs(workV - value) < 0.001f || isUpdating) return;
            isUpdating = true;

            workV = value;

            UpdateRGBFromHSV_Work();
            UpdateSelectedColor_Work();

            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
    }

    public float SaturationValue
    {
        get => workS;
        set
        {
            if (Math.Abs(workS - value) < 0.001f || isUpdating) return;
            isUpdating = true;

            workS = value;

            UpdateRGBFromHSV_Work();
            UpdateSelectedColor_Work();

            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
    }

    public float HueValue
    {
        get => workH;
        set
        {
            if (Math.Abs(workH - value) < 0.001f || isUpdating) return;
            isUpdating = true;

            workH = value;

            UpdateRGBFromHSV_Work();
            UpdateSelectedColor_Work();

            OnPropertyChanged(nameof(RedValue));
            OnPropertyChanged(nameof(GreenValue));
            OnPropertyChanged(nameof(BlueValue));
            OnPropertyChanged(nameof(HueValue));
            OnPropertyChanged(nameof(SaturationValue));
            OnPropertyChanged(nameof(BrightnessValue));
            OnPropertyChanged(nameof(SelectedColor));

            isUpdating = false;
        }
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

    private int lineWidth;
    public int LineWidth
    {
        get => lineWidth;
        set
        {
            if (lineWidth != value)
            {
                lineWidth = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateSelectedColor_Work()
    {
        SelectedColor = Color.FromHsv(workH, workS, workV);
    }

    private void UpdateHSVFromRGB_Work()
    {
        workH = Color.FromRgb(RedValue, GreenValue, BlueValue).GetHue();
        workS = Color.FromRgb(RedValue, GreenValue, BlueValue).GetSaturation();
        //workV = Color.FromRgb(RedValue, GreenValue, BlueValue).GetLuminosity();

        var conv = Color.FromRgb(RedValue, GreenValue, BlueValue).To<Hsv>();
        //workH = (float)conv.H;
        //workS = (float)conv.S;
        workV = (float)conv.V;
    }

    private void UpdateRGBFromHSV_Work()
    {
        workR = Color.FromHsv(HueValue, SaturationValue, BrightnessValue).Red;
        workG = Color.FromHsv(HueValue, SaturationValue, BrightnessValue).Green;
        workB = Color.FromHsv(HueValue, SaturationValue, BrightnessValue).Blue;
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public partial class ColorBoxItem : INotifyPropertyChanged
{
    public Color BackgroundColor { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}