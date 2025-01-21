#nullable disable
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
    public string ImageOverlayPath { get; set; }
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
    private Location _wsg84;

    public Location WGS84
    {
        get
        {
            return _wsg84;
        }
        set
        {
            _wsg84 = value;
        }
    }

    public LocationCH1903 CH1903
    {
        get
        {
            if (_wsg84 != null)
            {
                Functions.LLtoSwissGrid(_wsg84.Latitude, _wsg84.Longitude, out double swissEasting, out double swissNorthing);
                return new LocationCH1903(_wsg84.Timestamp, swissEasting, swissNorthing, (double)_wsg84.Altitude, (double)_wsg84.Accuracy);
            }
            else
                return null;
        }
    }
}

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class LocationCH1903
{
    public DateTimeOffset Timestamp { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Altitude { get; set; }
    public double Accuracy { get; set; }
    public LocationCH1903(DateTimeOffset timestamp, double x, double y, double altitude, double accuracy)
    {
        Timestamp = timestamp;
        X = x;
        Y = y;
        Altitude = altitude;
        Accuracy = accuracy;
    }
}