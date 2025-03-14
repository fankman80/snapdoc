using SkiaSharp;

namespace bsm24;

public static class Settings
{
    public static List<PriorityItem> PriorityItems { get => priorityItems; set => priorityItems = value; }
    public static string CacheDirectory { get => cacheDirectory; set => cacheDirectory = value; }
    public static string TemplateDirectory { get => templateDirectory; set => templateDirectory = value; }
    public static int ThumbSize { get => thumbSize; set => thumbSize = value; }
    public static int PlanPreviewSize { get => planPreviewSize; set => planPreviewSize = value; }
    public static double DefaultPinZoom { get => defaultPinZoom; set => defaultPinZoom = value; }
    public static int PinTextPadding { get => pinTextPadding; set => pinTextPadding = value; }
    public static int PinTextDistance { get => pinTextDistance; set => pinTextDistance = value; }
    public static List<IconItem> PinData { get => pinData; set => pinData = value; }
    public static Color[] ColorData { get => colorData; set => colorData = value; }

    private static string cacheDirectory = Path.Combine(FileSystem.AppDataDirectory, "cache");
    private static string templateDirectory = Path.Combine(FileSystem.AppDataDirectory, "templates");
    private static int thumbSize = 150;
    private static int planPreviewSize = 150;
    private static double defaultPinZoom = 2;
    private static int pinTextPadding = 6;
    private static int pinTextDistance = 3;
    private static List<IconItem> pinData = [];

    private static Color[] colorData = [
        new Color(0, 153, 0),
        new Color(202, 254, 150),
        new Color(159, 255, 127),
        new Color(0, 0, 0),
        new Color(127, 0, 255),
        new Color(3, 101, 221),

        new Color(127, 191, 255),
        new Color(125, 95, 0),
        new Color(223, 113, 0),
        new Color(255, 191, 0),
        new Color(197, 101, 227),
        new Color(250, 186, 252),

        new Color(121, 243, 243),
        new Color(0, 50, 204),
        new Color(52, 148, 253),
        new Color(255, 0, 0),
        new Color(255, 132, 132),
        new Color(255, 255, 0),

        new Color(0, 138, 30),
        new Color(0, 170, 30),
        new Color(255, 179, 179),
        new Color(223, 223, 223),
        new Color(255, 233, 210),
        new Color(255, 255, 255)];

    private static List<PriorityItem> priorityItems =
    [
        new PriorityItem { Key = "", Color = "#000000" },
        new PriorityItem { Key = "Empfehlung", Color = "#92D050" },
        new PriorityItem { Key = "Wichtig", Color = "#FFC000" },
        new PriorityItem { Key = "Kritisch", Color = "#FF0000" }
    ];
}
