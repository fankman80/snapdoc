#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace SnapDoc.Views;

public partial class PopupColorPicker : Popup<ColorPickerReturn>, INotifyPropertyChanged
{
    public ObservableCollection<ColorBoxItem> ColorsList { get; set; }
    public bool FillOpacityVisibility { get; set; }
    private bool isUpdating = false;
    private double workR, workG, workB;
    private double workH, workS, workV;

    public ICommand TapCommand =>
        new Command<ColorBoxItem>(item => SelectColor(item));

    public ICommand DoubleTapCommand =>
        new Command<ColorBoxItem>(item => DeleteColor(item));

    public PopupColorPicker(Color selectedColor, byte fillOpacity = 255, bool fillOpacityVisibility = false, string okText = "Ok")
    {
	    InitializeComponent();
        okButtonText.Text = okText;
        FillOpacityVisibility = fillOpacityVisibility;
        FillOpacity = fillOpacity;
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

    private void SelectColor(ColorBoxItem _pressedItem)
    {
        foreach (var item in ColorsList)
            item.IsSelected = false;

        _pressedItem.IsSelected = true;

        RedValue = _pressedItem.BackgroundColor.Red;
        GreenValue = _pressedItem.BackgroundColor.Green;
        BlueValue = _pressedItem.BackgroundColor.Blue;
    }

    private void DeleteColor(ColorBoxItem _pressedItem)
    {
        if (!_pressedItem.IsAddButton)
        {
            ColorsList.Remove(_pressedItem);

            // Farbliste speichern
            SettingsService.Instance.ColorList = [.. ColorsList
            .Where(c => !c.IsAddButton)
            .Select(c => c.BackgroundColor.ToHex())];

            SettingsService.Instance.SaveSettings();
        }
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new ColorPickerReturn(selectedColor.ToHex(), fillOpacity));
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

    private byte fillOpacity;
    public byte FillOpacity
    {
        get => fillOpacity;
        set
        {
            if (fillOpacity != value)
            {
                fillOpacity = value;
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

    private void OnColorBoxSizeChanged(object sender, EventArgs e)
    {
        if (sender is Border border && border.Width > 0)
        {
            border.HeightRequest = border.Width;
        }
    }
}
