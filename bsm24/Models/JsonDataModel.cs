#nullable disable
using Codeuctivity.OpenXmlPowerTools;
using DocumentFormat.OpenXml.InkML;
using SkiaSharp;
using System.ComponentModel;

namespace bsm24.Models;

public class JsonDataModel
{
    public string Client_name { get; set; }
    public string Object_address { get; set; }
    public string Working_title { get; set; }
    public string Object_name { get; set; }
    public DateTime Creation_date { get; set; }
    public string Project_manager { get; set; }
    public Pdf PlanPdf { get; set; }
    public Dictionary<string, Plan> Plans { get; set; }
    public string PlanPath { get; set; }
    public string ImagePath { get; set; }
    public string ThumbnailPath { get; set; }
    public string CustomPinsPath { get; set; }
    public string ProjectPath { get; set; }
    public string JsonFile { get; set; }
    public string TitleImage { get; set; }
}

public class Pdf
{
    public string File { get; set; }
}

public class Plan
{
    public string Name { get; set; }
    public string File { get; set; }
    public Size ImageSize { get; set; }
    public bool IsGrayscale { get; set; }
    public Dictionary<string, Pin> Pins { get; set; }
}

public partial class Pin : INotifyPropertyChanged
{
    private bool _allowExport;
    public Point Pos { get; set; }
    public Point Anchor { get; set; }
    public Size Size { get; set; }
    public bool IsLocked { get; set; }
    public bool IsLockRotate { get; set; }
    public bool IsCustomPin { get; set; }
    public string PinName { get; set; }
    public string PinDesc { get; set; }
    public string PinLocation { get; set; }
    public int PinPriority { get; set; }
    public string PinIcon { get; set; }
    public Dictionary<string, Foto> Fotos { get; set; }
    public string OnPlanId { get; set; }
    public string SelfId { get; set; }
    public DateTime DateTime { get; set; }
    public SKColor PinColor { get; set; }
    public double PinScale { get; set; }
    public double PinRotation { get; set; }
    public GeoLocData GeoLocation { get; set; }
    public bool AllowExport
    {
        get => _allowExport;
        set
        {
            if (_allowExport != value)
            {
                _allowExport = value;
                OnPropertyChanged(nameof(AllowExport));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class Foto
{
    public string File { get; set; }
    public bool HasOverlay { get; set; }
    public bool IsChecked { get; set; }
    public DateTime DateTime { get; set; }
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
    private void Initialize()
    {
        if (_wsg84 != null)
        {
            Timestamp = _wsg84.Timestamp;
            Accuracy = _wsg84.Accuracy.HasValue
                        ? (GeolocationAccuracy)_wsg84.Accuracy.Value
                        : GeolocationAccuracy.Default;
            WGS84 = new LocationWGS84(_wsg84.Latitude, _wsg84.Longitude);
            Functions.LLtoSwissGrid(_wsg84.Latitude, _wsg84.Longitude, out double swissEasting, out double swissNorthing);
            CH1903 = new LocationCH1903(swissEasting, swissNorthing);
        }
    }
}

public class LocationCH1903(double x, double y)
{
    public double X { get; set; } = x;
    public double Y { get; set; } = y;

    public override string ToString()
    {
        return $"X: {X}, Y: {Y}";
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