namespace SnapDoc;
using CommunityToolkit.Maui;

public static class Settings
{
    public static double DisplayDensity { get; set; } = 1;

    // Icondefinitionen
    public const string PinEditRotateModeLockIcon = MaterialIcons.Lock_reset;
    public const string PinEditRotateModeUnlockIcon = MaterialIcons.Rotate_auto;
    public const string PinEditSizeModeLockIcon = MaterialIcons.Lock;
    public const string PinEditSizeModeUnlockIcon = MaterialIcons.Zoom_out_map;
    public const string GPSButtonOffIcon = MaterialIcons.Location_off;
    public const string GPSButtonOnIcon = MaterialIcons.Where_to_vote;
    public const string GPSButtonUnknownIcon = MaterialIcons.Not_listed_location;
    public const string TableRowIcon = MaterialIcons.Table_rows;
    public const string TableGridIcon = MaterialIcons.Grid_on;

#if WINDOWS
    private static string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SnapDoc");
    private static double osBaseScale = 1.0;
#endif
#if ANDROID
    private static string dataDirectory = Path.Combine(FileSystem.AppDataDirectory, "SnapDoc");
    private static double osBaseScale = 1.0;
#endif
#if IOS
    private static string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SnapDoc");
    private static double osBaseScale = 2.0;
#endif

    public static double OsBaseScale { get => osBaseScale; set => osBaseScale = value; }
    public static string DataDirectory { get => dataDirectory; set => dataDirectory = value; }
    public static List<IconItem> IconData { get; set; } = [];

    public static readonly string CacheDirectory = Path.Combine(FileSystem.CacheDirectory, "AppCache");

    public static readonly string TemplateDirectory = Path.Combine(dataDirectory, "templates");

    public static readonly List<MapViewItem> SwissTopoLayers = [
        new MapViewItem { Desc = "OpenStreetMap Farbe", Id = "OpenStreetMap" },
        new MapViewItem { Desc = "SwissTopo Farbe", Id = "ch.swisstopo.pixelkarte-farbe" },
        new MapViewItem { Desc = "SwissTopo Satellit", Id = "ch.swisstopo.swissimage" }];
        new MapViewItem { Desc = "SwissTopo Graustufen", Id = "ch.swisstopo.pixelkarte-grau" },
        new MapViewItem { Desc = "SwissTopo Farbe Winter", Id = "ch.swisstopo.pixelkarte-farbe-winter" },
        //new MapViewItem { Desc = "SwissTopo Farbe PK1000", Id = "ch.swisstopo.pixelkarte-farbe-pk1000.noscale" },
        //new MapViewItem { Desc = "Swissimage 1946", Id = "ch.swisstopo.swissimage-product_1946" },
        //new MapViewItem { Desc = "Luftfahrtkarten ICAO", Id = "ch.bazl.luftfahrtkarten-icao" },
        //new MapViewItem { Desc = "Segelflugkarte", Id = "ch.bazl.segelflugkarte" },
        //new MapViewItem { Desc = "Mil Airspace Chart", Id = "ch.vbs.milairspacechart" },
        //new MapViewItem { Desc = "Sperr- und Gefahrenzonenkarte", Id = "ch.vbs.sperr-gefahrenzonenkarte" },
        //new MapViewItem { Desc = "Swiss Mil Pilots Chart", Id = "ch.vbs.swissmilpilotschart" },
        //new MapViewItem { Desc = "Hiks Dufour", Id = "ch.swisstopo.hiks-dufour" },
        //new MapViewItem { Desc = "Hiks Siegfried", Id = "ch.swisstopo.hiks-siegfried" },
        //new MapViewItem { Desc = "SwissTLM3D Eisenbahnnetz", Id = "ch.swisstopo.swisstlm3d-eisenbahnnetz" },
        //new MapViewItem { Desc = "SwissTLM3D Strassen", Id = "ch.swisstopo.swisstlm3d-strassen" },
        //new MapViewItem { Desc = "SwissTLM3D Übriger Verkehr", Id = "ch.swisstopo.swisstlm3d-uebrigerverkehr" },
        //new MapViewItem { Desc = "SwissTLM3D Wanderwege", Id = "ch.swisstopo.swisstlm3d-wanderwege" },
        //new MapViewItem { Desc = "SchweizMobil Wanderland", Id = "ch.astra.wanderland" },
        //new MapViewItem { Desc = "SchweizMobil Veloland", Id = "ch.astra.veloland" },
        //new MapViewItem { Desc = "SchweizMobil Mountainbikeland", Id = "ch.astra.mountainbikeland" },
        //new MapViewItem { Desc = "Hangneigung 30°", Id = "ch.swisstopo-karto.hangneigung" },
        //new MapViewItem { Desc = "Hangneigungsklassen 30°", Id = "ch.swisstopo.hangneigung-ueber_30" },
        //new MapViewItem { Desc = "Wildschutzgebiete", Id = "ch.bafu.wrz-jagdbanngebiete_select" },
        //new MapViewItem { Desc = "Wildruhezonen", Id = "ch.bafu.wrz-wildruhezonen_portal" },
        //new MapViewItem { Desc = "Schneeschuh Routen", Id = "ch.swisstopo-karto.schneeschuhrouten" },
        //new MapViewItem { Desc = "Skitouren Routen", Id = "ch.swisstopo-karto.skitouren" },
        //new MapViewItem { Desc = "Drohnen", Id = "ch.bazl.einschraenkungen-drohnen" },
        //new MapViewItem { Desc = "Schutzgebiete Luftfahrt", Id = "ch.bafu.schutzgebiete-luftfahrt" },
        //new MapViewItem { Desc = "Eiszeit", Id = "ch.swisstopo.geologie-eiszeit-lgm-raster" }];

    public static readonly List<string> MapIcons =
    [
        "themeColorPin",
        "locationpin1a.svg",
        "locationpin2a.svg",
        "locationpin3a.svg",
        "locationpin4a.svg"
    ];

    public static readonly Dictionary<string, string> Languages =
    new () {
        ["system"] = "System",
        ["de"] = "Deutsch",
        ["en"] = "English",
        ["fr"] = "Français",
    };

    public static readonly List<string> CameraTools =
    [
        "Auto",
        "System",
        "SnapDoc",
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
