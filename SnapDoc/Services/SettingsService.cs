#nullable disable
using SnapDoc.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;

namespace SnapDoc.Services;
public partial class SettingsService : INotifyPropertyChanged
{
    public static SettingsService Instance { get; } = new SettingsService();
    private const string SettingsFileName = "appsettings.ini";
    private SettingsService()
    {
        ColorThemes = ["EBBE", "Minimalist", "Wine", "Grass", "Fire", "Flower", "Pink"];
        AppThemes = ["Hell", "Dunkel"];
        SelectedColorTheme = ColorThemes[0]; // Standardauswahl
        SelectedAppTheme = AppThemes[0]; // Standardauswahl
        IconSortCrit = IconSortCrits[0]; // Standardauswahl
        PinSortCrit = PinSortCrits[0]; // Standardauswahl
        IconCategory = IconCategories[0]; // Standardauswahl
    }

    public string AppVersion { get; set; } = $"Version {AppInfo.VersionString}";

    private List<string> _mapIcons = new()
    {
        "themeColorPin",
        "mappin1a.png",
        "mappin2a.png",
        "mappin3a.png",
        "mappin4a.png"
    };
    public List<string> MapIcons
    {
        get => _mapIcons;
        set
        {
            if (_mapIcons != value)
            {
                _mapIcons = value;
                OnPropertyChanged(nameof(MapIcons));
            }
        }
    }

    private List<string> _iconSortCrits = new()
    {
        "nach Name",
        "nach Farbe"
    };
    public List<string> IconSortCrits
    {
        get => _iconSortCrits;
        set
        {
            if (_iconSortCrits != value)
            {
                _iconSortCrits = value;
                OnPropertyChanged(nameof(IconSortCrits));
            }
        }
    }

    private List<string> _pinSortCrits = new()
    {
        "nach Plan",
        "nach Pin",
        "nach Standort",
        "nach Bezeichnung",
        "nach aktiv/inaktiv",
        "nach Aufnahmedatum"
    };
    public List<string> PinSortCrits
    {
        get => _pinSortCrits;
        set
        {
            if (_pinSortCrits != value)
            {
                _pinSortCrits = value;
                OnPropertyChanged(nameof(PinSortCrits));
            }
        }
    }

    private List<PriorityItem> _priorityItems = new()
    {
        new PriorityItem { Key = "", Color = "#000000" },
        new PriorityItem { Key = "Empfehlung", Color = "#92D050" },
        new PriorityItem { Key = "Wichtig", Color = "#FFC000" },
        new PriorityItem { Key = "Kritisch", Color = "#FF0000" }
    };

    public List<PriorityItem> PriorityItems
    {
        get => _priorityItems;
        set
        {
            if (_priorityItems != value)
            {
                _priorityItems = value;
                OnPropertyChanged(nameof(PriorityItems));
            }
        }
    }

    public List<string> IconCategories { get; set; } = ["alle Icons"];

    private bool _isProjectLoaded = false;
    public bool IsProjectLoaded
    {
        get => _isProjectLoaded;
        set
        {
            if (_isProjectLoaded != value)
            {
                _isProjectLoaded = value;
                OnPropertyChanged(nameof(IsProjectLoaded));
            }
        }
    }

    private string _flyoutHeaderTitle = "by Emch+Berger AG Bern";
    public string FlyoutHeaderTitle
    {
        get => _flyoutHeaderTitle;
        set
        {
            if (_flyoutHeaderTitle != value)
            {
                _flyoutHeaderTitle = value;
                OnPropertyChanged(nameof(FlyoutHeaderTitle));
            }
        }
    }

    private string _flyoutHeaderDesc = "SnapDoc";
    public string FlyoutHeaderDesc
    {
        get => _flyoutHeaderDesc;
        set
        {
            if (_flyoutHeaderDesc != value)
            {
                _flyoutHeaderDesc = value;
                OnPropertyChanged(nameof(FlyoutHeaderDesc));
            }
        }
    }

