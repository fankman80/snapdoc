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
    public string PinName { get; set; }
    public string PinDesc { get; set; }
    public string PinLocation { get; set; }
    public string PinIcon { get; set; }
    public Dictionary<string, Foto> Fotos { get; set; }
    public string OnPlanName { get; set; }
    public string OnPlanId { get; set; }
    public string SelfId { get; set; }
    public SKColor PinColor { get; set; }
    public double PinScale { get; set; }
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

public class Position
{
    public float X { get; set; }
    public float Y { get; set; }
}