#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using System.ComponentModel;

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

    protected bool SetPlain<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        this.Touch();
        return true;
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

    private DateTime _creation_date;
    public DateTime Creation_date
    {
        get => _creation_date;
        set => SetPlain(ref _creation_date, value);
    }

    private Size _titleImageSize;
    public Size TitleImageSize
    {
        get => _titleImageSize;
        set => SetPlain(ref _titleImageSize, value);
    }
    
    public string PlanPath { get; set; }
    public string ImagePath { get; set; }
    public string ThumbnailPath { get; set; }
    public string CustomPinsPath { get; set; }
    public Dictionary<string, Plan> Plans { get; set; }
}

// =====================================================================
//  Plan
// =====================================================================
public partial class Plan : SyncModel
{
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Description { get; set; }
    [ObservableProperty] public partial bool IsGrayscale { get; set; }
    [ObservableProperty] public partial bool AllowExport { get; set; }
    [ObservableProperty] public partial string PlanColor { get; set; }
    [ObservableProperty] public partial int PinCount { get; set; }

    private string _file;
    public string File
    {
        get => _file;
        set => SetPlain(ref _file, value);
    }

    private Size _imageSize;
    public Size ImageSize
    {
        get => _imageSize;
        set => SetPlain(ref _imageSize, value);
    }

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
    [ObservableProperty] public partial string OnPlanId { get; set; }
    [ObservableProperty] public partial DateTime DateTime { get; set; }
    [ObservableProperty] public partial GeoLocData GeoLocation { get; set; }
    [ObservableProperty] public partial bool IsCustomIcon { get; set; }
    [ObservableProperty] public partial int PinPriority { get; set; }
    [ObservableProperty] public partial string PinIcon { get; set; }
    [ObservableProperty] public partial bool IsCustomPin { get; set; }
    [ObservableProperty] public partial bool IsAllowExport { get; set; }
    [ObservableProperty] public partial bool IsLockPosition { get; set; }
    [ObservableProperty] public partial bool IsLockRotate { get; set; }
    [ObservableProperty] public partial bool IsLockAutoScale { get; set; }

    private Point _pos;
    public Point Pos
    {
        get => _pos;
        set => SetPlain(ref _pos, value);
    }

    private Point _anchor;
    public Point Anchor
    {
        get => _anchor;
        set => SetPlain(ref _anchor, value);
    }

    private Size _size;
    public Size Size
    {
        get => _size;
        set => SetPlain(ref _size, value);
    }

    private double _pinScale;
    public double PinScale
    {
        get => _pinScale;
        set => SetPlain(ref _pinScale, value);
    }

    private double _pinRotation;
    public double PinRotation
    {
        get => _pinRotation;
        set => SetPlain(ref _pinRotation, value);
    }

    private SKColor _pinColor;
    public SKColor PinColor
    {
        get => _pinColor;
        set => SetPlain(ref _pinColor, value);
    }

    private bool _isWebMapPin;
    public bool IsWebMapPin
    {
        get => _isWebMapPin;
        set => SetPlain(ref _isWebMapPin, value);
    }

    public string SelfId { get; set; }
    public Dictionary<string, Foto> Fotos { get; set; } = [];
}

// =====================================================================
//  Foto
// =====================================================================
public partial class Foto : SyncModel
{
    [ObservableProperty] public partial bool AllowExport { get; set; }

    private string _file;
    public string File
    {
        get => _file;
        set => SetPlain(ref _file, value);
    }

    private bool _hasOverlay;
    public bool HasOverlay
    {
        get => _hasOverlay;
        set => SetPlain(ref _hasOverlay, value);
    }

    private DateTime _dateTime;
    public DateTime DateTime
    {
        get => _dateTime;
        set => SetPlain(ref _dateTime, value);
    }

    private Size _imageSize;
    public Size ImageSize
    {
        get => _imageSize;
        set => SetPlain(ref _imageSize, value);
    }
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