    private string _flyoutHeaderImage = "banner_thumbnail.png";
    public string FlyoutHeaderImage
    {
        get => _flyoutHeaderImage;
        set
        {
            if (_flyoutHeaderImage != value)
            {
                _flyoutHeaderImage = value;
                OnPropertyChanged(nameof(FlyoutHeaderImage));
            }
        }
    }

    private string _iconGalleryMode = "IconListTemplate";
    public string IconGalleryMode
    {
        get => _iconGalleryMode;
        set
        {
            if (_iconGalleryMode != value)
            {
                _iconGalleryMode = value;
                OnPropertyChanged(nameof(IconGalleryMode));
            }
        }
    }

    // Allgemeine Größen-Settings
    private int _maxPdfImageSizeW = 8192;
    public int MaxPdfImageSizeW
    {
        get => _maxPdfImageSizeW;
        set
        {
            if (_maxPdfImageSizeW != value)
            {
                _maxPdfImageSizeW = value;
                OnPropertyChanged(nameof(MaxPdfImageSizeW));
            }
        }
    }

    private int _maxPdfImageSizeH = 8192;
    public int MaxPdfImageSizeH
    {
        get => _maxPdfImageSizeH;
        set
        {
            if (_maxPdfImageSizeH != value)
            {
                _maxPdfImageSizeH = value;
                OnPropertyChanged(nameof(MaxPdfImageSizeH));
            }
        }
    }

    private int _thumbSize = 150;
    public int ThumbSize
    {
        get => _thumbSize;
        set
        {
            if (_thumbSize != value)
            {
                _thumbSize = value;
                OnPropertyChanged(nameof(ThumbSize));
            }
        }
    }

    private int _planPreviewSize = 150;
    public int PlanPreviewSize
    {
        get => _planPreviewSize;
        set
        {
            if (_planPreviewSize != value)
            {
                _planPreviewSize = value;
                OnPropertyChanged(nameof(PlanPreviewSize));
            }
        }
    }

    private int _iconPreviewSize = 64;
    public int IconPreviewSize
    {
        get => _iconPreviewSize;
        set
        {
            if (_iconPreviewSize != value)
            {
                _iconPreviewSize = value;
                OnPropertyChanged(nameof(IconPreviewSize));
            }
        }
    }

    private int _pinTextPadding = 6;
    public int PinTextPadding
    {
        get => _pinTextPadding;
        set
        {
            if (_pinTextPadding != value)
            {
                _pinTextPadding = value;
                OnPropertyChanged(nameof(PinTextPadding));
            }
        }
    }

    private int _pinTextDistance = 3;
    public int PinTextDistance
    {
        get => _pinTextDistance;
        set
        {
            if (_pinTextDistance != value)
            {
                _pinTextDistance = value;
                OnPropertyChanged(nameof(PinTextDistance));
            }
        }
    }

    private double _defaultPinZoom = 2;
    public double DefaultPinZoom
    {
        get => _defaultPinZoom;
        set
        {
            if (_defaultPinZoom != value)
            {
                _defaultPinZoom = value;
                OnPropertyChanged(nameof(DefaultPinZoom));
            }
        }
    }

    private List<string> _colorList = [
    "#009900",
    "#CAFE96",
    "#000000",
    "#7F00FF",
    "#0365DD",
    "#7FBFFF",
    "#7D5F00",
    "#DF7100",
    "#FFBF00",
    "#C565E3",
    "#FABAFC",
    "#79F3F3",
    "#0032CC",
    "#FF0000",
    "#FFFF00",
    "#DFDFDF"];
    public List<string> ColorList
    {
        get => _colorList;
        set
        {
            if (_colorList != value)
            {
                _colorList = value;
                OnPropertyChanged(nameof(ColorList));
            }
        }
    }

