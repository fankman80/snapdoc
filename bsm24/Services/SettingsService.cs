#nullable disable

using System.ComponentModel;

namespace bsm24.Services;
public partial class SettingsService : INotifyPropertyChanged
{
    private static SettingsService _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private SettingsService()
    {
        ImageQuality = "300";
        Themes = ["EBBE", "Lachs", "Gras", "Ozean", "Feuer", "Barbie"];
        DarkMode = ["Light", "Dark", "System Default"];
        SelectedTheme = Themes[0]; // Standardauswahl
        SelectedDarkMode = DarkMode[0]; // Standardauswahl
    }

    private string _imageQuality;
    public string ImageQuality
    {
        get => _imageQuality;
        set
        {
            if (_imageQuality == value) return;
            _imageQuality = value;
            OnPropertyChanged(_imageQuality);
        }
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

    private string _isPlanRotateLocked = "false";
    public string IsPlanRotateLocked
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
    private string _pinScaleLimit = "0,8";
#endif

#if ANDROID
    private string _pinScaleLimit = "0,5";
#endif

    public string PinScaleLimit
    {
        get => _pinScaleLimit;
        set
        {
            if (_pinScaleLimit != value)
            {
                _pinScaleLimit = Math.Round(Convert.ToDouble(value), 1).ToString();
                OnPropertyChanged(nameof(PinScaleLimit));
            }
        }
    }

    private string _pdfQuality = "300";
    public string PdfQuality
    {
        get => _pdfQuality;
        set
        {
            if (_pdfQuality != value)
            {
                _pdfQuality = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(PdfQuality));
            }
        }
    }

    #region
    // Export Settings

    private string _imageExportQuality = "90";
    public string ImageExportQuality
    {
        get => _imageExportQuality;
        set
        {
            if (_imageExportQuality != value)
            {
                _imageExportQuality = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(ImageExportQuality));
            }
        }
    }

    private string _isPlanExport = "true";
    public string IsPlanExport
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

    private string _isPosImageExport = "true";
    public string IsPosImageExport
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

    private string _isImageExport = "true";
    public string IsImageExport
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

    private string _isPinIconExport = "true";
    public string IsPinIconExport
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

    private string _imageExportSize = "40";
    public string ImageExportSize
    {
        get => _imageExportSize;
        set
        {
            if (_imageExportSize != value)
            {
                _imageExportSize = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(ImageExportSize));
            }
        }
    }

    private string _planExportSize = "140";
    public string PlanExportSize
    {
        get => _planExportSize;
        set
        {
            if (_planExportSize != value)
            {
                _planExportSize = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(PlanExportSize));
            }
        }
    }

    private string _imageExportScale = "0,1";
    public string ImageExportScale
    {
        get => _imageExportScale;
        set
        {
            if (_imageExportScale != value)
            {
                _imageExportScale = Math.Round(Convert.ToDouble(value), 2).ToString();
                OnPropertyChanged(nameof(ImageExportScale));
            }
        }
    }

    private string _posImageExportSize = "600";
    public string PosImageExportSize
    {
        get => _posImageExportSize;
        set
        {
            if (_posImageExportSize != value)
            {
                _posImageExportSize = Math.Round(Convert.ToDouble(value), 0).ToString();
                OnPropertyChanged(nameof(PosImageExportSize));
            }
        }
    }

    private string _posImageExportScale = "0,5";
    public string PosImageExportScale
    {
        get => _posImageExportScale;
        set
        {
            if (_posImageExportScale != value)
            {
                _posImageExportScale = Math.Round(Convert.ToDouble(value), 2).ToString();
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
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#ededed");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
                        break;

                    case "Lachs":
                        App.Current.Resources["Primary"] = Color.FromArgb("#9c4e38");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#c9a59b");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
                        break;

                    case "Gras":
                        App.Current.Resources["Primary"] = Color.FromArgb("#32a852");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#73b572");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
                        break;

                    case "Ozean":
                        App.Current.Resources["Primary"] = Color.FromArgb("#5e75ad");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#9caedb");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
                        break;

                    case "Feuer":
                        App.Current.Resources["Primary"] = Color.FromArgb("#e07a2d");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#edba93");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
                        break;

                    case "Barbie":
                        App.Current.Resources["Primary"] = Color.FromArgb("#fc03df");
                        App.Current.Resources["PrimaryText"] = Color.FromArgb("#000000");
                        App.Current.Resources["PrimaryDark"] = Color.FromArgb("#f2a7ea");
                        App.Current.Resources["PrimaryDarkText"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackground"] = Color.FromArgb("#ffffff");
                        App.Current.Resources["PrimaryBackgroundDark"] = Color.FromArgb("#404040");
                        App.Current.Resources["SecondaryBackgroundDark"] = Color.FromArgb("#404040");
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
                    case "System Default":
                        App.Current.UserAppTheme = AppTheme.Unspecified; // Verwende das systemweite Theme
                        break;
                }
            }
        }
    }
    #endregion


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}