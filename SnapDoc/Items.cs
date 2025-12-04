#nullable disable
#pragma warning disable MVVMTK0045

using CommunityToolkit.Mvvm.ComponentModel;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Services;
using System.Globalization;
using static SnapDoc.Helper;

namespace SnapDoc
{
    public partial class IconItem(string fileName, string displayName, Point anchorPoint, Size iconSize, bool isRotationLocked, bool isAutoScaleLocked, bool isCustomIcon, SKColor pinColor, double iconScale, string category, bool isDefaultIcon)
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
                if (IsCustomIcon)
                {
                    string fullPath = Path.Combine(Settings.DataDirectory, "customicons", FileName);

                    if (!File.Exists(fullPath))
                    {
                        // Lade Default-Icon falls CustomIcon nicht existiert
                        string newPin = SettingsService.Instance.DefaultPinIcon;
                        var iconItem = IconLookup.Get(newPin);
                        return iconItem.FileName;
                    }

                    return fullPath;
                }

                return FileName;
            }
        }
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
                if (e.PropertyName == nameof(Pin.IsAllowExport))
                    OnPropertyChanged(nameof(IsAllowExport));
            };
            UpdatePriorityColor();
        }

        // Grunddaten aus dem Modell
        public string SelfId => _pin.SelfId;
        public string OnPlanId => _pin.OnPlanId;
        public DateTime Time => _pin.DateTime;

        public string DisplayIconPath
        {
            get
            {
                if (_pin.IsCustomIcon)
                {
                    if (!File.Exists(Path.Combine(Settings.DataDirectory, "customicons", PinIcon)))
                    {
                        // Lade Default-Icon falls CustomIcon nicht existiert
                        string _newPin = SettingsService.Instance.DefaultPinIcon;
                        var iconItem = IconLookup.Get(_newPin);
                        return iconItem.FileName;
                    }
                    else
                        return Path.Combine(Settings.DataDirectory, "customicons", PinIcon);
                }
                else if (_pin.IsCustomPin)
                    return "shapes64.png";

                return PinIcon;
            }
        }

        public string PinName
        {
            get => _pin.PinName;
            set
            {
                if (_pin.PinName != value)
                {
                    _pin.PinName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PinDesc
        {
            get => _pin.PinDesc;
            set
            {
                if (_pin.PinDesc != value)
                {
                    _pin.PinDesc = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PinLocation
        {
            get => _pin.PinLocation;
            set
            {
                if (_pin.PinLocation != value)
                {
                    _pin.PinLocation = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PinIcon
        {
            get => _pin.PinIcon;
            set
            {
                if (_pin.PinIcon != value)
                {
                    _pin.PinIcon = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsAllowExport
        {
            get => _pin.IsAllowExport;
            set
            {
                if (_pin.IsAllowExport != value)
                {
                    _pin.IsAllowExport = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCustomPin
        {
            get => _pin.IsCustomPin;
            set
            {
                if (_pin.IsCustomPin != value)
                {
                    _pin.IsCustomPin = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLockPosition
        {
            get => _pin.IsLockPosition;
            set
            {
                if (_pin.IsLockPosition != value)
                {
                    _pin.IsLockPosition = value;
                    OnPropertyChanged();
                }
            }
        }

        public int PinPriority
        {
            get => _pin.PinPriority;
            set
            {
                if (_pin.PinPriority != value)
                {
                    _pin.PinPriority = value;
                    OnPropertyChanged();

                    // Hier neue Farbe laden
                    UpdatePriorityColor();
                }
            }
        }

        private Color _priorityColor;
        public Color PriorityColor
        {
            get => _priorityColor;
            set
            {
                if (_priorityColor != value)
                {
                    _priorityColor = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdatePriorityColor()
        {
            var items = SettingsService.Instance.PriorityItems;

            if (PinPriority > 0 && PinPriority < items.Count)
                PriorityColor = Color.FromArgb(items[PinPriority].Color);
            else
            {
                PriorityColor = Application.Current.RequestedTheme == AppTheme.Dark
                              ? (Color)Application.Current.Resources["PrimaryDarkText"]
                              : (Color)Application.Current.Resources["PrimaryText"];
            }
        }

        public string PlanDisplay =>
                string.IsNullOrWhiteSpace(GlobalJson.Data.Plans[OnPlanId].Name) || string.IsNullOrWhiteSpace(PinLocation)
                    ? GlobalJson.Data.Plans[OnPlanId].Name + PinLocation
                    : $"{GlobalJson.Data.Plans[OnPlanId].Name}  /  {PinLocation}";
    }

    public class ColorPickerReturn(string colorHex, int width, byte fillOpacity)
    {
        public string PenColorHex { get; set; } = colorHex;
        public int PenWidth { get; set; } = width;
        public byte FillOpacity { get; set; } = fillOpacity;
    }

    public class PlanSelectorReturn(string planTarget, bool isPinCopy)
    {
        public string PlanTarget { get; set; } = planTarget;
        public bool IsPinCopy { get; set; } = isPinCopy;
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

    public partial class ColorBoxItem : ObservableObject
    {
        [ObservableProperty] private Color backgroundColor;

        [ObservableProperty] private bool isSelected;
        public bool IsAddButton { get; set; }
    }
}
#pragma warning restore MVVMTK0045
