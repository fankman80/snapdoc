#nullable disable

using bsm24.Models;
using System.ComponentModel;
using System.Text.Json;

namespace bsm24.Services;
public partial class SettingsService : INotifyPropertyChanged
{
    public static SettingsService Instance { get; } = new SettingsService();

    private const string SettingsFileName = "appsettings.ini";

    private SettingsService()
    {
        Themes = ["EBBE", "Lachs", "Gras", "Ozean", "Feuer", "Flower", "Barbie"];
        DarkMode = ["Light", "Dark"];
        SelectedTheme = Themes[0]; // Standardauswahl
        SelectedDarkMode = DarkMode[0]; // Standardauswahl
    }

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

#if WINDOWS
    private double _pinScaleLimit = 100;
#endif

#if ANDROID
    private double _pinScaleLimit = 50;
#endif

    public double PinScaleLimit
    {
        get => _pinScaleLimit;
        set
        {
            if (_pinScaleLimit != value)
            {
                _pinScaleLimit = Math.Round(value, 0);
                OnPropertyChanged(nameof(PinScaleLimit));
            }
        }
    }

    private int _pdfQuality = 300;
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

    #region
    // Export Settings

    private int _imageExportQuality = 90;
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

    private int _planLabelSize = 64;
    public int PlanLabelSize
    {
        get => _planLabelSize;
        set
        {
            if (_planLabelSize != value)
            {
                _planLabelSize = value;
                OnPropertyChanged(nameof(PlanLabelSize));
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

    private int _planExportSize = 140;
    public int PlanExportSize
    {
        get => _planExportSize;
        set
        {
            if (_planExportSize != value)
            {
                _planExportSize = value;
                OnPropertyChanged(nameof(PlanExportSize));
            }
        }
    }

    private int _titleExportSize = 100;
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


    #region
    private List<string> _themes;
    public List<string> Themes
    {
        get => _themes;
        set
        {
            _themes = value;
            OnPropertyChanged(nameof(Themes));
        }
    }

    private string _selectedTheme;
    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme != value)
            {
                _selectedTheme = value;
                OnPropertyChanged(nameof(SelectedTheme));

                // Logik für das Anwenden der Farben basierend auf der Auswahl
                switch (_selectedTheme)
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

    private List<string> _darkMode;
    public List<string> DarkMode
    {
        get => _darkMode;
        set
        {
            _darkMode = value;
            OnPropertyChanged(nameof(DarkMode));
        }
    }

    private string _selectedDarkMode;
    public string SelectedDarkMode
    {
        get => _selectedDarkMode;
        set
        {
            if (_selectedDarkMode != value)
            {
                _selectedDarkMode = value;
                OnPropertyChanged(nameof(SelectedDarkMode));

                // Logik für das Anwenden der Farben basierend auf der Auswahl
                switch (_selectedDarkMode)
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
    #endregion

    public void SaveSettings()
    {
        var settings = new SettingsModel
        {
            PinScaleLimit = this.PinScaleLimit,
            IsPlanRotateLocked = this.IsPlanRotateLocked,
            PdfQuality = this.PdfQuality,
            SelectedTheme = this.SelectedTheme,
            SelectedDarkMode = this.SelectedDarkMode
        };

        var json = JsonSerializer.Serialize(settings, GetOptions());
        File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, SettingsFileName), json);
    }

    public void LoadSettings()
    {
        var filePath = Path.Combine(FileSystem.AppDataDirectory, SettingsFileName);
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<SettingsModel>(json);
            if (settings != null)
            {
                this.PinScaleLimit = settings.PinScaleLimit;
                this.IsPlanRotateLocked = settings.IsPlanRotateLocked;
                this.PdfQuality = settings.PdfQuality;
                this.SelectedTheme = settings.SelectedTheme;
                this.SelectedDarkMode = settings.SelectedDarkMode;
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
