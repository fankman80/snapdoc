#nullable disable

using SkiaSharp;
using System.ComponentModel;

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
}