    private bool _isPlanRotateLocked = false;
    public bool IsPlanRotateLocked
    {
        get => _isPlanRotateLocked;
        set
        {
            if (_isPlanRotateLocked != value)
            {
                _isPlanRotateLocked = value;
                OnPropertyChanged(nameof(IsPlanRotateLocked));
            }
        }
    }

    private double _pinMinScaleLimit = 60;
    public double PinMinScaleLimit
    {
        get => _pinMinScaleLimit;
        set
        {
            if (_pinMinScaleLimit != value)
            {
                _pinMinScaleLimit = Math.Round(value, 0);
                OnPropertyChanged(nameof(PinMinScaleLimit));
            }
        }
    }

    private double _pinMaxScaleLimit = 100;
    public double PinMaxScaleLimit
    {
        get => _pinMaxScaleLimit;
        set
        {
            if (_pinMaxScaleLimit != value)
            {
                _pinMaxScaleLimit = Math.Round(value,0);
                OnPropertyChanged(nameof(PinMaxScaleLimit));
            }
        }
    }

    private int _maxPdfPixelCount = 30000000;
    public int MaxPdfPixelCount
    {
        get => _maxPdfPixelCount;
        set
        {
            if (_maxPdfPixelCount != value)
            {
                _maxPdfPixelCount = (value / 1000000) * 1000000; ;
                OnPropertyChanged(nameof(MaxPdfPixelCount));
            }
        }
    }

    private int _mapIconSize = 85;
    public int MapIconSize
    {
        get => _mapIconSize;
        set
        {
            if (_mapIconSize != value)
            {
                _mapIconSize = value;
                OnPropertyChanged(nameof(MapIconSize));
            }
        }
    }

    private int _mapIcon = 0;
    public int MapIcon
    {
        get => _mapIcon;
        set
        {
            if (_mapIcon != value)
            {
                _mapIcon = value;
                OnPropertyChanged(nameof(MapIcon));
            }
        }
    }

    private int _pinPlaceMode = 0;
    public int PinPlaceMode
    {
        get => _pinPlaceMode;
        set
        {
            if (_pinPlaceMode != value)
            {
                _pinPlaceMode = value;
                OnPropertyChanged(nameof(PinPlaceMode));
            }
        }
    }

    private string _iconSortCrit;
    public string IconSortCrit
    {
        get => _iconSortCrit;
        set
        {
            if (_iconSortCrit != value)
            {
                _iconSortCrit = value;
                OnPropertyChanged(nameof(IconSortCrit));
            }
        }
    }

    private string _pinSortCrit;
    public string PinSortCrit
    {
        get => _pinSortCrit;
        set
        {
            if (_pinSortCrit != value)
            {
                _pinSortCrit = value;
                OnPropertyChanged(nameof(PinSortCrit));
            }
        }
    }

    private string _iconCategory;
    public string IconCategory
    {
        get => _iconCategory;
        set
        {
            if (_iconCategory != value)
            {
                _iconCategory = value;
                OnPropertyChanged(nameof(IconCategory));
            }
        }
    }

    #region
    // Export Settings

    private int _imageExportQuality = 80;
    public int ImageExportQuality
    {
        get => _imageExportQuality;
        set
        {
            if (_imageExportQuality != value)
            {
                _imageExportQuality = value;
                OnPropertyChanged(nameof(ImageExportQuality));
            }
        }
    }

    private int _planLabelFontSize = 4;
    public int PlanLabelFontSize
    {
        get => _planLabelFontSize;
        set
        {
            if (_planLabelFontSize != value)
            {
                _planLabelFontSize = value;
                OnPropertyChanged(nameof(PlanLabelFontSize));
            }
        }
    }


    private string _planLabelPrefix = "Pos. ";
    public string PlanLabelPrefix
    {
        get => _planLabelPrefix;
        set
        {
            if (_planLabelPrefix != value)
            {
                _planLabelPrefix = value;
                OnPropertyChanged(nameof(PlanLabelPrefix));
            }
        }
    }

