#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using SnapDoc.Resources.Languages;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace SnapDoc.Services;

[AttributeUsage(AttributeTargets.Property)]
public sealed class PersistAttribute : Attribute { }

public partial class SettingsService : ObservableObject
{
    // --- Singleton ---
    private const string SettingsFileName = "appsettings.ini";
    public const string FlyoutHeaderTitle = "SnapDoc";
    public const string FlyoutHeaderDesc = "by Emch+Berger AG Bern";
    public const string FlyoutHeaderImageThumb = "banner_thumbnail.png";
    public const string DefaultJson = "snapdoc_data.json";

    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    // Alle mit [Persist] markierten Properties (einmalig per Reflection ermittelt)
    private static readonly PropertyInfo[] PersistedProperties =
        [.. typeof(SettingsService)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.IsDefined(typeof(PersistAttribute)))];

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

    // =====================================================================
    // Laufzeitwerte (werden NICHT gespeichert)
    // =====================================================================
    public string ProjectPath { get; set; }
    [ObservableProperty] public partial string AppVersion { get; set; } = AppInfo.VersionString;
    [ObservableProperty] public partial bool IsProjectLoaded { get; set; } = false;
    [ObservableProperty] public partial string LastPinId { get; set; }
    [ObservableProperty] public partial string FlyoutHeaderImage { get; set; } = "";
    [ObservableProperty] public partial int FlashMode { get; set; } = 2;
    [ObservableProperty] public partial string SelectedTemplate { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> Templates { get; set; } = [];
    [ObservableProperty] public partial List<string> ColorThemes { get; set; }
    [ObservableProperty] public partial List<string> AppThemes { get; set; }
    [ObservableProperty] public partial List<string> AppLanguages { get; set; }
    [ObservableProperty] public partial List<string> AppCameraTools { get; set; }
    [ObservableProperty] public partial List<string> IconCategories { get; set; }
    [ObservableProperty] public partial List<string> MapIcons { get; set; } = Settings.MapIcons;
    [ObservableProperty] public partial string SelectedAppLanguage { get; set; } = Settings.Languages.First().Value;
    [ObservableProperty] public partial string SelectedCameraTool { get; set; } = Settings.CameraTools.First();
    [ObservableProperty] public partial string IconSortCrit { get; set; }
    [ObservableProperty] public partial string PinSortCrit { get; set; }
    [ObservableProperty] public partial string IconCategory { get; set; }

    // =====================================================================
    // Gespeicherte Einstellungen  →  [Persist]
    // Standardwert = Initialwert. Fehlt der Schlüssel in der Datei, wird er verwendet.
    // =====================================================================
    [ObservableProperty, Persist] public partial double OsBaseScale { get; set; } = Settings.OsBaseScale;
    [ObservableProperty, Persist] public partial double CaptureRatio { get; set; } = 1.33;
    [ObservableProperty, Persist] public partial bool IconGalleryGridView { get; set; } = false;
    [ObservableProperty, Persist] public partial bool PhotoGalleryGridView { get; set; } = false;
    [ObservableProperty, Persist] public partial int FotoThumbSize { get; set; } = 150;
    [ObservableProperty, Persist] public partial int FotoThumbQuality { get; set; } = 75;
    [ObservableProperty, Persist] public partial int FotoQuality { get; set; } = 80;
    [ObservableProperty, Persist] public partial int PlanQuality { get; set; } = 90;
    [ObservableProperty, Persist] public partial int PlanPreviewSize { get; set; } = 150;
    [ObservableProperty, Persist] public partial int PlanThumbSize { get; set; } = 1024;
    [ObservableProperty, Persist] public partial int FotoPreviewSize { get; set; } = 150;
    [ObservableProperty, Persist] public partial int IconPreviewSize { get; set; } = 64;
    [ObservableProperty, Persist] public partial int GridViewMinColumns { get; set; } = 3;
    [ObservableProperty, Persist] public partial double DefaultPinZoom { get; set; } = 4;
    [ObservableProperty, Persist] public partial bool IsPlanRotateLocked { get; set; } = false;
    [ObservableProperty, Persist] public partial bool IsShowLockBadges { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsPlanListThumbnails { get; set; } = false;
    [ObservableProperty, Persist] public partial bool IsHideInactivePlans { get; set; } = false;
    [ObservableProperty, Persist] public partial bool IsPinAutoLock { get; set; } = false;
    [ObservableProperty, Persist] public partial int PinMinScaleLimit { get; set; } = 60;
    [ObservableProperty, Persist] public partial int PinMaxScaleLimit { get; set; } = 100;
    [ObservableProperty, Persist] public partial int PdfFullViewDpi { get; set; } = 360;
    [ObservableProperty, Persist] public partial int PdfThumbDpi { get; set; } = 72;
    [ObservableProperty, Persist] public partial int MapIconSize { get; set; } = 85;
    [ObservableProperty, Persist] public partial int MapIcon { get; set; } = 0;
    [ObservableProperty, Persist][NotifyPropertyChangedFor(nameof(IsButtonActuallyVisible))] public partial bool IsPinPlaceBtnManualHide { get; set; } = false;
    [ObservableProperty, Persist][NotifyPropertyChangedFor(nameof(IsButtonActuallyVisible))] public partial int PinPlaceMode { get; set; } = 0;
    public bool IsButtonActuallyVisible => PinPlaceMode != 2 && !IsPinPlaceBtnManualHide;
    [ObservableProperty, Persist] public partial double PinDuplicateOffset { get; set; } = 0.01;
    [ObservableProperty, Persist] public partial int ImageExportQuality { get; set; } = 80;
    [ObservableProperty, Persist] public partial double PinLabelFontSize { get; set; } = 4;
    [ObservableProperty, Persist] public partial string PinLabelPrefix { get; set; } = "Pos. ";
    [ObservableProperty, Persist] public partial bool IsPlanExport { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsPosImageExport { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsImageExport { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsPinIconExport { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsFotoOverlayExport { get; set; } = true;
    [ObservableProperty, Persist] public partial int MaxFotoExportSize { get; set; } = 1000;
    [ObservableProperty, Persist] public partial int MaxPlanExportSize { get; set; } = 6000;
    [ObservableProperty, Persist] public partial double PinExportSize { get; set; } = 4.0;
    [ObservableProperty, Persist] public partial int PinPosCropExportSize { get; set; } = 30;
    [ObservableProperty, Persist] public partial double GpsResponseTimeOut { get; set; } = 10;
    [ObservableProperty, Persist] public partial float GpsMinTimeUpdate { get; set; } = 2.0f;
    [ObservableProperty, Persist] public partial bool IsGpsActive { get; set; } = false;
    [ObservableProperty, Persist] public partial string EditorTheme { get; set; } = "3024-night";
    [ObservableProperty, Persist] public partial float PolyLineHandleRadius { get; set; } = 10f;
    [ObservableProperty, Persist] public partial float PolyLineHandleTouchRadius { get; set; } = 20f;
    [ObservableProperty, Persist] public partial SKPoint TouchLiftDp { get; set; } = new(-0f, -40f);
    [ObservableProperty, Persist] public partial int DoubleClickThresholdMs { get; set; } = 300;
    [ObservableProperty, Persist] public partial string PolyLineHandleColor { get; set; } = "#ffffff";
    [ObservableProperty, Persist] public partial string PolyLineStartHandleColor { get; set; } = "#00FF00";
    [ObservableProperty, Persist] public partial byte PolyLineHandleAlpha { get; set; } = 80;
    [ObservableProperty, Persist] public partial Point CustomPinOffset { get; set; } = new(0, 0);
    [ObservableProperty, Persist] public partial string DefaultPinIcon { get; set; } = "a_pin_red.png";
    [ObservableProperty, Persist] public partial int MaxTileCache { get; set; } = 100;
    [ObservableProperty, Persist] public partial int TileSize { get; set; } = 1024;
    [ObservableProperty, Persist] public partial int MaxZoomLevel { get; set; } = 4;
    [ObservableProperty, Persist] public partial bool ShowFpsCounter { get; set; } = false;
    [ObservableProperty, Persist] public partial bool IsLoupeEnabled { get; set; } = true;
    [ObservableProperty, Persist] public partial bool IsMoveOffsetEnabled { get; set; } = false;
    [ObservableProperty, Persist] public partial float LoupeRadius { get; set; } = 80f;
    [ObservableProperty, Persist] public partial float LoupeZoomFactor { get; set; } = 2.5f;
    [ObservableProperty, Persist] public partial float LockIconOffset { get; set; } = -0.1f;
    [ObservableProperty, Persist] public partial float LockIconSize { get; set; } = 30;
    [ObservableProperty, Persist] public partial byte LockIconOpacity { get; set; } = 153;
    [ObservableProperty, Persist] public partial int CloudPollingIntervall { get; set; } = 15;
    [ObservableProperty, Persist] public partial int TombstoneKeepDays { get; set; } = 30;
    [ObservableProperty, Persist] public partial int ParallelDownloads { get; set; } = 12;
    [ObservableProperty, Persist] public partial int ParallelUploads { get; set; } = 12;

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

    // --- Listen (Laufzeit, nicht gespeichert) ---
    [ObservableProperty]
    public partial List<string> IconSortCrits { get; set; } =
    [
        AppResources.nach_name,
        AppResources.nach_farbe
    ];

    [ObservableProperty]
    public partial List<string> PinSortCrits { get; set; } =
    [
        AppResources.nach_plan,
        AppResources.nach_pin,
        AppResources.nach_standort,
        AppResources.nach_bezeichnung,
        AppResources.nach_aktiv_inaktiv,
        AppResources.nach_aufnahmedatum,
        AppResources.nach_prioritaet
    ];

    // --- Listen (gespeichert) ---
    [ObservableProperty, Persist]
    public partial List<PriorityItem> PriorityItems { get; set; } =
    [
        new() { Key = "", Color = null },
        new() { Key = AppResources.empfehlung, Color = "#92D050" },
        new() { Key = AppResources.wichtig, Color = "#FFC000" },
        new() { Key = AppResources.kritisch, Color = "#FF0000" }
    ];

    [ObservableProperty, Persist]
    public partial List<string> ColorList { get; set; } =
    [
        "#009900","#CAFE96","#000000","#7F00FF","#0365DD","#7FBFFF","#7D5F00","#DF7100","#FFBF00",
        "#C565E3","#FABAFC","#79F3F3","#0032CC","#FF0000","#FFFF00","#DFDFDF"
    ];

    // --- Drawing-Style Templates ---
    [ObservableProperty, Persist]
    public partial List<StylePickerItem> StyleTemplateItems { get; set; } =
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

    // =====================================================================
    // Save & Load
    // =====================================================================
    public void SaveSettings()
    {
        var root = new JsonObject();

        // Alle [Persist]-Properties automatisch
        foreach (var p in PersistedProperties)
            root[p.Name] = JsonSerializer.SerializeToNode(p.GetValue(this), p.PropertyType);

        // Gerundete Werte (wie bisher)
        root[nameof(PinLabelFontSize)] = Math.Round(PinLabelFontSize, 1);
        root[nameof(PinExportSize)] = Math.Round(PinExportSize, 1);

        // Auswahlwerte als Index (kompatibel zum bisherigen Dateiformat)
        root[nameof(SelectedColorTheme)] = ColorThemes.IndexOf(SelectedColorTheme);
        root[nameof(SelectedAppTheme)] = AppThemes.IndexOf(SelectedAppTheme);
        root[nameof(SelectedAppLanguage)] = AppLanguages.IndexOf(SelectedAppLanguage);
        root[nameof(SelectedCameraTool)] = AppCameraTools.IndexOf(SelectedCameraTool);
        root[nameof(IconSortCrit)] = IconSortCrits.IndexOf(IconSortCrit);
        root[nameof(PinSortCrit)] = PinSortCrits.IndexOf(PinSortCrit);
        root[nameof(IconCategory)] = IconCategories.IndexOf(IconCategory);

        File.WriteAllText(
            Path.Combine(Settings.DataDirectory, SettingsFileName),
            root.ToJsonString(_jsonOptions));
    }

    public void LoadSettings()
    {
        var filePath = Path.Combine(Settings.DataDirectory, SettingsFileName);
        if (!File.Exists(filePath)) return;

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{')) return;
            if (JsonNode.Parse(json) is not JsonObject root) return;

            bool hasMissingKeys = false;

            foreach (var p in PersistedProperties)
            {
                // Schlüssel fehlt (z. B. neue Einstellung nach Update)
                // → Initialwert der Property bleibt und wird unten in die Datei geschrieben
                if (!root.TryGetPropertyValue(p.Name, out var node) || node is null)
                {
                    hasMissingKeys = true;
                    continue;
                }

                try
                {
                    p.SetValue(this, node.Deserialize(p.PropertyType, _jsonOptions));
                }
                catch (Exception ex)
                {
                    // Nicht lesbar (z. B. Typ geändert) → Initialwert bleibt
                    hasMissingKeys = true;
                    Console.WriteLine($"⚠ Einstellung '{p.Name}' ignoriert: {ex.Message}");
                }
            }

            SelectedAppTheme = PickByIndex(root, nameof(SelectedAppTheme), AppThemes, SelectedAppTheme);
            SelectedColorTheme = PickByIndex(root, nameof(SelectedColorTheme), ColorThemes, SelectedColorTheme);
            SelectedAppLanguage = PickByIndex(root, nameof(SelectedAppLanguage), AppLanguages, SelectedAppLanguage);
            SelectedCameraTool = PickByIndex(root, nameof(SelectedCameraTool), AppCameraTools, SelectedCameraTool);
            IconSortCrit = PickByIndex(root, nameof(IconSortCrit), IconSortCrits, IconSortCrit);
            PinSortCrit = PickByIndex(root, nameof(PinSortCrit), PinSortCrits, PinSortCrit);
            IconCategory = PickByIndex(root, nameof(IconCategory), IconCategories, IconCategory);

            // Fehlende Schlüssel mit ihren Standardwerten in die Datei schreiben
            if (hasMissingKeys)
                SaveSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Fehler beim Laden der Einstellungen: {ex.Message}");
        }
    }

    public void ResetSettingsToDefaults()
    {
        // Frische Instanz enthält alle Initialwerte
        var defaults = new SettingsService();

        foreach (var p in PersistedProperties)
            p.SetValue(this, p.GetValue(defaults));

        SelectedColorTheme = defaults.SelectedColorTheme;
        SelectedAppTheme = defaults.SelectedAppTheme;
        SelectedAppLanguage = defaults.SelectedAppLanguage;
        SelectedCameraTool = defaults.SelectedCameraTool;
        IconSortCrit = defaults.IconSortCrit;
        PinSortCrit = defaults.PinSortCrit;
        IconCategory = defaults.IconCategory;
    }

    // =====================================================================
    // Helpers
    // =====================================================================
    private static string PickByIndex(JsonObject root, string key, List<string> list, string fallback)
    {
        if (list is null || list.Count == 0) return fallback;

        return root[key] is JsonValue v && v.TryGetValue<int>(out var i) && i >= 0 && i < list.Count
            ? list[i]
            : fallback;
    }
}
