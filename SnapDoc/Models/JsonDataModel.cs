#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SnapDoc.Models;

public abstract partial class SyncModel : ObservableObject, ISyncStamped
{
    public DateTimeOffset ModifiedAt { get; set; }
    public string ModifiedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    private static readonly HashSet<string> SyncMeta = [nameof(ModifiedAt), nameof(ModifiedBy), nameof(DeletedAt)];
    protected virtual bool IsDerived(string propertyName) => false;
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == null) return;
        if (SyncMeta.Contains(e.PropertyName)) return;
        if (IsDerived(e.PropertyName)) return;

        this.Touch();
    }
}

// =====================================================================
//  Projekt
// =====================================================================
public partial class JsonDataModel : SyncModel
{
    public string ProjectId { get; set; } = Guid.NewGuid().ToString();
    public string CloudDriveId { get; set; }
    public string CloudFolderId { get; set; }
    [ObservableProperty] public partial string Client_name { get; set; }
    [ObservableProperty] public partial string Object_address { get; set; }
    [ObservableProperty] public partial string Working_title { get; set; }
    [ObservableProperty] public partial string Project_nr { get; set; }
    [ObservableProperty] public partial string Object_name { get; set; }
    [ObservableProperty] public partial string Project_manager { get; set; }
    [ObservableProperty] public partial string TitleImage { get; set; }
    [ObservableProperty] public partial string CustomIconsPath { get; set; }
    [ObservableProperty] public partial DateTime Creation_date { get; set; }
    [ObservableProperty] public partial Size TitleImageSize { get; set; }

    public string PlanPath { get; set; }
    public string ImagePath { get; set; }
    public string ThumbnailPath { get; set; }
    public string CustomPinsPath { get; set; }
    public Dictionary<string, Plan> Plans { get; set; }
    public List<PriorityItem> PriorityItems { get; set; } = [];
}

// =====================================================================
//  Plan
// =====================================================================
public partial class Plan : SyncModel
{
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Description { get; set; }
    [ObservableProperty] public partial bool IsGrayscale { get; set; }
    [ObservableProperty] public partial bool IsInvertColor { get; set; }
    [ObservableProperty] public partial bool AllowExport { get; set; }
    [ObservableProperty] public partial string PlanColor { get; set; }
    [ObservableProperty] public partial int PinCount { get; set; }
    [ObservableProperty] public partial string MeasureUnit { get; set; }
    [ObservableProperty] public partial float MeasureFactor { get; set; }
    [ObservableProperty] public partial string File { get; set; }
    [ObservableProperty] public partial Size ImageSize { get; set; }

    public Dictionary<string, Pin> Pins { get; set; } = [];
    protected override bool IsDerived(string propertyName) => propertyName == nameof(PinCount);
}

// =====================================================================
//  Pin
// =====================================================================
public partial class Pin : SyncModel
{
    [ObservableProperty] public partial string PinName { get; set; }
    [ObservableProperty] public partial string PinDesc { get; set; }
    [ObservableProperty] public partial string PinLocation { get; set; }
    [ObservableProperty] public partial string CreatedBy { get; set; }
    [ObservableProperty] public partial string OnPlanId { get; set; }
    [ObservableProperty] public partial DateTime DateTime { get; set; }
    [ObservableProperty] public partial GeoLocData GeoLocation { get; set; }
    [ObservableProperty] public partial bool IsCustomIcon { get; set; }
    [ObservableProperty] public partial string PriorityKey { get; set; }
    [ObservableProperty] public partial string PinIcon { get; set; }
    [ObservableProperty] public partial bool IsCustomPin { get; set; }
    [ObservableProperty] public partial bool IsAllowExport { get; set; }
    [ObservableProperty] public partial bool IsLockPosition { get; set; }
    [ObservableProperty] public partial bool IsLockRotate { get; set; }
    [ObservableProperty] public partial bool IsLockAutoScale { get; set; }
    [ObservableProperty] public partial Point Pos { get; set; }
    [ObservableProperty] public partial Point Anchor { get; set; }
    [ObservableProperty] public partial Size Size { get; set; }
    [ObservableProperty] public partial double PinScale { get; set; }
    [ObservableProperty] public partial double PinRotation { get; set; }
    [ObservableProperty] public partial SKColor PinColor { get; set; }
    [ObservableProperty] public partial bool IsWebMapPin { get; set; }

    [JsonPropertyName("PinPriority")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]

    public int? LegacyPinPriority { get; set; }
    public string SelfId { get; set; }
    public Dictionary<string, Foto> Fotos { get; set; } = [];
}

// =====================================================================
//  Foto
// =====================================================================
public partial class Foto : SyncModel
{
    [ObservableProperty] public partial bool AllowExport { get; set; }
    [ObservableProperty] public partial string CreatedBy { get; set; }
    [ObservableProperty] public partial string File { get; set; }
    [ObservableProperty] public partial bool HasOverlay { get; set; }
    [ObservableProperty] public partial DateTime DateTime { get; set; }
    [ObservableProperty] public partial Size ImageSize { get; set; }
}

// =====================================================================
//  Geodaten (nicht synchronisiert - haengen immer am Pin)
// =====================================================================
public class GeoLocData
{
    private readonly Location _wsg84;
    public GeoLocData() { }
    public GeoLocData(Location wsg84)
    {
        _wsg84 = wsg84;
        Initialize();
    }

    public DateTimeOffset Timestamp { get; set; }
    public GeolocationAccuracy Accuracy { get; set; }
    public LocationWGS84 WGS84 { get; set; }
    public LocationCH1903 CH1903 { get; set; }

    private async void Initialize()
    {
        if (_wsg84 != null)
        {
            Timestamp = _wsg84.Timestamp;
            Accuracy = _wsg84.Accuracy.HasValue
                        ? (GeolocationAccuracy)_wsg84.Accuracy.Value
                        : GeolocationAccuracy.Default;
            WGS84 = new LocationWGS84(_wsg84.Latitude, _wsg84.Longitude);
            (double swissEasting, double swissNorthing) = await Helper.Wgs84ToLv95Async(_wsg84.Latitude, _wsg84.Longitude);
            CH1903 = new LocationCH1903(swissEasting, swissNorthing);
        }
    }

    public async Task UpdateCH1903Async()
    {
        if (WGS84 != null)
        {
            (double e, double n) = await Helper.Wgs84ToLv95Async(WGS84.Latitude, WGS84.Longitude);
            CH1903 = new LocationCH1903(e, n);
        }
    }
}

public class LocationCH1903(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;

    public override string ToString()
    {
        return $"East: {X}, North: {Y}";
    }
}

public class LocationWGS84(double latitude, double longitude)
{
    public double Latitude { get; set; } = latitude;
    public double Longitude { get; set; } = longitude;

    public override string ToString()
    {
        return $"Latitude: {Latitude}, Longitude: {Longitude}";
    }
}

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}
