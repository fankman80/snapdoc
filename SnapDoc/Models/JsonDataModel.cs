#nullable disable
#pragma warning disable MVVMTK0045

using SkiaSharp;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SnapDoc.Models;

public class JsonDataModel
{
    public string Client_name { get; set; }
    public string Object_address { get; set; }
    public string Working_title { get; set; }
    public string Project_nr { get; set; }
    public string Object_name { get; set; }
    public DateTime Creation_date { get; set; }
    public string Project_manager { get; set; }
    public Dictionary<string, Plan> Plans { get; set; }
    public string PlanPath { get; set; }
    public string ImagePath { get; set; }
    public string ThumbnailPath { get; set; }
    public string CustomPinsPath { get; set; }
    public string ProjectPath { get; set; }
    public string JsonFile { get; set; }
    public string TitleImage { get; set; }
    public Size TitleImageSize { get; set; }
}

public partial class Plan : ObservableObject
{
    public string Name { get; set; }
    public string File { get; set; }
    public Size ImageSize { get; set; }
    public bool IsGrayscale { get; set; }
    public string Description { get; set; }
    public Dictionary<string, Pin> Pins { get; set; } = [];

    [ObservableProperty] private bool _allowExport;

    [ObservableProperty] private string _planColor;

    [ObservableProperty] private int _pinCount;
}

public partial class Pin : ObservableObject
{
    public Point Pos { get; set; }
    public Point Anchor { get; set; }
    public Size Size { get; set; }
    public double PinScale { get; set; }
    public string PinName { get; set; }
    public string PinDesc { get; set; }
    public string PinLocation { get; set; }
    public int PinPriority { get; set; }
    public string OnPlanId { get; set; }
    public string SelfId { get; set; }
    public DateTime DateTime { get; set; }
    public SKColor PinColor { get; set; }
    public double PinRotation { get; set; }
    public GeoLocData GeoLocation { get; set; }
    public bool IsCustomIcon { get; set; }
    public Dictionary<string, Photo> Photos { get; set; }
    [ObservableProperty] private string _pinIcon;
    [ObservableProperty] private bool _isCustomPin;
    [ObservableProperty] private bool _isAllowExport;
    [ObservableProperty] private bool _isLockPosition;
    [ObservableProperty] private bool _isLockRotate;
    [ObservableProperty] private bool _isLockAutoScale;
}

public partial class Photo : ObservableObject
{
    public string File { get; set; }
    public bool HasOverlay { get; set; }
    public DateTime DateTime { get; set; }
    public Size ImageSize { get; set; }

    [ObservableProperty] private bool _allowExport;
}

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
#pragma warning restore MVVMTK0045