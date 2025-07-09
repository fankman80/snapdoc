namespace bsm24;
using CommunityToolkit.Maui;

public static class Settings
{
    public const int MaxPdfImageSizeW = 8192;
    public const int MaxPdfImageSizeH = 8192;
    public const int ThumbSize = 150;
    public const int PlanPreviewSize = 150;
    public const int IconPreviewSize = 64;
    public const int PinTextPadding = 6;
    public const int PinTextDistance = 3;
    public const double DefaultPinZoom = 2;
    public const string PinEditSliderRotateLock = "Autom. ausrichten ▶ ziehen";
    public const string PinEditSliderRotateUnlock = "Autom. Ausrichtung aktiviert";

#if WINDOWS
    private static string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BSM24");
#endif
#if ANDROID
    private static string dataDirectory = Path.Combine(Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments)?.AbsolutePath ?? string.Empty, "BSM24");
#endif
#if IOS
    private static string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BSM24");
#endif
    public static string DataDirectory { get => dataDirectory; set => dataDirectory = value; }
    public static List<IconItem> IconData { get; set; } = [];

    public static readonly string CacheDirectory = FileSystem.CacheDirectory;

    public static readonly string TemplateDirectory = Path.Combine(dataDirectory, "templates");

    public static readonly List<MapViewItem> SwissTopoLayers = [
        new MapViewItem { Desc = "kein Map-Layer", Id = "" },
        new MapViewItem { Desc = "PIXELKARTE_FARBE", Id = "ch.swisstopo.pixelkarte-farbe" },
        new MapViewItem { Desc = "PIXELKARTE_FARK_PK1000", Id = "ch.swisstopo.pixelkarte-farbe-pk1000.noscale" },
        new MapViewItem { Desc = "PIXELKARTE_GRAUSTUFEN", Id = "ch.swisstopo.pixelkarte-grau" },
        new MapViewItem { Desc = "PIXELKARTE_FARBE_WINTER", Id = "ch.swisstopo.pixelkarte-farbe-winter" },
        new MapViewItem { Desc = "SWISSIMAGE", Id = "ch.swisstopo.swissimage" },
        new MapViewItem { Desc = "SWISSIMAGE_1946", Id = "ch.swisstopo.swissimage-product_1946" },
        new MapViewItem { Desc = "LUFTFAHRTKARTEN_ICAO", Id = "ch.bazl.luftfahrtkarten-icao" },
        new MapViewItem { Desc = "SEGELFLUGKARTE", Id = "ch.bazl.segelflugkarte" },
        new MapViewItem { Desc = "MIL_AIRSPACE_CHART", Id = "ch.vbs.milairspacechart" },
        new MapViewItem { Desc = "SPERR_GEFAHRENZONENKARTE", Id = "ch.vbs.sperr-gefahrenzonenkarte" },
        new MapViewItem { Desc = "SWISSMILPILOTSCHART", Id = "ch.vbs.swissmilpilotschart" },
        new MapViewItem { Desc = "HIKS_DUFOR", Id = "ch.swisstopo.hiks-dufour" },
        new MapViewItem { Desc = "HIKS_SIEGFRIED", Id = "ch.swisstopo.hiks-siegfried" },
        new MapViewItem { Desc = "SWISSTLM3D_EISENBAHNNETZ", Id = "ch.swisstopo.swisstlm3d-eisenbahnnetz" },
        new MapViewItem { Desc = "SWISSTLM3D_STRASSEN", Id = "ch.swisstopo.swisstlm3d-strassen" },
        new MapViewItem { Desc = "SWISSTLM3D_UEBRIGVERKEHR", Id = "ch.swisstopo.swisstlm3d-uebrigerverkehr" },
        new MapViewItem { Desc = "SWISSTLM3D_WANDERWEGE", Id = "ch.swisstopo.swisstlm3d-wanderwege" },
        new MapViewItem { Desc = "SCHWEIZMOBIL_WANDERLAND", Id = "ch.astra.wanderland" },
        new MapViewItem { Desc = "SCHWEIZMOBIL_VELOLAND", Id = "ch.astra.veloland" },
        new MapViewItem { Desc = "SCHWEIZMOBIL_MOUNTAINBIKELAND", Id = "ch.astra.mountainbikeland" },
        new MapViewItem { Desc = "HANGNEIGUNG_30", Id = "ch.swisstopo-karto.hangneigung" },
        new MapViewItem { Desc = "HANGNEIGUNGSKLASSEN_30", Id = "ch.swisstopo.hangneigung-ueber_30" },
        new MapViewItem { Desc = "WILDSCHUTZ_GEBIETE", Id = "ch.bafu.wrz-jagdbanngebiete_select" },
        new MapViewItem { Desc = "WILDRUHEZONEN", Id = "ch.bafu.wrz-wildruhezonen_portal" },
        new MapViewItem { Desc = "SCHNEESCHUH_ROUTEN", Id = "ch.swisstopo-karto.schneeschuhrouten" },
        new MapViewItem { Desc = "SKI_TOUR_ROUTEN", Id = "ch.swisstopo-karto.skitouren" },
        new MapViewItem { Desc = "DROHNEN", Id = "ch.bazl.einschraenkungen-drohnen" },
        new MapViewItem { Desc = "SCHUTZGEBIETE_LUFTFAHRT", Id = "ch.bafu.schutzgebiete-luftfahrt" },
        new MapViewItem { Desc = "EISZEIT", Id = "ch.swisstopo.geologie-eiszeit-lgm-raster" }];

    public static readonly List<PriorityItem> PriorityItems =
    [
        new PriorityItem { Key = "", Color = "#000000" },
        new PriorityItem { Key = "Empfehlung", Color = "#92D050" },
        new PriorityItem { Key = "Wichtig", Color = "#FFC000" },
        new PriorityItem { Key = "Kritisch", Color = "#FF0000" }
    ];

    public static readonly List<string> IconSortCrits =
    [
        "nach Name",
        "nach Farbe"
    ];

    public static readonly List<string> PinSortCrits =
    [
        "nach Plan",
        "nach Pin",
        "nach Standort",
        "nach Bezeichnung",
        "nach aktiv/deaktiv",
        "nach Aufnahmedatum"
    ];

    public static readonly List<string> MapIcons =
    [
        "mappin1a.png",
        "mappin2a.png",
        "mappin3a.png",
        "mappin4a.png",
        "mappin5a.png"
    ];

    public static readonly PopupOptions PopupOptions = new()
    {
        CanBeDismissedByTappingOutsideOfPopup = false,
        Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = new CornerRadius(14),
            StrokeThickness = 0
        }         
    };
}