    private bool _isPlanExport = true;
    public bool IsPlanExport
    {
        get => _isPlanExport;
        set
        {
            if (_isPlanExport != value)
            {
                _isPlanExport = value;
                OnPropertyChanged(nameof(IsPlanExport));
            }
        }
    }

    private bool _isPosImageExport = true;
    public bool IsPosImageExport
    {
        get => _isPosImageExport;
        set
        {
            if (_isPosImageExport != value)
            {
                _isPosImageExport = value;
                OnPropertyChanged(nameof(IsPosImageExport));
            }
        }
    }

    private bool _isImageExport = true;
    public bool IsImageExport
    {
        get => _isImageExport;
        set
        {
            if (_isImageExport != value)
            {
                _isImageExport = value;
                OnPropertyChanged(nameof(IsImageExport));
            }
        }
    }

    private bool _isPinIconExport = true;
    public bool IsPinIconExport
    {
        get => _isPinIconExport;
        set
        {
            if (_isPinIconExport != value)
            {
                _isPinIconExport = value;
                OnPropertyChanged(nameof(IsPinIconExport));
            }
        }
    }

    private bool _isFotoOverlayExport = true;
    public bool IsFotoOverlayExport
    {
        get => _isFotoOverlayExport;
        set
        {
            if (_isFotoOverlayExport != value)
            {
                _isFotoOverlayExport = value;
                OnPropertyChanged(nameof(IsFotoOverlayExport));
            }
        }
    }

    private bool _isFotoCompressed = true;
    public bool IsFotoCompressed
    {
        get => _isFotoCompressed;
        set
        {
            if (_isFotoCompressed != value)
            {
                _isFotoCompressed = value;
                OnPropertyChanged(nameof(IsFotoCompressed));
            }
        }
    }

    private int _fotoCompressValue = 20;
    public int FotoCompressValue
    {
        get => _fotoCompressValue;
        set
        {
            if (_fotoCompressValue != value)
            {
                _fotoCompressValue = value;
                OnPropertyChanged(nameof(FotoCompressValue));
            }
        }
    }

    private int _imageExportSize = 40;
    public int ImageExportSize
    {
        get => _imageExportSize;
        set
        {
            if (_imageExportSize != value)
            {
                _imageExportSize = value;
                OnPropertyChanged(nameof(ImageExportSize));
            }
        }
    }

    private double _pinExportSize = 3.2;
    public double PinExportSize
    {
        get => _pinExportSize;
        set
        {
            if (_pinExportSize != value)
            {
                _pinExportSize = Math.Round(value, 1);
                OnPropertyChanged(nameof(PinExportSize));
            }
        }
    }

    private int _titleExportSize = 90;
    public int TitleExportSize
    {
        get => _titleExportSize;
        set
        {
            if (_titleExportSize != value)
            {
                _titleExportSize = value;
                OnPropertyChanged(nameof(TitleExportSize));
            }
        }
    }

    private int _pinPosExportSize = 25;
    public int PinPosExportSize
    {
        get => _pinPosExportSize;
        set
        {
            if (_pinPosExportSize != value)
            {
                _pinPosExportSize = value;
                OnPropertyChanged(nameof(PinPosExportSize));
            }
        }
    }

    private int _pinPosCropExportSize = 300;
    public int PinPosCropExportSize
    {
        get => _pinPosCropExportSize;
        set
        {
            if (_pinPosCropExportSize != value)
            {
                _pinPosCropExportSize = value;
                OnPropertyChanged(nameof(PinPosCropExportSize));
            }
        }
    }
    #endregion

    private List<string> _colorThemes;
    public List<string> ColorThemes
    {
        get => _colorThemes;
        set
        {
            _colorThemes = value;
            OnPropertyChanged(nameof(ColorThemes));
        }
    }

