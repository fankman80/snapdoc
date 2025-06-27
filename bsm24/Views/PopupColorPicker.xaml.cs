#nullable disable

using bsm24.Services;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace bsm24.Views;

public partial class PopupColorPicker : Popup<ColorPickerReturn>, INotifyPropertyChanged
{
    public ObservableCollection<ColorBoxItem> ColorsList { get; set; }
    public bool LineWidthVisibility { get; set; }
    private bool isUpdating = false;
    private double workR, workG, workB;
    private double workH, workS, workV;

    public PopupColorPicker(int lineWidth, Color selectedColor, bool lineWidthVisibility = true, string okText = "Ok")
    {
	    InitializeComponent();
        okButtonText.Text = okText;
        LineWidthVisibility = lineWidthVisibility;
        LineWidth = lineWidth;
        ColorsList = new ObservableCollection<ColorBoxItem>(
                    SettingsService.Instance.ColorList.Select(c => new ColorBoxItem
                    { BackgroundColor = Color.FromRgba(c) }))
                    {
                        new() { BackgroundColor = selectedColor, IsAddButton = true }
                    };

        // Prüfen, ob selectedColor in der Liste vorkommt
        var matchingItem = ColorsList
            .Take(ColorsList.Count - 1)
            .FirstOrDefault(c => c.BackgroundColor.ToHex() == selectedColor.ToHex());

        if (matchingItem != null)
        {
            matchingItem.IsSelected = true;
            RedValue = matchingItem.BackgroundColor.Red;
            GreenValue = matchingItem.BackgroundColor.Green;
            BlueValue = matchingItem.BackgroundColor.Blue;
        }            
        else
        {
            RedValue = selectedColor.Red;
            GreenValue = selectedColor.Green;
            BlueValue = selectedColor.Blue;
        }

        BindingContext = this;
    }

    private void OnColorTapped(object sender, EventArgs e)
    {
        if (sender is MR.Gestures.Border border && border.BindingContext is ColorBoxItem tappedItem)
        {
            foreach (var item in ColorsList)
                item.IsSelected = false;

            tappedItem.IsSelected = true;

            RedValue = tappedItem.BackgroundColor.Red;
            GreenValue = tappedItem.BackgroundColor.Green;
            BlueValue = tappedItem.BackgroundColor.Blue;
        }
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new ColorPickerReturn(SelectedColor.ToHex(), LineWidth));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    public void OnAddTapped(object sender, EventArgs e)
    {
        // Prüfen, ob selectedColor in der Liste vorkommt
        var matchingItem = ColorsList
            .Take(ColorsList.Count - 1)
            .FirstOrDefault(c => c.BackgroundColor.ToHex() == SelectedColor.ToHex());

        if (matchingItem == null)
        {
            foreach (var item in ColorsList)
                item.IsSelected = false;

            // Dummy entfernen
            ColorsList.RemoveAt(ColorsList.Count - 1);

            // Neue Farbe hinzufügen
            ColorsList.Add(new ColorBoxItem { BackgroundColor = SelectedColor, IsSelected = true });

            // Dummy wieder ans Ende setzen
            ColorsList.Add(new ColorBoxItem { IsAddButton = true });

            // Farbliste speichern
            SettingsService.Instance.ColorList = [.. ColorsList
                .Where(c => !c.IsAddButton)
                .Select(c => c.BackgroundColor.ToHex())];

            SettingsService.Instance.SaveSettings();
        }
    }

    private void OnLongPressed(object sender, EventArgs e)
    {
        if (sender is MR.Gestures.Border border && border.BindingContext is ColorBoxItem tappedItem)
        {
            if (!tappedItem.IsAddButton)
            {
                ColorsList.Remove(tappedItem);

                // Farbliste speichern
                SettingsService.Instance.ColorList = [.. ColorsList
                .Where(c => !c.IsAddButton)
                .Select(c => c.BackgroundColor.ToHex())];

                SettingsService.Instance.SaveSettings();
            }
        }
    }

    public double RedValue
    {
        get => workR;
        set
        {
            if (workR == value || isUpdating) return;
            isUpdating = true;

            workR = value;

            UpdateHSVFromRGB_Work();
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

    public double GreenValue
    {
        get => workG;
        set
        {
            if (workG == value || isUpdating) return;
            isUpdating = true;

            workG = value;

            UpdateHSVFromRGB_Work();
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

    public double BlueValue
    {
        get => workB;
        set
        {
            if (workB == value || isUpdating) return;
            isUpdating = true;

            workB = value;

            UpdateHSVFromRGB_Work();
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

    public double BrightnessValue
    {
        get => workV;
        set
        {
            if (workV == value || isUpdating) return;
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

    public double SaturationValue
    {
        get => workS;
        set
        {
            if (workS == value || isUpdating) return;
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

    public double HueValue
    {
        get => workH;
        set
        {
            if (workH == value|| isUpdating) return;
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
        SelectedColor = Color.FromRgb(RedValue, GreenValue, BlueValue);
        if (ColorsList?.Any() == true)
        {
            var lastItem = ColorsList.Last();
            lastItem.BackgroundColor = SelectedColor;
        }
    }

    private void UpdateHSVFromRGB_Work()
    {
        workH = Color.FromRgb(workR, workG, workB).GetHue();
        workS = Color.FromRgb(workR, workG, workB).GetSaturation();
        workV = Color.FromRgb(workR, workG, workB).GetLuminosity();
    }

    private void UpdateRGBFromHSV_Work()
    {
        workR = Color.FromHsla(workH, workS, workV).Red;
        workG = Color.FromHsla(workH, workS, workV).Green;
        workB = Color.FromHsla(workH, workS, workV).Blue;
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public partial class ColorBoxItem : INotifyPropertyChanged
{
    private Color backgroundColor;
    public Color BackgroundColor
    {
        get => backgroundColor;
        set
        {
            if (backgroundColor != value)
            {
                backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }
    }
    public bool IsAddButton { get; set; }

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
