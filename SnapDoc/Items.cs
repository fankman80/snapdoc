#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Services;
using static SnapDoc.Helper;

namespace SnapDoc;

// =====================================================================
//  Datei-/Projektlisten
// =====================================================================

/// <summary>Ein Projekt in der Projektliste (OpenProject).</summary>
public partial class FileItem : ObservableObject
{
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public required DateTime FileDate { get; set; }

    [ObservableProperty] public partial string ImagePath { get; set; }
    [ObservableProperty] public partial string ThumbnailPath { get; set; }
    [ObservableProperty] public partial bool HasCloudSync { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }

    private bool _isSyncChecked;
    public bool IsSyncChecked
    {
        get => _isSyncChecked;
        set
        {
            if (SetProperty(ref _isSyncChecked, value))
                OnPropertyChanged(nameof(DisplayOpacity));
        }
    }

    public double DisplayOpacity => IsSyncChecked ? 1.0 : 0.4;
}

// =====================================================================
//  Plaene
// =====================================================================

/// <summary>
/// ViewModel-Wrapper um <see cref="Plan"/> fuer die Planliste im Flyout.
/// Alle Modellaenderungen - auch die aus dem Cloud-Merge - kommen ueber
/// <see cref="OnModelPropertyChanged"/> automatisch in der UI an.
/// </summary>
public partial class PlanItem(Plan plan) : ModelItem<Plan>(plan)
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanRoute { get; set; } = string.Empty;
    public bool IsWebMapPlan { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial string Thumbnail { get; set; }

    // --- Anzeige ----------------------------------------------------
    public bool ShowThumbnail => !IsWebMapPlan;
    public double DisplayOpacity => AllowExport ? 1.0 : 0.3;

    // --- Modellgebundene Werte --------------------------------------

    /// <summary>Planname - liegt im Modell, damit Umbenennungen aus der Cloud ankommen.</summary>
    public string Title
    {
        get => Model.Name;
        set => SetModel(Model.Name, value, v => Model.Name = v);
    }

    public string Description
    {
        get => Model.Description;
        set => SetModel(Model.Description, value, v => Model.Description = v);
    }

    public bool AllowExport
    {
        get => Model.AllowExport;
        set => SetModel(Model.AllowExport, value, v => Model.AllowExport = v, [nameof(DisplayOpacity)]);
    }

    public string PlanColor
    {
        get => Model.PlanColor;
        set => SetModel(Model.PlanColor, value, v => Model.PlanColor = v);
    }

    public bool IsGrayscale
    {
        get => Model.IsGrayscale;
        set => SetModel(Model.IsGrayscale, value, v => Model.IsGrayscale = v);
    }

    public int PinCount
    {
        get => Model.PinCount;
        set => SetModel(Model.PinCount, value, v => Model.PinCount = v);
    }

    // --- Modelländerungen durchreichen ------------------------------
    protected override void OnModelPropertyChanged(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(Plan.Name): Notify(nameof(Title)); break;
            case nameof(Plan.Description): Notify(nameof(Description)); break;
            case nameof(Plan.PlanColor): Notify(nameof(PlanColor)); break;
            case nameof(Plan.PinCount): Notify(nameof(PinCount)); break;
            case nameof(Plan.IsGrayscale): Notify(nameof(IsGrayscale)); break;

            case nameof(Plan.AllowExport):
                Notify(nameof(AllowExport), nameof(DisplayOpacity));
                break;
        }
    }
}

// =====================================================================
//  Pins
// =====================================================================

/// <summary>
/// ViewModel-Wrapper um <see cref="Pin"/> fuer Pin-Liste und Detailansicht.
/// Reagiert auf saemtliche Modellaenderungen selbst - ein Messenger-Abo
/// pro Pin waere bei vielen Pins unnoetig teuer.
/// </summary>
public partial class PinItem : ModelItem<Pin>
{
    public PinItem(Pin pin) : base(pin)
    {
        _isCustomPin = pin.IsCustomPin;
        _isWebMapPin = pin.IsWebMapPin;

        UpdatePriorityColor();
    }

    // --- Nur-Lese-Werte aus dem Modell ------------------------------
    public string SelfId => Model.SelfId;
    public string OnPlanId => Model.OnPlanId;
    public DateTime Time => Model.DateTime;
    public bool HasGeolocation => Model.GeoLocation != null;

    // --- Anzeige ----------------------------------------------------
    public double DisplayOpacity => IsAllowExport ? 1.0 : 0.3;

