#pragma warning disable MVVMTK0045
using CommunityToolkit.Mvvm.ComponentModel;
using SnapDoc.Models;
using System.Collections.ObjectModel;
using System.Text.Json;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Services;

public partial class SettingsService : ObservableObject
{
    // --- Singleton ---
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private const string SettingsFileName = "appsettings.ini";
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // --- Theme Dictionaries ---
    private static readonly Dictionary<string, Dictionary<string, string>> ColorThemeMapping = new()
    {
        ["EBBE"] = new()
        {
            ["Primary"] = "#00b0ca",
            ["PrimaryDark"] = "#00b0ca",
            ["PrimaryDarkAccent"] = "#00b0ca",
            ["Secondary"] = "#00b0ca",
        },
        ["Minimalist"] = new()
        {
            ["Primary"] = "#000000",
            ["PrimaryDark"] = "#ededed",
            ["PrimaryDarkAccent"] = "#ffffff",
            ["Secondary"] = "#949494",
        },
        ["Flower"] = new()
        {
            ["Primary"] = "#9f4bcc",
            ["PrimaryDark"] = "#c37de8",
            ["PrimaryDarkAccent"] = "#c37de8",
            ["Secondary"] = "#9f4bcc",
        },
        ["Wine"] = new()
        {
            ["Primary"] = "#9c4e38",
            ["PrimaryDark"] = "#b8705c",
            ["PrimaryDarkAccent"] = "#b8705c",
            ["Secondary"] = "#9c4e38",
        },
        ["Grass"] = new()
        {
            ["Primary"] = "#32a852",
            ["PrimaryDark"] = "#52c771",
            ["PrimaryDarkAccent"] = "#52c771",
            ["Secondary"] = "#32a852",
        },
        ["Fire"] = new()
        {
            ["Primary"] = "#e07a2d",
            ["PrimaryDark"] = "#ed9f64",
            ["PrimaryDarkAccent"] = "#ed9f64",
            ["Secondary"] = "#e07a2d",
        },
        ["Pink"] = new()
        {
            ["Primary"] = "#fc03df",
            ["PrimaryDark"] = "#f763e6",
            ["PrimaryDarkAccent"] = "#f763e6",
            ["Secondary"] = "#fc03df",
        }
    };

    // --- Konstruktor ---
    private SettingsService()
    {
        // --- Standardwerte für Nicht-Nullable-Felder ---
        _selectedColorTheme = string.Empty;
        _selectedAppTheme = string.Empty;
        _selectedTemplate = string.Empty;

        ColorThemes = [.. ColorThemeMapping.Keys];
        AppThemes = [AppResources.hell, AppResources.dunkel];
        AppLanguages = [.. Settings.Languages.Values];
        IconCategories = [AppResources.alle_icons];
        SelectedColorTheme = ColorThemes[0];
        SelectedAppTheme = AppThemes[0];
        IconSortCrit = IconSortCrits[0];
        PinSortCrit = PinSortCrits[0];
        IconCategory = IconCategories[0];
    }

