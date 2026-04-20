using CommunityToolkit.Mvvm.ComponentModel;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using System.Collections.ObjectModel;
using System.Text.Json;

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

        ColorThemes = [.. ColorThemeMapping.Keys];
        AppThemes = [AppResources.hell, AppResources.dunkel];
        AppLanguages = [.. Settings.Languages.Values];
        AppCameraTools = [.. Settings.CameraTools];
        IconCategories = [AppResources.alle_icons];
        SelectedColorTheme = ColorThemes[0];
        SelectedAppTheme = AppThemes[0];
        IconSortCrit = IconSortCrits[0];
        PinSortCrit = PinSortCrits[0];
        IconCategory = IconCategories[0];
    }

    [ObservableProperty] public partial string SelectedAppLanguage { get; set; } = Settings.Languages.First().Value;
    [ObservableProperty] public partial string SelectedCameraTool { get; set; } = Settings.CameraTools.First();
    [ObservableProperty] public partial int FlashMode { get; set; } = 0;
    [ObservableProperty] public partial double CaptureRatio { get; set; } = 1.33;
    [ObservableProperty] public partial string AppVersion { get; set; } = AppInfo.VersionString;
    [ObservableProperty] public partial bool IsProjectLoaded { get; set; } = false;
    [ObservableProperty] public partial string FlyoutHeaderTitle { get; set; } = "by Emch+Berger AG Bern";
    [ObservableProperty] public partial string FlyoutHeaderDesc { get; set; } = "SnapDoc";
    [ObservableProperty] public partial string FlyoutHeaderImageThumb { get; set; } = "banner_thumbnail.png";
    [ObservableProperty] public partial string FlyoutHeaderImage { get; set; } = "";
    [ObservableProperty] public partial bool IconGalleryGridView { get; set; } = false;
    [ObservableProperty] public partial bool PhotoGalleryGridView { get; set; } = false;
    [ObservableProperty] public partial int MaxPdfImageSizeW { get; set; } = 8192;
    [ObservableProperty] public partial int MaxPdfImageSizeH { get; set; } = 8192;
    [ObservableProperty] public partial int FotoThumbSize { get; set; } = 150;
    [ObservableProperty] public partial int FotoThumbQuality { get; set; } = 80;
    [ObservableProperty] public partial int FotoQuality { get; set; } = 90;
    [ObservableProperty] public partial int PlanPreviewSize { get; set; } = 150;
    [ObservableProperty] public partial int FotoPreviewSize { get; set; } = 150;
    [ObservableProperty] public partial int IconPreviewSize { get; set; } = 64;
    [ObservableProperty] public partial int GridViewMinColumns { get; set; } = 3;
    [ObservableProperty] public partial double DefaultPinZoom { get; set; } = 2;
    [ObservableProperty] public partial bool IsPlanRotateLocked { get; set; } = false;
    [ObservableProperty] public partial bool IsPlanListThumbnails { get; set; } = false;
    [ObservableProperty] public partial bool IsHideInactivePlans { get; set; } = false;
    [ObservableProperty] public partial bool IsPinAutoLock { get; set; } = false;
    [ObservableProperty] public partial int PinMinScaleLimit { get; set; } = 80;
    [ObservableProperty] public partial int PinMaxScaleLimit { get; set; } = 100;
    [ObservableProperty] public partial int MaxPdfPixelCount { get; set; } = 30;
    [ObservableProperty] public partial int PdfThumbDpi { get; set; } = 72;
    [ObservableProperty] public partial int MapIconSize { get; set; } = 85;
    [ObservableProperty] public partial int MapIcon { get; set; } = 0;
    [ObservableProperty] public partial int MapOverlay1 { get; set; } = 1;
    [ObservableProperty] public partial int MapOverlay2 { get; set; } = 5;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsButtonActuallyVisible))] public partial bool IsPinPlaceBtnManualHide { get; set; } = false;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsButtonActuallyVisible))] public partial int PinPlaceMode { get; set; } = 0;
    public bool IsButtonActuallyVisible => PinPlaceMode != 2 && !IsPinPlaceBtnManualHide;
    [ObservableProperty] public partial double PinDuplicateOffset { get; set; } = 0.01;
    [ObservableProperty] public partial string IconSortCrit { get; set; }
    [ObservableProperty] public partial string PinSortCrit { get; set; }
    [ObservableProperty] public partial string IconCategory { get; set; }
    [ObservableProperty] public partial int ImageExportQuality { get; set; } = 80;
    [ObservableProperty] public partial double PinLabelFontSize { get; set; } = 4;
    [ObservableProperty] public partial string PinLabelPrefix { get; set; } = "Pos. ";
    [ObservableProperty] public partial bool IsPlanExport { get; set; } = true;
    [ObservableProperty] public partial bool IsPosImageExport { get; set; } = true;
    [ObservableProperty] public partial bool IsImageExport { get; set; } = true;
    [ObservableProperty] public partial bool IsPinIconExport { get; set; } = true;
    [ObservableProperty] public partial bool IsFotoOverlayExport { get; set; } = true;
    [ObservableProperty] public partial int FotoCompressValue { get; set; } = 20;
    [ObservableProperty] public partial double PinExportSize { get; set; } = 3.2;
    [ObservableProperty] public partial int PinPosExportSize { get; set; } = 25;
    [ObservableProperty] public partial int PinPosCropExportSize { get; set; } = 300;
    [ObservableProperty] public partial double GpsResponseTimeOut { get; set; } = 10;
    [ObservableProperty] public partial float GpsMinTimeUpdate { get; set; } = 2.0f;
    [ObservableProperty] public partial bool IsGpsActive { get; set; } = false;
    [ObservableProperty] public partial string EditorTheme { get; set; } = "material-darker";
    [ObservableProperty] public partial float PolyLineHandleRadius { get; set; } = 10f;
    [ObservableProperty] public partial float PolyLineHandleTouchRadius { get; set; } = 20f;
    [ObservableProperty] public partial int DoubleClickThresholdMs { get; set; } = 300;
    [ObservableProperty] public partial string PolyLineHandleColor { get; set; } = "#808080";
    [ObservableProperty] public partial string PolyLineStartHandleColor { get; set; } = "#00FF00";
    [ObservableProperty] public partial byte PolyLineHandleAlpha { get; set; } = 127;
    [ObservableProperty] public partial Point CustomPinOffset { get; set; } = new(0,0);
    [ObservableProperty] public partial string DefaultPinIcon { get; set; } = "a_pin_red.png";
    [ObservableProperty] public partial string? SelectedTemplate { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> Templates { get; set; } = [];
    [ObservableProperty] public partial List<string> ColorThemes { get; set; }
    [ObservableProperty] public partial List<string> AppThemes { get; set; }
    [ObservableProperty] public partial List<string> AppLanguages { get; set; }
    [ObservableProperty] public partial List<string> AppCameraTools { get; set; }
    [ObservableProperty] public partial List<string> IconCategories { get; set; }
    [ObservableProperty] public partial List<string> MapIcons { get; set; } = Settings.MapIcons;

    // Lists
    [ObservableProperty] public partial List<string> IconSortCrits { get; set; } =
    [
        AppResources.nach_name,
        AppResources.nach_farbe
    ];
    [ObservableProperty] public partial List<string> PinSortCrits { get; set; } =
    [
        AppResources.nach_plan,
        AppResources.nach_pin,
        AppResources.nach_standort,
        AppResources.nach_bezeichnung,
        AppResources.nach_aktiv_inaktiv,
        AppResources.nach_aufnahmedatum,
        AppResources.nach_prioritaet
    ];
    [ObservableProperty] public partial List<PriorityItem> PriorityItems { get; set; } =
    [
        new() { Key = "", Color = null },
        new() { Key = AppResources.empfehlung, Color = "#92D050" },
        new() { Key = AppResources.wichtig, Color = "#FFC000" },
        new() { Key = AppResources.kritisch, Color = "#FF0000" }
    ];
    [ObservableProperty] public partial List<string> ColorList { get; set; } =
    [
        "#009900","#CAFE96","#000000","#7F00FF","#0365DD","#7FBFFF","#7D5F00","#DF7100","#FFBF00",
        "#C565E3","#FABAFC","#79F3F3","#0032CC","#FF0000","#FFFF00","#DFDFDF"
    ];
    [ObservableProperty] public partial List<StylePickerItem> StyleTemplateItems { get; set; } =
    [
        new() {
            Text = "Text",
            BackgroundColor = Colors.LightGreen.WithAlpha(0.5f).ToArgbHex(true),
            BorderColor = Colors.Green.WithAlpha(1f).ToArgbHex(true),
            TextColor = Colors.DarkGreen.WithAlpha(1f).ToArgbHex(true),
            LineWidth = 3,
            StrokeStyle = "",
            },
        new() {
            Text = "Text",
            BackgroundColor = Colors.LightYellow.WithAlpha(0.5f).ToArgbHex(true),
            BorderColor = Colors.Goldenrod.WithAlpha(1f).ToArgbHex(true),
            TextColor = Colors.DarkGoldenrod.WithAlpha(1f).ToArgbHex(true),
            LineWidth = 6,
            StrokeStyle = "4 2",
        },
        new() {
            Text = "Text",
            BackgroundColor = Colors.LightPink.WithAlpha(0.5f).ToArgbHex(true),
            BorderColor = Colors.Red.WithAlpha(1f).ToArgbHex(true),
            TextColor = Colors.DarkRed.WithAlpha(1f).ToArgbHex(true),
            LineWidth = 2,
            StrokeStyle = "4 2 8 2",
            },
        new() {
            Text = "Text",
            BackgroundColor = Colors.OrangeRed.WithAlpha(0.5f).ToArgbHex(true),
            BorderColor = Colors.DarkRed.WithAlpha(1f).ToArgbHex(true),
            TextColor = Colors.White.WithAlpha(1f).ToArgbHex(true),
            LineWidth = 0,
            StrokeStyle = "4 4",
            }
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
            PinDuplicateOffset = PinDuplicateOffset,
            IsPlanRotateLocked = IsPlanRotateLocked,
            IsPlanListThumbnails = IsPlanListThumbnails,
            IsHideInactivePlans = IsHideInactivePlans,
            IsPinAutoLock = IsPinAutoLock,
            MaxPdfPixelCount = MaxPdfPixelCount,
            PdfThumbDpi = PdfThumbDpi,
            SelectedColorTheme = ColorThemes.IndexOf(SelectedColorTheme),
            SelectedAppTheme = AppThemes.IndexOf(SelectedAppTheme),
            SelectedAppLanguage = AppLanguages.IndexOf(SelectedAppLanguage),
            SelectedCameraTool = AppCameraTools.IndexOf(SelectedCameraTool),
            CaptureRatio = CaptureRatio,
            IconSortCrit = IconSortCrits.IndexOf(IconSortCrit),
            PinSortCrit = PinSortCrits.IndexOf(PinSortCrit),
            IconCategory = IconCategories.IndexOf(IconCategory),
            IsPlanExport = IsPlanExport,
            IsPosImageExport = IsPosImageExport,
            IsPinIconExport = IsPinIconExport,
            IsImageExport = IsImageExport,
            IsFotoOverlayExport = IsFotoOverlayExport,
            FotoCompressValue = FotoCompressValue,
            PinLabelPrefix = PinLabelPrefix,
            PinLabelFontSize = Math.Round(PinLabelFontSize, 1),
            PinExportSize = Math.Round(PinExportSize, 1),
            PinPosExportSize = PinPosExportSize,
            PinPosCropExportSize = PinPosCropExportSize,
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
            StyleTemplateItems = StyleTemplateItems,
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
            PinDuplicateOffset = settings.PinDuplicateOffset;
            IsPlanRotateLocked = settings.IsPlanRotateLocked;
            IsPlanListThumbnails = settings.IsPlanListThumbnails;
            IsHideInactivePlans = settings.IsHideInactivePlans;
            IsPinAutoLock = settings.IsPinAutoLock;
            MaxPdfPixelCount = settings.MaxPdfPixelCount;
            PdfThumbDpi = settings.PdfThumbDpi;
            SelectedAppTheme = (settings.SelectedAppTheme < AppThemes.Count) ? AppThemes[settings.SelectedAppTheme] : AppThemes[0];
            SelectedColorTheme = (settings.SelectedColorTheme < ColorThemes.Count) ? ColorThemes[settings.SelectedColorTheme] : ColorThemes[0];
            SelectedAppLanguage = (settings.SelectedAppLanguage < AppLanguages.Count) ? AppLanguages[settings.SelectedAppLanguage] : AppLanguages[0];
            SelectedCameraTool = (settings.SelectedCameraTool < AppCameraTools.Count) ? AppCameraTools[settings.SelectedCameraTool] : AppCameraTools[0];
            CaptureRatio = settings.CaptureRatio;
            IconCategory = (settings.IconCategory < IconCategories.Count && settings.IconCategory > 0) ? IconCategories[settings.IconCategory] : IconCategories[0];
            IsPlanExport = settings.IsPlanExport;
            IsPosImageExport = settings.IsPosImageExport;
            IsPinIconExport = settings.IsPinIconExport;
            IsImageExport = settings.IsImageExport;
            IsFotoOverlayExport = settings.IsFotoOverlayExport;
            FotoCompressValue = settings.FotoCompressValue;
            PinLabelPrefix = settings.PinLabelPrefix ?? string.Empty;
            PinLabelFontSize = settings.PinLabelFontSize;
            PinExportSize = settings.PinExportSize;
            PinPosExportSize = settings.PinPosExportSize;
            PinPosCropExportSize = settings.PinPosCropExportSize;
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
            StyleTemplateItems = settings.StyleTemplateItems ?? StyleTemplateItems;
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
        PinDuplicateOffset = defaultSettings.PinDuplicateOffset;
        IsPlanRotateLocked = defaultSettings.IsPlanRotateLocked;
        IsPlanListThumbnails = defaultSettings.IsPlanListThumbnails;
        IsHideInactivePlans = defaultSettings.IsHideInactivePlans;
        IsPinAutoLock = defaultSettings.IsPinAutoLock;
        MaxPdfPixelCount = defaultSettings.MaxPdfPixelCount;
        PdfThumbDpi = defaultSettings.PdfThumbDpi;
        SelectedColorTheme = defaultSettings.SelectedColorTheme;
        SelectedAppTheme = defaultSettings.SelectedAppTheme;
        SelectedAppLanguage = defaultSettings.SelectedAppLanguage;
        SelectedCameraTool  = defaultSettings.SelectedCameraTool;
        CaptureRatio = defaultSettings.CaptureRatio;
        IconSortCrit = defaultSettings.IconSortCrit;
        PinSortCrit = defaultSettings.PinSortCrit;
        IconCategory = defaultSettings.IconCategory;
        IsPlanExport = defaultSettings.IsPlanExport;
        IsPosImageExport = defaultSettings.IsPosImageExport;
        IsPinIconExport = defaultSettings.IsPinIconExport;
        IsImageExport = defaultSettings.IsImageExport;
        IsFotoOverlayExport = defaultSettings.IsFotoOverlayExport;
        FotoCompressValue = defaultSettings.FotoCompressValue;
        PinLabelPrefix = defaultSettings.PinLabelPrefix;
        PinLabelFontSize = defaultSettings.PinLabelFontSize;
        PinExportSize = defaultSettings.PinExportSize;
        PinPosExportSize = defaultSettings.PinPosExportSize;
        PinPosCropExportSize = defaultSettings.PinPosCropExportSize;
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
        StyleTemplateItems = [.. defaultSettings.StyleTemplateItems];
    }
}