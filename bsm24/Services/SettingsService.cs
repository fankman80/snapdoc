#nullable disable
using bsm24.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;

namespace bsm24.Services;
public partial class SettingsService : INotifyPropertyChanged
{
    public static SettingsService Instance { get; } = new SettingsService();
    private const string SettingsFileName = "appsettings.ini";
    private SettingsService()
    {
        ColorThemes = ["EBBE", "Lachs", "Gras", "Ozean", "Feuer", "Flower", "Barbie"];
        AppThemes = ["Light", "Dark"];
        SelectedColorTheme = ColorThemes[0]; // Standardauswahl
        SelectedAppTheme = AppThemes[0]; // Standardauswahl
        IconSortCrit = IconSortCrits[0]; // Standardauswahl
        PinSortCrit = PinSortCrits[0]; // Standardauswahl
    }

    public List<string> MapIcons { get; set; } = Settings.MapIcons;

    public List<string> IconSortCrits { get; set; } = Settings.IconSortCrits;

    public List<string> PinSortCrits { get; set; } = Settings.PinSortCrits;


    private string _flyoutHeaderTitle = "";
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

    private string _flyoutHeaderDesc = "BSM 24 by EBBE";
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
                _pinMinScaleLimit = Math.Min(Math.Round(value,0), PinMaxScaleLimit);
                OnPropertyChanged(nameof(PinMinScaleLimit));
            }
        }
    }

    private double _pinMaxScaleLimit = 80;
    public double PinMaxScaleLimit
    {
        get => _pinMaxScaleLimit;
        set
        {
            if (_pinMaxScaleLimit != value)
            {
                _pinMaxScaleLimit = Math.Max(Math.Round(value,0), PinMinScaleLimit);
                OnPropertyChanged(nameof(PinMaxScaleLimit));
            }
        }
    }

    private int _pdfQuality = 350;
    public int PdfQuality
    {
        get => _pdfQuality;
        set
        {
            if (_pdfQuality != value)
            {
                _pdfQuality = (int)(value / 50) * 50; ;
                OnPropertyChanged(nameof(PdfQuality));
            }
        }
    }

    private int _mapIconSize = 80;
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

    private int _mapIcon = 1;
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

    private List<string> _iconCategories;
    public List<string> IconCategories
    {
        get => _iconCategories;
        set
        {
            if (_iconCategories != value)
            {
                _iconCategories = value;
                OnPropertyChanged(nameof(IconCategories));
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

    private double _imageExportScale = 0.1;
    public double ImageExportScale
    {
        get => _imageExportScale;
        set
        {
            if (_imageExportScale != value)
            {
                _imageExportScale = Math.Round(value, 2);
                OnPropertyChanged(nameof(ImageExportScale));
            }
        }
    }

    private int _posImageExportSize = 40;
    public int PosImageExportSize
    {
        get => _posImageExportSize;
        set
        {
            if (_posImageExportSize != value)
            {
                _posImageExportSize = value;
                OnPropertyChanged(nameof(PosImageExportSize));
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

    private double _posImageExportScale = 0.5;
    public double PosImageExportScale
    {
        get => _posImageExportScale;
        set
        {
            if (_posImageExportScale != value)
            {
                _posImageExportScale = Math.Round(value, 2);
                OnPropertyChanged(nameof(PosImageExportScale));
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
                    case "EBBE":
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

                    case "Lachs":
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

                    case "Gras":
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

                    case "Ozean":
                        App.Current.Resources["Primary"] = Color.FromArgb("#5e75ad");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryAccent"] = Color.FromArgb("#5e75ad");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#c7d3f2");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryDarkAccent"] = Color.FromArgb("#5e75ad");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["Secondary"] = Color.FromArgb("#c7d3f2");
                        App.Current.Resources["SecondaryDark"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["SecondaryAccent"] = Color.FromArgb("#5e75ad");
                        break;

                    case "Feuer":
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

                    case "Barbie":
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
                    case "Light":
                        App.Current.UserAppTheme = AppTheme.Light; // Setze auf helles Theme
                        break;
                    case "Dark":
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
            IsPlanRotateLocked = this.IsPlanRotateLocked,
            PdfQuality = this.PdfQuality,
            SelectedColorTheme = ColorThemes.IndexOf(this.SelectedColorTheme),
            SelectedAppTheme = AppThemes.IndexOf(this.SelectedAppTheme),
            IconSortCrit = IconSortCrits.IndexOf(this.IconSortCrit),
            PinSortCrit = PinSortCrits.IndexOf(this.PinSortCrit),
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
                this.IsPlanRotateLocked = settings.IsPlanRotateLocked;
                this.PdfQuality = settings.PdfQuality;
                this.SelectedAppTheme = (settings.SelectedAppTheme  < AppThemes.Count)
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
