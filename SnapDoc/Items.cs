#nullable disable

using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using SnapDoc.Models;
using System.Globalization;

namespace SnapDoc
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
        public required DateTime FileDate { get; set; }
        public string DateTimeDisplay => FileDate.ToString("dd. MMMM yyyy' / 'HH:mm", new CultureInfo("de-DE"));
        public required string ImagePath { get; set; }
        public required string ThumbnailPath { get; set; }
    }

    public partial class PinItem : ObservableObject
    {
        private readonly Pin _pin; // direkte Referenz auf das zugrundeliegende Modell

        public PinItem(Pin pin)
        {
            _pin = pin ?? throw new ArgumentNullException(nameof(pin));

            // UI-Update bei Änderungen am Modell
            _pin.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Pin.AllowExport))
                    OnPropertyChanged(nameof(AllowExport));
            };
        }

        // Grunddaten aus dem Modell
        public string SelfId => _pin.SelfId;
        public string OnPlanId => _pin.OnPlanId;
        public string PinLocation => _pin.PinLocation;
        public string PinDesc => _pin.PinDesc;
        public string PinIcon => _pin.PinIcon;
        public string PinName => _pin.PinName;
        public DateTime Time => _pin.DateTime;

        public bool AllowExport
        {
            get => _pin.AllowExport;
            set
            {
                if (_pin.AllowExport != value)
                {
                    _pin.AllowExport = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PlanDisplay =>
                string.IsNullOrWhiteSpace(GlobalJson.Data.Plans[OnPlanId].Name) || string.IsNullOrWhiteSpace(PinLocation)
                    ? GlobalJson.Data.Plans[OnPlanId].Name + PinLocation
                    : $"{GlobalJson.Data.Plans[OnPlanId].Name}  /  {PinLocation}";
    }


    public class ColorPickerReturn(string colorHex, int width)
    {
        public string PenColorHex { get; set; } = colorHex;
        public int PenWidth { get; set; } = width;
    }

    public class PlanEditReturn(string nameEntry, string descEntry, bool allowExport, int planRotate, string planColor)
    {
        public string NameEntry { get; set; } = nameEntry;
        public string DescEntry { get; set; } = descEntry;
        public bool AllowExport { get; set; } = allowExport;
        public int PlanRotate { get; set; } = planRotate;
        public string PlanColor { get; set; } = planColor;
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

    public partial class PlanItem : ObservableObject
    {
        private readonly Plan _plan; // direkte Referenz auf das zugrundeliegende Modell

        public PlanItem(Plan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));

            _plan.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Plan.AllowExport))
                    OnPropertyChanged(nameof(AllowExport));
                else if (e.PropertyName == nameof(Plan.PlanColor))
                    OnPropertyChanged(nameof(PlanColor));
                else if (e.PropertyName == nameof(Plan.PinCount))
                    OnPropertyChanged(nameof(PinCount));
            };
        }

        public string PlanId { get; set; } = string.Empty;
        public string PlanRoute { get; set; } = string.Empty;

        public bool AllowExport
        {
            get => _plan.AllowExport;
            set
            {
                if (_plan.AllowExport != value)
                {
                    _plan.AllowExport = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PlanColor
        {
            get => _plan.PlanColor;
            set
            {
                if (_plan.PlanColor != value)
                {
                    _plan.PlanColor = value;
                    OnPropertyChanged(nameof(PlanColor));
                }
            }
        }

        public int PinCount
        {
            get => _plan.PinCount;
            set
            {
                if (_plan.PinCount != value)
                {
                    _plan.PinCount = value;
                    OnPropertyChanged(nameof(PinCount));
                }
            }
        }

        [ObservableProperty] private string _title;

        [ObservableProperty] private bool _isSelected;
    }

    public partial class FotoItem : ObservableObject
    {
        public string ImagePath { get; set; }
        public DateTime DateTime { get; set; }

        [ObservableProperty] private bool _allowExport;
    }

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
    }
}