    private string _selectedColorTheme;
    public string SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (_selectedColorTheme != value)
            {
                _selectedColorTheme = value;
                OnPropertyChanged(nameof(SelectedColorTheme));

                // Logik für das Anwenden der Farben basierend auf der Auswahl
                switch (_selectedColorTheme)
                {
                    case "Minimalist":
                        App.Current.Resources["Primary"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#ededed");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#949494");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#000000");
                        break;

                    case "Wine":
                        App.Current.Resources["Primary"] = Color.FromArgb("#9c4e38");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#9c4e38");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#e6c3ba");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#9c4e38");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#e6c3ba");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#9c4e38");
                        break;

                    case "Grass":
                        App.Current.Resources["Primary"] = Color.FromArgb("#32a852");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#32a852");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#c1e8c1");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#32a852");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#c1e8c1");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#32a852");
                        break;

                    case "EBBE":
                        App.Current.Resources["Primary"] = Color.FromArgb("#00b0ca");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#5e75ad");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#00b0ca");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#00b0ca");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#00b0ca");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#00b0ca");
                        break;

                    case "Fire":
                        App.Current.Resources["Primary"] = Color.FromArgb("#e07a2d");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#e07a2d");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#f2cdb1");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#e07a2d");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#f2cdb1");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#e07a2d");
                        break;

                    case "Flower":
                        App.Current.Resources["Primary"] = Color.FromArgb("#9f4bcc");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#9f4bcc");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#e5befa");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#9f4bcc");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#e5befa");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#9f4bcc");
                        break;

                    case "Pink":
                        App.Current.Resources["Primary"] = Color.FromArgb("#fc03df");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#fc03df");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#eed5f2");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#fc03df");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#eed5f2");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#fc03df");
                        break;

                    default:
                        break;
                }
            }
        }
    }

    private List<string> _appThemes;
    public List<string> AppThemes
    {
        get => _appThemes;
        set
        {
            _appThemes = value;
            OnPropertyChanged(nameof(AppThemes));
        }
    }

    private string _selectedAppTheme;
    public string SelectedAppTheme
    {
        get => _selectedAppTheme;
        set
        {
            if (_selectedAppTheme != value)
            {
                _selectedAppTheme = value;
                OnPropertyChanged(nameof(SelectedAppTheme));

                // Logik für das Anwenden der Farben basierend auf der Auswahl
                switch (_selectedAppTheme)
                {
                    case "Hell":
                        App.Current.UserAppTheme = AppTheme.Light; // Setze auf helles Theme
                        break;
                    case "Dunkel":
                        App.Current.UserAppTheme = AppTheme.Dark; // Setze auf dunkles Theme
                        break;
                }
            }
        }
    }

    private ObservableCollection<string> _templates = [];
    public ObservableCollection<string> Templates
    {
        get => _templates;
        set
        {
            if (_templates != value)
            {
                _templates = value;
                OnPropertyChanged(nameof(Templates));
            }
        }
    }

    private string _selectedTemplate;
    public string SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (_selectedTemplate != value)
            {
                _selectedTemplate = value;
                OnPropertyChanged(nameof(SelectedTemplate));
            }
        }
    }

    public void SaveSettings()
    {
        var settings = new SettingsModel
        {
            PinMinScaleLimit = this.PinMinScaleLimit,
            PinMaxScaleLimit = this.PinMaxScaleLimit,
            MapIconSize = this.MapIconSize,
            MapIcon = this.MapIcon,
            PinPlaceMode = this.PinPlaceMode,
            IsPlanRotateLocked = this.IsPlanRotateLocked,
            MaxPdfPixelCount = this.MaxPdfPixelCount,
            SelectedColorTheme = ColorThemes.IndexOf(this.SelectedColorTheme),
            SelectedAppTheme = AppThemes.IndexOf(this.SelectedAppTheme),
            IconSortCrit = IconSortCrits.IndexOf(this.IconSortCrit),
            PinSortCrit = PinSortCrits.IndexOf(this.PinSortCrit),
            IconCategory = IconCategories.IndexOf(this.IconCategory),
            IsFotoCompressed = this.IsFotoCompressed,
            FotoCompressValue = this.FotoCompressValue,
            IconGalleryMode = this.IconGalleryMode,
            MaxPdfImageSizeW = this.MaxPdfImageSizeW,
            MaxPdfImageSizeH = this.MaxPdfImageSizeH,
            ThumbSize = this.ThumbSize,
            PlanPreviewSize = this.PlanPreviewSize,
            IconPreviewSize = this.IconPreviewSize,
            PinTextPadding = this.PinTextPadding,
            PinTextDistance = this.PinTextDistance,
            DefaultPinZoom = this.DefaultPinZoom,
            ColorList = this.ColorList,
            MapIcons = this.MapIcons,
            IconSortCrits = this.IconSortCrits,
            PinSortCrits = this.PinSortCrits,
            PriorityItems = this.PriorityItems,
        };

        var json = JsonSerializer.Serialize(settings, GetOptions());
        File.WriteAllText(Path.Combine(Settings.DataDirectory, SettingsFileName), json);
    }

    public void LoadSettings()
    {
        var filePath = Path.Combine(Settings.DataDirectory, SettingsFileName);
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<SettingsModel>(json);
            if (settings != null)
            {
                this.PinMinScaleLimit = settings.PinMinScaleLimit;
                this.PinMaxScaleLimit = settings.PinMaxScaleLimit;
                this.MapIconSize = settings.MapIconSize;
                this.MapIcon = settings.MapIcon;
                this.PinPlaceMode = settings.PinPlaceMode;
                this.IsPlanRotateLocked = settings.IsPlanRotateLocked;
                this.MaxPdfPixelCount = settings.MaxPdfPixelCount;
                this.SelectedAppTheme = (settings.SelectedAppTheme < AppThemes.Count)
                    ? AppThemes[settings.SelectedAppTheme]
                    : AppThemes[0];
                this.SelectedColorTheme = (settings.SelectedColorTheme < ColorThemes.Count)
                    ? ColorThemes[settings.SelectedColorTheme]
                    : ColorThemes[0];
                this.IconSortCrit = (settings.IconSortCrit < IconSortCrits.Count)
                    ? IconSortCrits[settings.IconSortCrit]
                    : IconSortCrits[0];
                this.PinSortCrit = (settings.PinSortCrit < PinSortCrits.Count)
                    ? PinSortCrits[settings.PinSortCrit]
                    : PinSortCrits[0];
                this.IconCategory = (settings.IconCategory < IconCategories.Count && settings.IconCategory > 0)
                    ? IconCategories[settings.IconCategory]
                    : IconCategories[0];
                this.IsFotoCompressed = settings.IsFotoCompressed;
                this.FotoCompressValue = settings.FotoCompressValue;
                this.IconGalleryMode = settings.IconGalleryMode ?? "IconListTemplate";
                this.MaxPdfImageSizeW = settings.MaxPdfImageSizeW;
                this.MaxPdfImageSizeH = settings.MaxPdfImageSizeH;
                this.ThumbSize = settings.ThumbSize;
                this.PlanPreviewSize = settings.PlanPreviewSize;
                this.IconPreviewSize = settings.IconPreviewSize;
                this.PinTextPadding = settings.PinTextPadding;
                this.PinTextDistance = settings.PinTextDistance;
                this.DefaultPinZoom = settings.DefaultPinZoom;
                this.ColorList = settings.ColorList ?? _colorList;
                this.MapIcons = settings.MapIcons ?? _mapIcons;
                this.IconSortCrits = settings.IconSortCrits ?? _iconSortCrits;
                this.PinSortCrits = settings.PinSortCrits ?? _pinSortCrits;
                this.PriorityItems = settings.PriorityItems ?? _priorityItems;
            }
        }
    }

    public static JsonSerializerOptions GetOptions()
    {
        return new() { WriteIndented = true };
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