    [ObservableProperty] private string _selectedAppLanguage = Settings.Languages.First().Value;
    [ObservableProperty] private string _appVersion = AppInfo.VersionString;
    [ObservableProperty] private bool _isProjectLoaded = false;
    [ObservableProperty] private string _flyoutHeaderTitle = "by Emch+Berger AG Bern";
    [ObservableProperty] private string _flyoutHeaderDesc = "SnapDoc";
    [ObservableProperty] private string _flyoutHeaderImageThumb = "banner_thumbnail.png";
    [ObservableProperty] private string _flyoutHeaderImage = "";
    [ObservableProperty] private bool _iconGalleryGridView = false;
    [ObservableProperty] private bool _photoGalleryGridView = false;
    [ObservableProperty] private int _maxPdfImageSizeW = 8192;
    [ObservableProperty] private int _maxPdfImageSizeH = 8192;
    [ObservableProperty] private int _fotoThumbSize = 150;
    [ObservableProperty] private int _fotoThumbQuality = 80;
    [ObservableProperty] private int _fotoQuality = 90;
    [ObservableProperty] private int _planPreviewSize = 150;
    [ObservableProperty] private int _fotoPreviewSize = 150;
    [ObservableProperty] private int _iconPreviewSize = 64;
    [ObservableProperty] private int _gridViewMinColumns = 3;
    [ObservableProperty] private double _defaultPinZoom = 2;
    [ObservableProperty] private bool _isPlanRotateLocked = false;
    [ObservableProperty] private bool _isPlanListThumbnails = false;
    [ObservableProperty] private bool _isHideInactivePlans = false;
    [ObservableProperty] private int _pinMinScaleLimit = 80;
    [ObservableProperty] private int _pinMaxScaleLimit = 100;
    [ObservableProperty] private int _maxPdfPixelCount = 30;
    [ObservableProperty] private int _pdfThumbDpi = 72;
    [ObservableProperty] private int _mapIconSize = 85;
    [ObservableProperty] private int _mapIcon = 0;
    [ObservableProperty] private int _mapOverlay1 = 1;
    [ObservableProperty] private int _mapOverlay2 = 5;
    [ObservableProperty] private int _pinPlaceMode = 0;
    [ObservableProperty] private string _iconSortCrit;
    [ObservableProperty] private string _pinSortCrit;
    [ObservableProperty] private string _iconCategory;
    [ObservableProperty] private int _imageExportQuality = 80;
    [ObservableProperty] private double _pinLabelFontSize = 4;
    [ObservableProperty] private string _pinLabelPrefix = "Pos. ";
    [ObservableProperty] private bool _isPlanExport = true;
    [ObservableProperty] private bool _isPosImageExport = true;
    [ObservableProperty] private bool _isImageExport = true;
    [ObservableProperty] private bool _isPinIconExport = true;
    [ObservableProperty] private bool _isFotoOverlayExport = true;
    [ObservableProperty] private bool _isFotoCompressed = true;
    [ObservableProperty] private int _fotoCompressValue = 20;
    [ObservableProperty] private int _imageExportSize = 40;
    [ObservableProperty] private double _pinExportSize = 3.2;
    [ObservableProperty] private int _titleExportSize = 90;
    [ObservableProperty] private int _pinPosExportSize = 25;
    [ObservableProperty] private int _pinPosCropExportSize = 300;
    [ObservableProperty] private double _gpsResponseTimeOut = 10;
    [ObservableProperty] private float _gpsMinTimeUpdate = 2.0f;
    [ObservableProperty] private bool _isGpsActive = false;
    [ObservableProperty] private string _editorTheme = "material-darker";
    [ObservableProperty] private float _polyLineHandleRadius = 10f;
    [ObservableProperty] private float _polyLineHandleTouchRadius = 20f;
    [ObservableProperty] private int _doubleClickThresholdMs = 300;
    [ObservableProperty] private string _polyLineHandleColor = "#808080";
    [ObservableProperty] private string _polyLineStartHandleColor = "#00FF00";
    [ObservableProperty] private byte _polyLineHandleAlpha = 200;
    [ObservableProperty] private Point _customPinOffset = Settings.DefaultCustomPinOffset;
    [ObservableProperty] private string _defaultPinIcon = "a_pin_red.png";
    [ObservableProperty] private string _selectedTemplate;
    [ObservableProperty] private ObservableCollection<string> _templates = [];
    [ObservableProperty] private List<string> _colorThemes;
    [ObservableProperty] private List<string> _appThemes;
    [ObservableProperty] private List<string> _appLanguages;
    [ObservableProperty] private List<string> _iconCategories;
    [ObservableProperty] private List<string> _mapIcons = Settings.MapIcons;

    // Lists
    [ObservableProperty] private List<string> _iconSortCrits =
    [
        AppResources.nach_name,
        AppResources.nach_farbe
    ];
    [ObservableProperty] private List<string> _pinSortCrits =
    [
        AppResources.nach_plan,
        AppResources.nach_pin,
        AppResources.nach_standort,
        AppResources.nach_bezeichnung,
        AppResources.nach_aktiv_inaktiv,
        AppResources.nach_aufnahmedatum,
        AppResources.nach_prioritaet
    ];
    [ObservableProperty] private List<PriorityItem> _priorityItems =
    [
        new() { Key = "", Color = "#000000" },
        new() { Key = AppResources.empfehlung, Color = "#92D050" },
        new() { Key = AppResources.wichtig, Color = "#FFC000" },
        new() { Key = AppResources.kritisch, Color = "#FF0000" }
    ];
    [ObservableProperty] private List<string> _colorList =
    [
        "#009900","#CAFE96","#000000","#7F00FF","#0365DD","#7FBFFF","#7D5F00","#DF7100","#FFBF00",
        "#C565E3","#FABAFC","#79F3F3","#0032CC","#FF0000","#FFFF00","#DFDFDF"
    ];

