#nullable disable

using SkiaSharp;
using System.ComponentModel;
using bsm24.Models;

namespace bsm24
{
    public class IconItem(string fileName, string displayName, Point anchorPoint, Size iconSize, bool isRotationLocked, SKColor pinColor, double iconScale, string category)
    {
        public string FileName { get; set; } = fileName;
        public string DisplayName { get; set; } = displayName;
        public Point AnchorPoint { get; set; } = anchorPoint;
        public Size IconSize { get; set; } = iconSize;
        public bool IsRotationLocked { get; set; } = isRotationLocked;
        public bool IsCustomPin { get; set; } = false;
        public SKColor PinColor { get; set; } = pinColor;
        public double IconScale { get; set; } = iconScale;
        public string Category { get; set; } = category;
    }

    public class FileItem
    {
        public required string FileName { get; set; }
        public required string FilePath { get; set; }
        public required string FileDate { get; set; }
        public required string ImagePath { get; set; }
        public required string ThumbnailPath { get; set; }
    }

    public partial class PinItem : INotifyPropertyChanged
    {
        public required string PinDesc { get; set; }
        public required string PinIcon { get; set; }
        public required string PinName { get; set; }
        public required string PinLocation { get; set; }
        public required string OnPlanId { get; set; }
        public required string OnPlanName { get; set; }
        public required string SelfId { get; set; }
        public required DateTime Time { get; set; }

        private bool allowExport;
        public bool AllowExport
        {
            get => allowExport;
            set
            {
                if (allowExport != value)
                {
                    allowExport = value;
                    OnPropertyChanged(nameof(AllowExport)); // Hier wird die Änderung gemeldet
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColorPickerReturn(string colorHex, int width)
    {
        public string PenColorHex { get; set; } = colorHex;
        public int PenWidth { get; set; } = width;
    }

    public class PlanEditReturn(string nameEntry, string descEntry, bool allowExport)
    {
        public string NameEntry { get; set; } = nameEntry;
        public string DescEntry { get; set; } = descEntry;
        public bool AllowExport { get; set; } = allowExport;
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

    public partial class PlanItem : INotifyPropertyChanged
    {
        private readonly Plan _plan; // direkte Referenz auf das zugrundeliegende Modell

        public PlanItem(Plan plan)
        {
            _plan = plan;
            PlanId = plan != null ? "" : string.Empty;
            _plan.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Plan.AllowExport))
                    OnPropertyChanged(nameof(AllowExport));
            };
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }
        private string _title;

        public string PlanId { get; set; }
        public string IconGlyph { get; set; }
        public string PlanRoute { get; set; }
        public bool AllowExport
        {
            get => _plan != null && _plan.AllowExport;
            set
            {
                if (_plan != null && _plan.AllowExport != value)
                {
                    _plan.AllowExport = value;
                    OnPropertyChanged(nameof(AllowExport));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