    public string DisplayIconPath
    {
        get
        {
            if (Model.IsCustomIcon)
            {
                string fullPath = Path.Combine(Settings.DataDirectory, "customicons", PinIcon);

                // Default-Icon laden, falls das CustomIcon nicht existiert
                return File.Exists(fullPath)
                    ? fullPath
                    : IconLookup.Get(SettingsService.Instance.DefaultPinIcon).FileName;
            }

            return Model.IsCustomPin ? "shapes64.png" : PinIcon;
        }
    }

    public string PlanDisplay
    {
        get
        {
            if (GlobalJson.Data?.Plans == null ||
                !GlobalJson.Data.Plans.TryGetValue(OnPlanId, out var plan))
                return PinLocation ?? "";

            return string.IsNullOrWhiteSpace(plan.Name) || string.IsNullOrWhiteSpace(PinLocation)
                ? plan.Name + PinLocation
                : $"{plan.Name}  /  {PinLocation}";
        }
    }

    // --- Modellgebundene Werte --------------------------------------
    public string PinName
    {
        get => Model.PinName;
        set => SetModel(Model.PinName, value, v => Model.PinName = v);
    }

    public string PinDesc
    {
        get => Model.PinDesc;
        set => SetModel(Model.PinDesc, value, v => Model.PinDesc = v);
    }

    public string PinLocation
    {
        get => Model.PinLocation;
        set => SetModel(Model.PinLocation, value, v => Model.PinLocation = v, [nameof(PlanDisplay)]);
    }

    public string PinIcon
    {
        get => Model.PinIcon;
        set => SetModel(Model.PinIcon, value, v => Model.PinIcon = v, [nameof(DisplayIconPath)]);
    }

    public bool IsAllowExport
    {
        get => Model.IsAllowExport;
        set => SetModel(Model.IsAllowExport, value, v => Model.IsAllowExport = v, [nameof(DisplayOpacity)]);
    }

    public bool IsLockPosition
    {
        get => Model.IsLockPosition;
        set => SetModel(Model.IsLockPosition, value, v => Model.IsLockPosition = v);
    }

    public int PinPriority
    {
        get => Model.PinPriority;
        set
        {
            if (SetModel(Model.PinPriority, value, v => Model.PinPriority = v))
                UpdatePriorityColor();
        }
    }

    // --- Lokaler UI-Zustand -----------------------------------------
    private bool _isCustomPin;
    public bool IsCustomPin
    {
        get => _isCustomPin;
        set => SetProperty(ref _isCustomPin, value);
    }

    private bool _isWebMapPin;
    public bool IsWebMapPin
    {
        get => _isWebMapPin;
        set => SetProperty(ref _isWebMapPin, value);
    }

    private Color _priorityColor;
    public Color PriorityColor
    {
        get => _priorityColor;
        set => SetProperty(ref _priorityColor, value);
    }

    public List<string> PinPriorites { get; } =
        [.. SettingsService.Instance.PriorityItems.Select(item => item.Key)];

    private void UpdatePriorityColor()
    {
        var items = SettingsService.Instance.PriorityItems;

        PriorityColor = PinPriority > 0 && PinPriority < items.Count
            ? Color.FromArgb(items[PinPriority].Color)
            : Application.Current.RequestedTheme == AppTheme.Dark
                ? (Color)Application.Current.Resources["PrimaryDarkText"]
                : (Color)Application.Current.Resources["PrimaryText"];
    }

    // --- Modelländerungen durchreichen ------------------------------
    protected override void OnModelPropertyChanged(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(Pin.PinName): Notify(nameof(PinName)); break;
            case nameof(Pin.PinDesc): Notify(nameof(PinDesc)); break;
            case nameof(Pin.SelfId): Notify(nameof(SelfId)); break;
            case nameof(Pin.OnPlanId): Notify(nameof(OnPlanId), nameof(PlanDisplay)); break;
            case nameof(Pin.DateTime): Notify(nameof(Time)); break;
            case nameof(Pin.GeoLocation): Notify(nameof(HasGeolocation)); break;
            case nameof(Pin.IsLockPosition): Notify(nameof(IsLockPosition)); break;

            case nameof(Pin.PinLocation):
                Notify(nameof(PinLocation), nameof(PlanDisplay));
                break;

            case nameof(Pin.PinIcon):
                Notify(nameof(PinIcon), nameof(DisplayIconPath));
                break;

            case nameof(Pin.IsCustomIcon):
                Notify(nameof(DisplayIconPath));
                break;

            case nameof(Pin.IsAllowExport):
                Notify(nameof(IsAllowExport), nameof(DisplayOpacity));
                break;

            case nameof(Pin.PinPriority):
                Notify(nameof(PinPriority));
                MainThread.BeginInvokeOnMainThread(UpdatePriorityColor);
                break;

            case nameof(Pin.IsCustomPin):
                IsCustomPin = Model.IsCustomPin;
                Notify(nameof(DisplayIconPath));
                break;

            case nameof(Pin.IsWebMapPin):
                IsWebMapPin = Model.IsWebMapPin;
                break;
        }
    }
}

