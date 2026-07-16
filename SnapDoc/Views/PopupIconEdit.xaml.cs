#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SkiaSharp;
using SnapDoc.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static SnapDoc.Helper;

namespace SnapDoc.Views;

public partial class PopupIconEdit : Popup<string>, INotifyPropertyChanged
{
    public IconItem iconItem;
    public int IconPreviewWidth { get; set; } = 120;
    public int IconPreviewHeight { get; set; }

    public PopupIconEdit(IconItem _iconItem)
    {
        InitializeComponent();
        iconItem = _iconItem;
        iconImage.Source = iconItem.DisplayIconPath;
        iconName.Text = iconItem.DisplayName;
        iconCategory.Text = iconItem.Category;
        IconPreviewHeight = (int)(IconPreviewWidth * iconItem.IconSize.Height / iconItem.IconSize.Width);

        Anchor_X = iconItem.AnchorPoint.X;
        Anchor_Y = iconItem.AnchorPoint.Y;
        Anchor_X_Text = Anchor_X.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        Anchor_Y_Text = Anchor_Y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        IconScale = iconItem.IconScale;
        allowRotate.IsToggled = iconItem.IsRotationLocked;
        allowAutoScale.IsToggled = iconItem.IsAutoScaleLocked;
        SelectedColor = new Color(iconItem.PinColor.Red, iconItem.PinColor.Green, iconItem.PinColor.Blue);
        setDefault.IsToggled = iconItem.IsDefaultIcon;

        if (iconItem.IsCustomIcon)
            deleteIcon.IsVisible = true;

        BindingContext = this;
        StartBlinking();
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

    private double anchor_X;
    public double Anchor_X
    {
        get => anchor_X;
        set
        {
            double clampedValue = Math.Clamp(value, 0.0, 1.0);
            if (anchor_X != clampedValue)
            {
                anchor_X = clampedValue;
                TransX = (int)(clampedValue * IconPreviewWidth);
                OnPropertyChanged();
            }
        }
    }

    private double anchor_Y;
    public double Anchor_Y
    {
        get => anchor_Y;
        set
        {
            double clampedValue = Math.Clamp(value, 0.0, 1.0);
            if (anchor_Y != clampedValue)
            {
                anchor_Y = clampedValue;
                TransY = (int)(clampedValue * IconPreviewHeight);
                OnPropertyChanged();
            }
        }
    }

    private string anchor_X_Text;
    public string Anchor_X_Text
    {
        get => anchor_X_Text;
        set
        {
            if (anchor_X_Text != value)
            {
                anchor_X_Text = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(value))
                {
                    string cleanX = value.Replace(',', '.');
                    if (double.TryParse(cleanX, System.Globalization.CultureInfo.InvariantCulture, out double parsedX))
                    {
                        double clampedX = Math.Clamp(parsedX, 0.0, 1.0);
                        TransX = (int)(clampedX * IconPreviewWidth);
                    }
                }
            }
        }
    }

    private string anchor_Y_Text;
    public string Anchor_Y_Text
    {
        get => anchor_Y_Text;
        set
        {
            if (anchor_Y_Text != value)
            {
                anchor_Y_Text = value;
                OnPropertyChanged();

                if (!string.IsNullOrEmpty(value))
                {
                    string cleanY = value.Replace(',', '.');
                    if (double.TryParse(cleanY, System.Globalization.CultureInfo.InvariantCulture, out double parsedY))
                    {
                        double clampedY = Math.Clamp(parsedY, 0.0, 1.0);
                        TransY = (int)(clampedY * IconPreviewHeight);
                    }
                }
            }
        }
    }

    private int transX;
    public int TransX
    {
        get => transX;
        set
        {
            if (transX != value)
            {
                transX = value;
                OnPropertyChanged();
            }
        }
    }

    private int transY;
    public int TransY
    {
        get => transY;
        set
        {
            if (transY != value)
            {
                transY = value;
                OnPropertyChanged();
            }
        }
    }

    private double iconScale;
    public double IconScale
    {
        get => iconScale;
        set
        {
            if (iconScale != value)
            {
                iconScale = value;
                OnPropertyChanged();
            }
        }
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(Anchor_X_Text))
        {
            string cleanX = Anchor_X_Text.Replace(',', '.');
            if (double.TryParse(cleanX, System.Globalization.CultureInfo.InvariantCulture, out double parsedX))
            {
                Anchor_X = parsedX; // Setzt den echten Double-Wert inklusive Math.Clamp
            }
        }

        if (!string.IsNullOrEmpty(Anchor_Y_Text))
        {
            string cleanY = Anchor_Y_Text.Replace(',', '.');
            if (double.TryParse(cleanY, System.Globalization.CultureInfo.InvariantCulture, out double parsedY))
            {
                Anchor_Y = parsedY; // Setzt den echten Double-Wert inklusive Math.Clamp
            }
        }

        var file = Path.GetFileName(iconItem.FileName);
        string returnValue = null;

        if (deleteIcon.IsToggled == false)
        {
            var updatedItem = new IconItem(
                file,
                iconName.Text,
                new Point(Anchor_X, Anchor_Y), // Garantiert die korrekten, gecleanten Double-Werte
                iconItem.IconSize,
                allowRotate.IsToggled,
                allowAutoScale.IsToggled,
                iconItem.IsCustomIcon,
                new SKColor((byte)(SelectedColor.Red * 255), (byte)(SelectedColor.Green * 255), (byte)(SelectedColor.Blue * 255)),
                IconScale,
                iconCategory.Text,
                SettingsService.Instance.DefaultPinIcon == file
            );

            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), updatedItem);
            IconLookup.AddOrUpdate(updatedItem);
            returnValue = file;

            if (setDefault.IsToggled)
            {
                SettingsService.Instance.DefaultPinIcon = file;
                SettingsService.Instance.SaveSettings();
            }
        }
        else
        {
            var iconFile = Path.Combine(Settings.DataDirectory, "customicons", file);
            if (File.Exists(iconFile))
            {
                if (iconItem.IsDefaultIcon)
                {
                    SettingsService.Instance.DefaultPinIcon = Settings.IconData.FirstOrDefault().FileName;
                    SettingsService.Instance.SaveSettings();
                }
                File.Delete(iconFile);

                Helper.DeleteIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), file);
                IconLookup.Remove(file);
                returnValue = "deleted";
            }
        }

        Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
        SettingsService.Instance.IconCategories = iconCategories;
        IconLookup.Initialize(Settings.IconData);

        try { await CloseAsync(returnValue); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(SelectedColor);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedColor = Color.FromArgb(result.Result.ColorHex);
    }

    private void StartBlinking()
    {
        var blinkAnimation = new Animation
        {
            {
                0, 0.5,
                new Animation(v => { blinkingLabel.TextColor = Color.FromRgb(v, v, v); }, 0, 1)
            },
            {
                0.5, 1,
                new Animation(v => { blinkingLabel.TextColor = Color.FromRgb(v, v, v); }, 1, 0)
            }
        };

        blinkAnimation.Commit(blinkingLabel, "BlinkColor", length: 400, repeat: () => true);
    }
}
