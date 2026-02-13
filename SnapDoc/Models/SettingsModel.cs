namespace SnapDoc.Models;

internal class SettingsModel
{
    public int PinMinScaleLimit { get; set; } // Skalierungsgrenze für Pins (Minimum)
    public int PinMaxScaleLimit { get; set; } // Skalierungsgrenze für Pins (Maximum)
    public int MapService { get; set; } // swisstopo-Karten oder OpenStreetMap-Karten aktiviert
    public int MapIconSize { get; set; } // Größe der Karten-Icons auf swisstopo
    public int MapIcon { get; set; } // Ausgewähltes Karten-Icon auf swisstopo
    public int MapOverlay1 { get; set; } // Ausgewähltes swisstopo-Overlay 1
    public int MapOverlay2 { get; set; } // Ausgewähltes swisstopo-Overlay 2
    public int PinPlaceMode { get; set; } // Modus zum Platzieren der Pins
    public double PinDuplicateOffset { get; set; } // Abstand, um den Pins bei der Platzierung verschoben werden, wenn sich bereits ein Pin an der Position befindet
    public int IconSortCrit { get; set; } // Kriterium zur Sortierung der Icons
    public int PinSortCrit { get; set; } // Kriterium zur Sortierung der Pins
    public int IconCategory { get; set; } // Icon-Kategorie
    public bool IsPlanRotateLocked { get; set; } // Planrotation sperren
    public bool IsPlanListThumbnails { get; set; } // Planliste mit Thumbnails anzeigen
    public bool IsHideInactivePlans { get; set; } // Inaktive Pläne in der Planliste ausblenden
    public int MaxPdfPixelCount { get; set; } // Maximale Pixelanzahl eines PDF-Bildes beim Import
    public int PdfThumbDpi { get; set; } // DPI der PDF-Thumbnails im Import-Dialog
    public int SelectedColorTheme { get; set; } // App-Farbschema
    public int SelectedAppTheme { get; set; } // App-Design (Hell/Dunkel)
    public int SelectedAppLanguage { get; set; } // App-Sprache
    public bool IsPlanExport { get; set; } // Pläne im Bericht exportieren
    public bool IsPosImageExport { get; set; } // Positionsbilder im Bericht exportieren
    public bool IsPinIconExport { get; set; } // Pin-Icons im Bericht exportieren
    public bool IsImageExport { get; set; } // Fotos im Bericht exportieren
    public bool IsFotoOverlayExport { get; set; } // Foto-Overlays im Bericht exportieren
    public bool IsFotoCompressed { get; set; } // Fotos im Worddokument komprimieren
    public int FotoCompressValue { get; set; } // Kompressionsqualität der Fotos im Worddokument (0-100)
    public string? PinLabelPrefix { get; set; } // Präfix für Pin-Beschriftungen
    public double PinLabelFontSize { get; set; } // Schriftgröße der Pin-Beschriftungen
    public double PinExportSize { get; set; } // Größe der Pins im Bericht in Milimeter
    public int ImageExportSize { get; set; } // Größe der Fotos im Bericht (maximale Kantenlänge in Pixel)
    public int PinPosExportSize { get; set; } // Größe der Pin-Position-Bilder im Bericht (maximale Kantenlänge in Pixel)
    public int PinPosCropExportSize { get; set; } // Größe der zugeschnittenen Pin-Position-Bilder im Bericht
    public int TitleExportSize { get; set; } // Grösse vom Titelbild im Bericht
    public bool IconGalleryGridView { get; set; } // Modus der Icon-Galerie (Raster oder Liste)
    public bool PhotoGalleryGridView { get; set; } // Modus der Foto-Galerie (Raster oder Liste)
    public int MaxPdfImageSizeW { get; set; } // Maximale Breite eines PDF-Bildes beim Import
    public int MaxPdfImageSizeH { get; set; } // Maximale Höhe eines PDF-Bildes beim Import
    public int FotoThumbSize { get; set; } // Thumbnail-Größe der Fotos (minimale Kantenlänge)
    public int FotoThumbQuality { get; set; } // Thumbnail Kompressionsqualität (0-100)
    public int FotoQuality { get; set; } // Foto Kompressionsqualität (0-100)
    public int PlanPreviewSize { get; set; } // Grösse Planvorschau im PDF-Import-Dialog
    public int FotoPreviewSize { get; set; } // Grösse Fotovorschau in der Foto-Galerie
    public int IconPreviewSize { get; set; } // Grösse der Icons in der Icon-Auswahl
    public int GridViewMinColumns { get; set; } // Minimale Spaltenanzahl im Rastermodus
    public double DefaultPinZoom { get; set; } // Standard Zoomstufe für Pins
    public double GpsResponseTimeOut { get; set; } // Maximale Wartezeit auf GPS-Daten
    public float GpsMinTimeUpdate { get; set; } // Minimale Zeitänderung für GPS-Updates
    public bool IsGpsActive { get; set; } // GPS Ein/Aus
    public string? EditorTheme { get; set; } // Json Editor Thema
    public float PolyLineHandleRadius { get; set; } // Radius der Griffe für Polylinien
    public float PolyLineHandleTouchRadius { get; set; } // Touch-Radius der Griffe für Polylinien
    public int DoubleClickThresholdMs { get; set; } // Zeitspanne für Doppelklick-Erkennung in Millisekunden
    public string? PolyLineHandleColor { get; set; } // Farbe der Griffe für Polylinien (Hex-Wert)
    public string? PolyLineStartHandleColor { get; set; } // Farbe des Start-Griffs für Polylinien (Hex-Wert)
    public byte PolyLineHandleAlpha { get; set; } // Alpha-Wert der Griffe für Polylinien (Hex-Wert)
    public Point CustomPinOffset { get; set; } // Offset für die CustomPins unterschiedlich nach Betriebssystem
    public string? DefaultPinIcon { get; set; } // Standard Pin Icon
    public List<string>? ColorList { get; set; } // Systemweite Farbliste (Hex-Werte)
    public List<PriorityItem>? PriorityItems { get; set; } // Liste der Prioritätsstufen (Name und Hex-Farbcode)
    public List<StylePickerItem>? StyleTemplateItems { get; set; } // Liste der Stilvorlagen für den Stil-Editor
}
