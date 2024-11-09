#nullable disable

namespace bsm24.Models
{
    public class JsonDataModel
    {
        public string Client_name { get; set; }
        public string Object_address { get; set; }
        public string Working_title { get; set; }
        public string Object_name { get; set; }
        public string Creation_date { get; set; }
        public string Project_manager { get; set; }
        public Pdf PlanPdf { get; set; }
        public Dictionary<string, Plan> Plans { get; set; }
        public string PlanPath { get; set; }
        public string ImagePath { get; set; }
        public string ImageOverlayPath { get; set; }
        public string ThumbnailPath { get; set; }
        public string ProjectPath { get; set; }
        public string JsonFile { get; set; }
    }

    public class Pdf
    {
        public string File { get; set; }
        public Point Pos { get; set; }
        public int Rotation { get; set; }
    }

    public class Plan
    {
        public string Name { get; set; }
        public string File { get; set; }
        public Size ImageSize { get; set; }
        public Dictionary<string, Pin> Pins { get; set; }
    }

    public class Pin
    {
        public Point Pos { get; set; }
        public Point Anchor { get; set; }
        public Size Size { get; set; }
        public bool IsLocked { get; set; }
        public bool IsLockRotate { get; set; }
        public string InfoTxt { get; set; }
        public string PinTxt { get; set; }
        public string PinIcon { get; set; }
        public Dictionary<string, Foto> Fotos { get; set; }
    }

    public class Foto
    {
        public string File { get; set; }
        public bool HasOverlay { get; set; }
        public bool IsChecked { get; set; }
    }

    public class Position
    {
        public float X { get; set; }
        public float Y { get; set; }
    }
}