    // --- Selected ColorTheme ---
    private string _selectedColorTheme;
    public string SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (_selectedColorTheme == value)
                return;
            _selectedColorTheme = value;
            ApplyColorThemeSafe(value);
        }
    }

    private static void ApplyColorThemeSafe(string theme)
    {
        if (App.Current == null)
            return;
        ApplyColorTheme(theme);
    }

    public static void ApplyColorTheme(string theme)
    {
        if (theme == null)
            return;
        if (!ColorThemeMapping.TryGetValue(theme, out var colors))
            return;

        foreach (var kvp in colors)
            Application.Current?.Resources?[kvp.Key] = Color.FromArgb(kvp.Value);
    }

    public void ApplyThemeAfterAppStart()
    {
        if (!string.IsNullOrWhiteSpace(SelectedColorTheme))
            ApplyColorThemeSafe(SelectedColorTheme);
    }

    // --- Selected AppTheme ---
    private string _selectedAppTheme;
    public string SelectedAppTheme
    {
        get => _selectedAppTheme;
        set
        {
            if (_selectedAppTheme == value)
                return;
            _selectedAppTheme = value;
            ApplyAppThemeSafe(value);
        }
    }

    private static void ApplyAppThemeSafe(string theme)
    {
        if (App.Current == null)
            return;
        App.Current.UserAppTheme = theme == AppResources.hell ? AppTheme.Light : AppTheme.Dark;
    }

    public void ApplyAppThemeAfterAppStart()
    {
        if (!string.IsNullOrWhiteSpace(SelectedAppTheme))
            ApplyAppThemeSafe(SelectedAppTheme);
    }

    // --- Save & Load ---
    public void SaveSettings()
    {
        var settings = new SettingsModel
        {
            PinMinScaleLimit = PinMinScaleLimit,
            PinMaxScaleLimit = PinMaxScaleLimit,
            MapIconSize = MapIconSize,
            MapIcon = MapIcon,
            MapOverlay1 = MapOverlay1,
            MapOverlay2 = MapOverlay2,
            PinPlaceMode = PinPlaceMode,
            IsPlanRotateLocked = IsPlanRotateLocked,
            IsPlanListThumbnails = IsPlanListThumbnails,
            IsHideInactivePlans = IsHideInactivePlans,
            MaxPdfPixelCount = MaxPdfPixelCount,
            PdfThumbDpi = PdfThumbDpi,
            SelectedColorTheme = ColorThemes.IndexOf(SelectedColorTheme),
            SelectedAppTheme = AppThemes.IndexOf(SelectedAppTheme),
            SelectedAppLanguage = AppLanguages.IndexOf(SelectedAppLanguage),
            IconSortCrit = IconSortCrits.IndexOf(IconSortCrit),
            PinSortCrit = PinSortCrits.IndexOf(PinSortCrit),
            IconCategory = IconCategories.IndexOf(IconCategory),
            IsPlanExport = IsPlanExport,
            IsPosImageExport = IsPosImageExport,
            IsPinIconExport = IsPinIconExport,
            IsImageExport = IsImageExport,
            IsFotoOverlayExport = IsFotoOverlayExport,
            IsFotoCompressed = IsFotoCompressed,
            FotoCompressValue = FotoCompressValue,
            PinLabelPrefix = PinLabelPrefix,
            PinLabelFontSize = Math.Round(PinLabelFontSize, 1),
            PinExportSize = Math.Round(PinExportSize, 1),
            ImageExportSize = ImageExportSize,
            PinPosExportSize = PinPosExportSize,
            PinPosCropExportSize = PinPosCropExportSize,
            TitleExportSize = TitleExportSize,
            IconGalleryGridView = IconGalleryGridView,
            PhotoGalleryGridView = PhotoGalleryGridView,
            MaxPdfImageSizeW = MaxPdfImageSizeW,
            MaxPdfImageSizeH = MaxPdfImageSizeH,
            FotoThumbSize = FotoThumbSize,
            FotoThumbQuality = FotoThumbQuality,
            FotoQuality = FotoQuality,
            PlanPreviewSize = PlanPreviewSize,
            FotoPreviewSize = FotoPreviewSize,
            IconPreviewSize = IconPreviewSize,
            GridViewMinColumns = GridViewMinColumns,
            DefaultPinZoom = DefaultPinZoom,
            GpsResponseTimeOut = GpsResponseTimeOut,
            GpsMinTimeUpdate = GpsMinTimeUpdate,
            IsGpsActive = IsGpsActive,
            EditorTheme = EditorTheme,
            PolyLineHandleRadius = PolyLineHandleRadius,
            PolyLineHandleTouchRadius = PolyLineHandleTouchRadius,
            DoubleClickThresholdMs = DoubleClickThresholdMs,
            PolyLineHandleColor = PolyLineHandleColor,
            PolyLineStartHandleColor = PolyLineStartHandleColor,
            PolyLineHandleAlpha = PolyLineHandleAlpha,
            CustomPinOffset = CustomPinOffset,
            DefaultPinIcon = DefaultPinIcon,
            ColorList = ColorList,
            PriorityItems = PriorityItems,
        };
        File.WriteAllText(Path.Combine(Settings.DataDirectory, SettingsFileName), JsonSerializer.Serialize(settings, _jsonOptions));
    }

    public void LoadSettings()
    {
        var filePath = Path.Combine(Settings.DataDirectory, SettingsFileName);
        if (!File.Exists(filePath)) return;

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{')) return;

            var settings = JsonSerializer.Deserialize<SettingsModel>(json);
            if (settings == null) return;

            PinMinScaleLimit = settings.PinMinScaleLimit;
            PinMaxScaleLimit = settings.PinMaxScaleLimit;
            MapIconSize = settings.MapIconSize;
            MapIcon = settings.MapIcon;
            MapOverlay1 = settings.MapOverlay1;
            MapOverlay2 = settings.MapOverlay2;
            PinPlaceMode = settings.PinPlaceMode;
            IsPlanRotateLocked = settings.IsPlanRotateLocked;
            IsPlanListThumbnails = settings.IsPlanListThumbnails;
            IsHideInactivePlans = settings.IsHideInactivePlans;
            MaxPdfPixelCount = settings.MaxPdfPixelCount;
            PdfThumbDpi = settings.PdfThumbDpi;
            SelectedAppTheme = (settings.SelectedAppTheme < AppThemes.Count) ? AppThemes[settings.SelectedAppTheme] : AppThemes[0];
            SelectedColorTheme = (settings.SelectedColorTheme < ColorThemes.Count) ? ColorThemes[settings.SelectedColorTheme] : ColorThemes[0];
            SelectedAppLanguage = (settings.SelectedAppLanguage < AppLanguages.Count) ? AppLanguages[settings.SelectedAppLanguage] : AppLanguages[0];
            IconCategory = (settings.IconCategory < IconCategories.Count && settings.IconCategory > 0) ? IconCategories[settings.IconCategory] : IconCategories[0];
            IsPlanExport = settings.IsPlanExport;
            IsPosImageExport = settings.IsPosImageExport;
            IsPinIconExport = settings.IsPinIconExport;
            IsImageExport = settings.IsImageExport;
            IsFotoOverlayExport = settings.IsFotoOverlayExport;
            IsFotoCompressed = settings.IsFotoCompressed;
            FotoCompressValue = settings.FotoCompressValue;
            PinLabelPrefix = settings.PinLabelPrefix ?? string.Empty;
            PinLabelFontSize = settings.PinLabelFontSize;
            PinExportSize = settings.PinExportSize;
            ImageExportSize = settings.ImageExportSize;
            PinPosExportSize = settings.PinPosExportSize;
            PinPosCropExportSize = settings.PinPosCropExportSize;
            TitleExportSize = settings.TitleExportSize;
            IconGalleryGridView = settings.IconGalleryGridView;
            PhotoGalleryGridView = settings.PhotoGalleryGridView;
            MaxPdfImageSizeW = settings.MaxPdfImageSizeW;
            MaxPdfImageSizeH = settings.MaxPdfImageSizeH;
            FotoThumbSize = settings.FotoThumbSize;
            FotoThumbQuality = settings.FotoThumbQuality;
            FotoQuality = settings.FotoQuality;
            PlanPreviewSize = settings.PlanPreviewSize;
            FotoPreviewSize = settings.FotoPreviewSize;
            IconPreviewSize = settings.IconPreviewSize;
            GridViewMinColumns = settings.GridViewMinColumns;
            DefaultPinZoom = settings.DefaultPinZoom;
            GpsResponseTimeOut = settings.GpsResponseTimeOut;
            GpsMinTimeUpdate = settings.GpsMinTimeUpdate;
            IsGpsActive = settings.IsGpsActive;
            EditorTheme = settings.EditorTheme ?? string.Empty;
            PolyLineHandleRadius = settings.PolyLineHandleRadius;
            PolyLineHandleTouchRadius = settings.PolyLineHandleTouchRadius;
            DoubleClickThresholdMs = settings.DoubleClickThresholdMs;
            PolyLineHandleColor = settings.PolyLineHandleColor ?? string.Empty;
            PolyLineStartHandleColor = settings.PolyLineStartHandleColor ?? string.Empty;
            PolyLineHandleAlpha = settings.PolyLineHandleAlpha;
            CustomPinOffset = settings.CustomPinOffset;
            DefaultPinIcon = settings.DefaultPinIcon ?? string.Empty;
            ColorList = settings.ColorList ?? ColorList;
            PriorityItems = settings.PriorityItems ?? PriorityItems;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Fehler beim Laden der Einstellungen: {ex.Message}");
        }
    }

    public void ResetSettingsToDefaults()
    {
        // Einfach den Konstruktor einmal wieder aufrufen
        var defaultSettings = new SettingsService();
        PinMinScaleLimit = defaultSettings.PinMinScaleLimit;
        PinMaxScaleLimit = defaultSettings.PinMaxScaleLimit;
        MapIconSize = defaultSettings.MapIconSize;
        MapIcon = defaultSettings.MapIcon;
        MapOverlay1 = defaultSettings.MapOverlay1;
        MapOverlay2 = defaultSettings.MapOverlay2;
        PinPlaceMode = defaultSettings.PinPlaceMode;
        IsPlanRotateLocked = defaultSettings.IsPlanRotateLocked;
        IsPlanListThumbnails = defaultSettings.IsPlanListThumbnails;
        IsHideInactivePlans = defaultSettings.IsHideInactivePlans;
        MaxPdfPixelCount = defaultSettings.MaxPdfPixelCount;
        PdfThumbDpi = defaultSettings.PdfThumbDpi;
        SelectedColorTheme = defaultSettings.SelectedColorTheme;
        SelectedAppTheme = defaultSettings.SelectedAppTheme;
        SelectedAppLanguage = defaultSettings.SelectedAppLanguage;
        IconSortCrit = defaultSettings.IconSortCrit;
        PinSortCrit = defaultSettings.PinSortCrit;
        IconCategory = defaultSettings.IconCategory;
        IsPlanExport = defaultSettings.IsPlanExport;
        IsPosImageExport = defaultSettings.IsPosImageExport;
        IsPinIconExport = defaultSettings.IsPinIconExport;
        IsImageExport = defaultSettings.IsImageExport;
        IsFotoOverlayExport = defaultSettings.IsFotoOverlayExport;
        IsFotoCompressed = defaultSettings.IsFotoCompressed;
        FotoCompressValue = defaultSettings.FotoCompressValue;
        PinLabelPrefix = defaultSettings.PinLabelPrefix;
        PinLabelFontSize = defaultSettings.PinLabelFontSize;
        PinExportSize = defaultSettings.PinExportSize;
        ImageExportSize = defaultSettings.ImageExportSize;
        PinPosExportSize = defaultSettings.PinPosExportSize;
        PinPosCropExportSize = defaultSettings.PinPosCropExportSize;
        TitleExportSize = defaultSettings.TitleExportSize;
        IconGalleryGridView = defaultSettings.IconGalleryGridView;
        PhotoGalleryGridView = defaultSettings.PhotoGalleryGridView;
        MaxPdfImageSizeW = defaultSettings.MaxPdfImageSizeW;
        MaxPdfImageSizeH = defaultSettings.MaxPdfImageSizeH;
        FotoThumbSize = defaultSettings.FotoThumbSize;
        FotoThumbQuality = defaultSettings.FotoThumbQuality;
        FotoQuality = defaultSettings.FotoQuality;
        PlanPreviewSize = defaultSettings.PlanPreviewSize;
        FotoPreviewSize = defaultSettings.FotoPreviewSize;
        IconPreviewSize = defaultSettings.IconPreviewSize;
        GridViewMinColumns = defaultSettings.GridViewMinColumns;
        DefaultPinZoom = defaultSettings.DefaultPinZoom;
        GpsResponseTimeOut = defaultSettings.GpsResponseTimeOut;
        GpsMinTimeUpdate = defaultSettings.GpsMinTimeUpdate;
        IsGpsActive = defaultSettings.IsGpsActive;
        EditorTheme = defaultSettings.EditorTheme;
        PolyLineHandleRadius = defaultSettings.PolyLineHandleRadius;
        PolyLineHandleTouchRadius = defaultSettings.PolyLineHandleTouchRadius;
        DoubleClickThresholdMs = defaultSettings.DoubleClickThresholdMs;
        PolyLineHandleColor = defaultSettings.PolyLineHandleColor;
        PolyLineStartHandleColor = defaultSettings.PolyLineStartHandleColor;
        PolyLineHandleAlpha = defaultSettings.PolyLineHandleAlpha;
        CustomPinOffset = defaultSettings.CustomPinOffset;
        DefaultPinIcon = defaultSettings.DefaultPinIcon;
        ColorList = [.. defaultSettings.ColorList];
        PriorityItems = [.. defaultSettings.PriorityItems];
    }
}
#pragma warning restore MVVMTK0045