#nullable disable
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

    public string ProjectPath { get; set; }

    [ObservableProperty] public partial double OsBaseScale { get; set; } = Settings.OsBaseScale;
    [ObservableProperty] public partial string DefaultJson { get; set; } = "snapdoc_data.json";
    [ObservableProperty] public partial string SelectedAppLanguage { get; set; } = Settings.Languages.First().Value;
    [ObservableProperty] public partial string SelectedCameraTool { get; set; } = Settings.CameraTools.First();
    [ObservableProperty] public partial int FlashMode { get; set; } = 0;
    [ObservableProperty] public partial double CaptureRatio { get; set; } = 1.33;
    [ObservableProperty] public partial string AppVersion { get; set; } = AppInfo.VersionString;
    [ObservableProperty] public partial bool IsProjectLoaded { get; set; } = false;
    [ObservableProperty] public partial string LastPinId { get; set; }
    [ObservableProperty] public partial string FlyoutHeaderTitle { get; set; } = "by Emch+Berger AG Bern";
    [ObservableProperty] public partial string FlyoutHeaderDesc { get; set; } = "SnapDoc";
    [ObservableProperty] public partial string FlyoutHeaderImageThumb { get; set; } = "banner_thumbnail.png";
    [ObservableProperty] public partial string FlyoutHeaderImage { get; set; } = "";
    [ObservableProperty] public partial bool IconGalleryGridView { get; set; } = false;
    [ObservableProperty] public partial bool PhotoGalleryGridView { get; set; } = false;
    [ObservableProperty] public partial int FotoThumbSize { get; set; } = 150;
    [ObservableProperty] public partial int FotoThumbQuality { get; set; } = 75;
    [ObservableProperty] public partial int FotoQuality { get; set; } = 80;
    [ObservableProperty] public partial int PlanQuality { get; set; } = 90;
    [ObservableProperty] public partial int PlanPreviewSize { get; set; } = 150;
    [ObservableProperty] public partial int PlanThumbSize { get; set; } = 512;
    [ObservableProperty] public partial int FotoPreviewSize { get; set; } = 150;
    [ObservableProperty] public partial int IconPreviewSize { get; set; } = 64;
    [ObservableProperty] public partial int GridViewMinColumns { get; set; } = 3;
    [ObservableProperty] public partial double DefaultPinZoom { get; set; } = 4;
    [ObservableProperty] public partial bool IsPlanRotateLocked { get; set; } = false;
    [ObservableProperty] public partial bool IsPlanListThumbnails { get; set; } = false;
    [ObservableProperty] public partial bool IsHideInactivePlans { get; set; } = false;
    [ObservableProperty] public partial bool IsPinAutoLock { get; set; } = false;
    [ObservableProperty] public partial int PinMinScaleLimit { get; set; } = 60;
    [ObservableProperty] public partial int PinMaxScaleLimit { get; set; } = 100;
    [ObservableProperty] public partial int PdfFullViewDpi { get; set; } = 360;
    [ObservableProperty] public partial int PdfThumbDpi { get; set; } = 72;
    [ObservableProperty] public partial int MapIconSize { get; set; } = 85;
    [ObservableProperty] public partial int MapIcon { get; set; } = 0;
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
    [ObservableProperty] public partial int MaxFotoExportSize { get; set; } = 1000;
    [ObservableProperty] public partial int MaxPlanExportSize { get; set; } = 6000;
    [ObservableProperty] public partial double PinExportSize { get; set; } = 4.0;
    [ObservableProperty] public partial int PinPosCropExportSize { get; set; } = 30;
    [ObservableProperty] public partial double GpsResponseTimeOut { get; set; } = 10;
    [ObservableProperty] public partial float GpsMinTimeUpdate { get; set; } = 2.0f;
    [ObservableProperty] public partial bool IsGpsActive { get; set; } = false;
    [ObservableProperty] public partial string EditorTheme { get; set; } = "3024-night";
    [ObservableProperty] public partial float PolyLineHandleRadius { get; set; } = 10f;
    [ObservableProperty] public partial float PolyLineHandleTouchRadius { get; set; } = 20f;
    [ObservableProperty] public partial int DoubleClickThresholdMs { get; set; } = 300;
    [ObservableProperty] public partial string PolyLineHandleColor { get; set; } = "#ffffff";
    [ObservableProperty] public partial string PolyLineStartHandleColor { get; set; } = "#00FF00";
    [ObservableProperty] public partial byte PolyLineHandleAlpha { get; set; } = 160;
    [ObservableProperty] public partial Point CustomPinOffset { get; set; } = new(0,0);
    [ObservableProperty] public partial string DefaultPinIcon { get; set; } = "a_pin_red.png";
    [ObservableProperty] public partial string SelectedTemplate { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> Templates { get; set; } = [];
    [ObservableProperty] public partial List<string> ColorThemes { get; set; }
    [ObservableProperty] public partial List<string> AppThemes { get; set; }
    [ObservableProperty] public partial List<string> AppLanguages { get; set; }
    [ObservableProperty] public partial List<string> AppCameraTools { get; set; }
    [ObservableProperty] public partial List<string> IconCategories { get; set; }
    [ObservableProperty] public partial List<string> MapIcons { get; set; } = Settings.MapIcons;
    [ObservableProperty] public partial int MaxTileCache { get; set; } = 100;
    [ObservableProperty] public partial int TileSize { get; set; } = 1024;
    [ObservableProperty] public partial int MaxZoomLevel { get; set; } = 4;
    [ObservableProperty] public partial bool IsLoupeEnabled { get; set; } = true;
    [ObservableProperty] public partial float LoupeRadius { get; set; } = 80f;
    [ObservableProperty] public partial float LoupeZoomFactor { get; set; } = 2.5f;
    [ObservableProperty] public partial int CloudPollingIntervall { get; set; } = 15;
    [ObservableProperty] public partial int ParallelDownloads { get; set; } = 12;
    [ObservableProperty] public partial int ParallelUploads { get; set; } = 12;

#pragma warning disable CA1822
    public string GlobalCloudIcon
    {
        get
        {
            return (SaveManager.CurrentAuth != null && SaveManager.CurrentAuth.IsLoggedIn)
                ? MaterialIcons.Cloud_done
                : MaterialIcons.Cloud_off;
        }
    }
    public string CurrentUserName => SaveManager.CurrentAuth?.CurrentUserName ?? string.Empty;
    public string CurrentUserEmail => SaveManager.CurrentAuth?.CurrentUserEmail ?? string.Empty;
    public bool IsCloudLoggedIn => SaveManager.CurrentAuth?.IsLoggedIn == true;
#pragma warning restore CA1822

    public void RefreshCloudState()
    {
        OnPropertyChanged(nameof(GlobalCloudIcon));
        OnPropertyChanged(nameof(IsCloudLoggedIn));
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(CurrentUserEmail));
    }

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

    // --- Drawing-Style Templates ---
    [ObservableProperty] public partial List<StylePickerItem> StyleTemplateItems { get; set; } =
    [
        new() {
            Text = "Fluchtweg",
            BackgroundColor = "#7F90EE90",
            BorderColor = "#FF008000",
            TextColor = "#FF006400",
            LineWidth = 3,
            StrokeStyle = ""
        },
        new() {
            Text = "Textfeld",
            BackgroundColor = "#FFFFFF",
            BorderColor = "#000000",
            TextColor = "#000000",
            LineWidth = 3,
            StrokeStyle = ""
        },
        new() {
            Text = "Hinweis",
            BackgroundColor = "#66FFB6C1",
            BorderColor = "#FF0000",
            TextColor = "#8B0000",
            LineWidth = 4,
            StrokeStyle = ""
        },
        new() {
            Text = "Gefahr",
            BackgroundColor = "#7FFFFFE0",
            BorderColor = "#DAA520",
            TextColor = "#B8860B",
            LineWidth = 5,
            StrokeStyle = "4 3"
        },
        new() {
            Text = "Text",
            BackgroundColor = "#7FFF4500",
            BorderColor = "#8B0000",
            TextColor = "#FFFFFF",
            LineWidth = 0,
            StrokeStyle = ""
        },
        new() {
            Text = "RWA",
            BackgroundColor = "#FFFF00",
            BorderColor = "#000000",
            TextColor = "#000000",
            LineWidth = 3,
            StrokeStyle = ""
        }
    ];

    // --- Selected ColorTheme ---
    private string _selectedColorTheme;
    public string SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (_selectedColorTheme == value) return;
            _selectedColorTheme = value;
            ApplyColorThemeSafe(value);
        }
    }

    private static void ApplyColorThemeSafe(string theme)
    {
        if (App.Current == null) return;
        ApplyColorTheme(theme);
    }

    public static void ApplyColorTheme(string theme)
    {
        if (theme == null) return;
        if (!ColorThemeMapping.TryGetValue(theme, out var colors)) return;

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
            if (_selectedAppTheme == value) return;
            _selectedAppTheme = value;
            ApplyAppThemeSafe(value);
        }
    }

    private static void ApplyAppThemeSafe(string theme)
    {
        if (App.Current == null) return;
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
            OsBaseScale = OsBaseScale,
            DefaultJson = DefaultJson,
            PinMinScaleLimit = PinMinScaleLimit,
            PinMaxScaleLimit = PinMaxScaleLimit,
            MapIconSize = MapIconSize,
            MapIcon = MapIcon,
            PinPlaceMode = PinPlaceMode,
            PinDuplicateOffset = PinDuplicateOffset,
            IsPlanRotateLocked = IsPlanRotateLocked,
            IsPlanListThumbnails = IsPlanListThumbnails,
            IsHideInactivePlans = IsHideInactivePlans,
            IsPinAutoLock = IsPinAutoLock,
            PdfFullViewDpi = PdfFullViewDpi,
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
            MaxFotoExportSize = MaxFotoExportSize,
            MaxPlanExportSize = MaxPlanExportSize,
            PinLabelPrefix = PinLabelPrefix,
            PinLabelFontSize = Math.Round(PinLabelFontSize, 1),
            PinExportSize = Math.Round(PinExportSize, 1),
            PinPosCropExportSize = PinPosCropExportSize,
            IconGalleryGridView = IconGalleryGridView,
            PhotoGalleryGridView = PhotoGalleryGridView,
            FotoThumbSize = FotoThumbSize,
            FotoThumbQuality = FotoThumbQuality,
            FotoQuality = FotoQuality,
            PlanQuality = PlanQuality,
            PlanPreviewSize = PlanPreviewSize,
            PlanThumbSize = PlanThumbSize,
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
            MaxTileCache = MaxTileCache,
            TileSize = TileSize,
            IsLoupeEnabled = IsLoupeEnabled,
            MaxZoomLevel = MaxZoomLevel,
            LoupeRadius = LoupeRadius,
            LoupeZoomFactor = LoupeZoomFactor,
            CloudPollingIntervall = CloudPollingIntervall,
            ParallelDownloads = ParallelDownloads,
            ParallelUploads = ParallelUploads
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

            var defaultSettings = new SettingsService();

            OsBaseScale = settings.OsBaseScale ?? defaultSettings.OsBaseScale;
            DefaultJson = !string.IsNullOrWhiteSpace(settings.DefaultJson) ? settings.DefaultJson : defaultSettings.DefaultJson;
            PinMinScaleLimit = settings.PinMinScaleLimit ?? defaultSettings.PinMinScaleLimit;
            PinMaxScaleLimit = settings.PinMaxScaleLimit ?? defaultSettings.PinMaxScaleLimit;
            MapIconSize = settings.MapIconSize ?? defaultSettings.MapIconSize;
            MapIcon = settings.MapIcon ?? defaultSettings.MapIcon;
            PinPlaceMode = settings.PinPlaceMode ?? defaultSettings.PinPlaceMode;
            PinDuplicateOffset = settings.PinDuplicateOffset ?? defaultSettings.PinDuplicateOffset;
            IsPlanRotateLocked = settings.IsPlanRotateLocked ?? defaultSettings.IsPlanRotateLocked;
            IsPlanListThumbnails = settings.IsPlanListThumbnails ?? defaultSettings.IsPlanListThumbnails;
            IsHideInactivePlans = settings.IsHideInactivePlans ?? defaultSettings.IsHideInactivePlans;
            IsPinAutoLock = settings.IsPinAutoLock ?? defaultSettings.IsPinAutoLock;
            PdfFullViewDpi = settings.PdfFullViewDpi ?? defaultSettings.PdfFullViewDpi;
            PdfThumbDpi = settings.PdfThumbDpi ?? defaultSettings.PdfThumbDpi;

            SelectedAppTheme = (settings.SelectedAppTheme.HasValue && settings.SelectedAppTheme >= 0 && settings.SelectedAppTheme < AppThemes.Count)
                ? AppThemes[settings.SelectedAppTheme.Value] : defaultSettings.SelectedAppTheme;

            SelectedColorTheme = (settings.SelectedColorTheme.HasValue && settings.SelectedColorTheme >= 0 && settings.SelectedColorTheme < ColorThemes.Count)
                ? ColorThemes[settings.SelectedColorTheme.Value] : defaultSettings.SelectedColorTheme;

            SelectedAppLanguage = (settings.SelectedAppLanguage.HasValue && settings.SelectedAppLanguage >= 0 && settings.SelectedAppLanguage < AppLanguages.Count)
                ? AppLanguages[settings.SelectedAppLanguage.Value] : defaultSettings.SelectedAppLanguage;

            SelectedCameraTool = (settings.SelectedCameraTool.HasValue && settings.SelectedCameraTool >= 0 && settings.SelectedCameraTool < AppCameraTools.Count)
                ? AppCameraTools[settings.SelectedCameraTool.Value] : defaultSettings.SelectedCameraTool;

            IconCategory = (settings.IconCategory.HasValue && settings.IconCategory > 0 && settings.IconCategory < IconCategories.Count)
                ? IconCategories[settings.IconCategory.Value] : defaultSettings.IconCategory;

            IsPlanExport = settings.IsPlanExport ?? defaultSettings.IsPlanExport;
            IsPosImageExport = settings.IsPosImageExport ?? defaultSettings.IsPosImageExport;
            IsPinIconExport = settings.IsPinIconExport ?? defaultSettings.IsPinIconExport;
            IsImageExport = settings.IsImageExport ?? defaultSettings.IsImageExport;
            IsFotoOverlayExport = settings.IsFotoOverlayExport ?? defaultSettings.IsFotoOverlayExport;
            MaxFotoExportSize = settings.MaxFotoExportSize ?? defaultSettings.MaxFotoExportSize;
            MaxPlanExportSize = settings.MaxPlanExportSize ?? defaultSettings.MaxPlanExportSize;
            PinLabelPrefix = !string.IsNullOrWhiteSpace(settings.PinLabelPrefix) ? settings.PinLabelPrefix : defaultSettings.PinLabelPrefix;
            DefaultPinIcon = !string.IsNullOrWhiteSpace(settings.DefaultPinIcon) ? settings.DefaultPinIcon : defaultSettings.DefaultPinIcon;
            EditorTheme = !string.IsNullOrWhiteSpace(settings.EditorTheme) ? settings.EditorTheme : defaultSettings.EditorTheme;
            PolyLineHandleColor = !string.IsNullOrWhiteSpace(settings.PolyLineHandleColor) ? settings.PolyLineHandleColor : defaultSettings.PolyLineHandleColor;
            PolyLineStartHandleColor = !string.IsNullOrWhiteSpace(settings.PolyLineStartHandleColor) ? settings.PolyLineStartHandleColor : defaultSettings.PolyLineStartHandleColor;
            PinLabelFontSize = settings.PinLabelFontSize ?? defaultSettings.PinLabelFontSize;
            PinExportSize = settings.PinExportSize ?? defaultSettings.PinExportSize;
            PinPosCropExportSize = settings.PinPosCropExportSize ?? defaultSettings.PinPosCropExportSize;
            IconGalleryGridView = settings.IconGalleryGridView ?? defaultSettings.IconGalleryGridView;
            PhotoGalleryGridView = settings.PhotoGalleryGridView ?? defaultSettings.PhotoGalleryGridView;
            FotoThumbSize = settings.FotoThumbSize ?? defaultSettings.FotoThumbSize;
            FotoThumbQuality = settings.FotoThumbQuality ?? defaultSettings.FotoThumbQuality;
            FotoQuality = settings.FotoQuality ?? defaultSettings.FotoQuality;
            PlanQuality = settings.PlanQuality ?? defaultSettings.PlanQuality;
            PlanPreviewSize = settings.PlanPreviewSize ?? defaultSettings.PlanPreviewSize;
            PlanThumbSize = settings.PlanThumbSize ?? defaultSettings.PlanThumbSize;
            FotoPreviewSize = settings.FotoPreviewSize ?? defaultSettings.FotoPreviewSize;
            IconPreviewSize = settings.IconPreviewSize ?? defaultSettings.IconPreviewSize;
            GridViewMinColumns = settings.GridViewMinColumns ?? defaultSettings.GridViewMinColumns;
            DefaultPinZoom = settings.DefaultPinZoom ?? defaultSettings.DefaultPinZoom;
            GpsResponseTimeOut = settings.GpsResponseTimeOut ?? defaultSettings.GpsResponseTimeOut;
            GpsMinTimeUpdate = settings.GpsMinTimeUpdate ?? defaultSettings.GpsMinTimeUpdate;
            IsGpsActive = settings.IsGpsActive ?? defaultSettings.IsGpsActive;
            PolyLineHandleRadius = settings.PolyLineHandleRadius ?? defaultSettings.PolyLineHandleRadius;
            PolyLineHandleTouchRadius = settings.PolyLineHandleTouchRadius ?? defaultSettings.PolyLineHandleTouchRadius;
            DoubleClickThresholdMs = settings.DoubleClickThresholdMs ?? defaultSettings.DoubleClickThresholdMs;
            PolyLineHandleAlpha = settings.PolyLineHandleAlpha ?? defaultSettings.PolyLineHandleAlpha;
            CustomPinOffset = settings.CustomPinOffset ?? defaultSettings.CustomPinOffset;
            ColorList = settings.ColorList?.Count > 0 ? settings.ColorList : [.. defaultSettings.ColorList];
            PriorityItems = settings.PriorityItems?.Count > 0 ? settings.PriorityItems : [.. defaultSettings.PriorityItems];
            StyleTemplateItems = settings.StyleTemplateItems?.Count > 0 ? settings.StyleTemplateItems : [.. defaultSettings.StyleTemplateItems];
            MaxTileCache = settings.MaxTileCache ?? defaultSettings.MaxTileCache;
            TileSize = settings.TileSize ?? defaultSettings.TileSize;
            IsLoupeEnabled = settings.IsLoupeEnabled ?? defaultSettings.IsLoupeEnabled;
            MaxZoomLevel = settings.MaxZoomLevel ?? defaultSettings.MaxZoomLevel;
            LoupeRadius = settings.LoupeRadius ?? defaultSettings.LoupeRadius;
            LoupeZoomFactor = settings.LoupeZoomFactor ?? defaultSettings.LoupeZoomFactor;
            CloudPollingIntervall = settings.CloudPollingIntervall ?? defaultSettings.CloudPollingIntervall;
            ParallelDownloads = settings.ParallelDownloads ?? defaultSettings.ParallelDownloads;
            ParallelUploads = settings.ParallelUploads ?? defaultSettings.ParallelUploads;
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
        OsBaseScale = defaultSettings.OsBaseScale;
        DefaultJson = defaultSettings.DefaultJson;
        PinMinScaleLimit = defaultSettings.PinMinScaleLimit;
        PinMaxScaleLimit = defaultSettings.PinMaxScaleLimit;
        MapIconSize = defaultSettings.MapIconSize;
        MapIcon = defaultSettings.MapIcon;
        PinPlaceMode = defaultSettings.PinPlaceMode;
        PinDuplicateOffset = defaultSettings.PinDuplicateOffset;
        IsPlanRotateLocked = defaultSettings.IsPlanRotateLocked;
        IsPlanListThumbnails = defaultSettings.IsPlanListThumbnails;
        IsHideInactivePlans = defaultSettings.IsHideInactivePlans;
        IsPinAutoLock = defaultSettings.IsPinAutoLock;
        PdfFullViewDpi = defaultSettings.PdfFullViewDpi;
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
        MaxFotoExportSize = defaultSettings.MaxFotoExportSize;
        MaxPlanExportSize = defaultSettings.MaxPlanExportSize;
        PinLabelPrefix = defaultSettings.PinLabelPrefix;
        PinLabelFontSize = defaultSettings.PinLabelFontSize;
        PinExportSize = defaultSettings.PinExportSize;
        PinPosCropExportSize = defaultSettings.PinPosCropExportSize;
        IconGalleryGridView = defaultSettings.IconGalleryGridView;
        PhotoGalleryGridView = defaultSettings.PhotoGalleryGridView;
        FotoThumbSize = defaultSettings.FotoThumbSize;
        FotoThumbQuality = defaultSettings.FotoThumbQuality;
        FotoQuality = defaultSettings.FotoQuality;
        PlanQuality = defaultSettings.PlanQuality;
        PlanPreviewSize = defaultSettings.PlanPreviewSize;
        PlanThumbSize = defaultSettings.PlanThumbSize;
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
        MaxTileCache = defaultSettings.MaxTileCache;
        TileSize = defaultSettings.TileSize;
        IsLoupeEnabled = defaultSettings.IsLoupeEnabled;
        MaxZoomLevel = defaultSettings.MaxZoomLevel;
        LoupeRadius = defaultSettings.LoupeRadius;
        LoupeZoomFactor = defaultSettings.LoupeZoomFactor;
        CloudPollingIntervall = defaultSettings.CloudPollingIntervall;
        ParallelDownloads = defaultSettings.ParallelDownloads;
        ParallelUploads = defaultSettings.ParallelUploads;
    }
}