// =====================================================================
//  Fotos und Icons
// =====================================================================

/// <summary>Ein Foto in der Galerie bzw. an einem Pin.</summary>
public partial class FotoItem : ObservableObject
{
    [ObservableProperty] public partial ImageSource DisplayImage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayOpacity))]
    public partial bool AllowExport { get; set; }

    public string ImagePath { get; set; }
    public DateTime DateTime { get; set; }
    public string OnPlanId { get; set; }
    public string OnPinId { get; set; }

    public double DisplayOpacity => AllowExport ? 1.0 : 0.3;

    public string PlanDisplay =>
        GlobalJson.Data?.Plans != null && GlobalJson.Data.Plans.TryGetValue(OnPlanId, out var plan)
            ? plan.Name
            : "";

    /// <summary>Bild ueber einen Byte-Stream laden - umgeht den MAUI-Bildcache.</summary>
    public void ReloadImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath)) return;

        try
        {
            var bytes = File.ReadAllBytes(ImagePath);
            DisplayImage = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden: {ex.Message}");
        }
    }

    public FotoItem Initialize()
    {
        ReloadImage();
        return this;
    }
}

/// <summary>Ein Icon aus der Icon-Galerie.</summary>
public class IconItem(
    string fileName, string displayName, Point anchorPoint, Size iconSize,
    bool isRotationLocked, bool isAutoScaleLocked, bool isCustomIcon,
    SKColor pinColor, double iconScale, string category, bool isDefaultIcon)
{
    public string FileName { get; set; } = fileName;
    public string DisplayName { get; set; } = displayName;
    public Point AnchorPoint { get; set; } = anchorPoint;
    public Size IconSize { get; set; } = iconSize;
    public bool IsRotationLocked { get; set; } = isRotationLocked;
    public bool IsAutoScaleLocked { get; set; } = isAutoScaleLocked;
    public bool IsCustomIcon { get; set; } = isCustomIcon;
    public bool IsCustomPin { get; set; } = false;
    public SKColor PinColor { get; set; } = pinColor;
    public double IconScale { get; set; } = iconScale;
    public string Category { get; set; } = category;
    public bool IsDefaultIcon { get; set; } = isDefaultIcon;

    public string DisplayIconPath
    {
        get
        {
            if (!IsCustomIcon) return FileName;

            string fullPath = Path.Combine(Settings.DataDirectory, "customicons", FileName);

            // Default-Icon laden, falls das CustomIcon nicht existiert
            return File.Exists(fullPath)
                ? fullPath
                : IconLookup.Get(SettingsService.Instance.DefaultPinIcon).FileName;
        }
    }
}

// =====================================================================
//  Einfache Listen- und Auswahl-Items
// =====================================================================

/// <summary>Eine PDF-Seite im Import-Dialog.</summary>
public class PdfItem
{
    public string ImagePath { get; set; }
    public string PreviewPath { get; set; }
    public string PdfPath { get; set; }
    public bool IsChecked { get; set; }
    public int Dpi { get; set; }
    public string DisplayName { get; set; }
    public string ImageName { get; set; }
    public int PdfPage { get; set; }
    public int FinalWidth { get; set; }
    public int FinalHeight { get; set; }
}

/// <summary>Eine Stilvorlage im Style-Picker.</summary>
public class StylePickerItem
{
    public string Text { get; set; }
    public string BackgroundColor { get; set; }
    public string BorderColor { get; set; }
    public string TextColor { get; set; }
    public int LineWidth { get; set; }
    public string StrokeStyle { get; set; }
    public bool IsHatchEffect { get; set; } = false;
    public float HatchStrokeWitdh { get; set; }
    public float HatchStrokeSpace { get; set; }
    public float HatchRotation { get; set; }

    public double[] StrokeDashArray =>
        Helper.ParseDashArray(StrokeStyle)?.Select(f => (double)f).ToArray();
}

public partial class ColorBoxItem : ObservableObject
{
    [ObservableProperty] public partial Color BackgroundColor { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public bool IsAddButton { get; set; }
}

public class PriorityItem
{
    public required string Key { get; set; }
    public required string Color { get; set; }
}

public class MapViewItem
{
    public required string Desc { get; set; }
    public required string Id { get; set; }
}

public class RatioItem
{
    public required string Name { get; set; }
    public double Value { get; set; }
}
