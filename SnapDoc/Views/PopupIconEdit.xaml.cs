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
        IconScale = iconItem.IconScale;
        allowRotate.IsToggled = iconItem.IsRotationLocked;
        allowAutoScale.IsToggled = iconItem.IsAutoScaleLocked;
        SelectedColor = new Color(iconItem.PinColor.Red, iconItem.PinColor.Green, iconItem.PinColor.Blue);
        setDefault.IsToggled = iconItem.IsDefaultIcon;

        if (iconItem.IsCustomIcon)
            deleteIconContainer.IsVisible = true;

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
            if (anchor_X != value)
            {
                anchor_X = value;
                TransX = (int)(value * IconPreviewWidth);
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
            if (anchor_Y != value)
            {
                anchor_Y = value;
                TransY = (int)(value * IconPreviewHeight);
                OnPropertyChanged();
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
        var file = Path.GetFileName(iconItem.FileName);
        string returnValue = null;

        if (deleteIcon.IsToggled == false)
        {
            var updatedItem = new IconItem(
                file,
                iconName.Text,
                new Point(Anchor_X, Anchor_Y),
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

        // Icon-Daten einlesen
        Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
        SettingsService.Instance.IconCategories = iconCategories;
        IconLookup.Initialize(Settings.IconData);

        await CloseAsync(returnValue);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    public new event PropertyChangedEventHandler PropertyChanged;
    protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnColorPickerClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(0, SelectedColor, lineWidthVisibility: false);
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
            SelectedColor = Color.FromArgb(result.Result.PenColorHex);
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
