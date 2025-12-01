
#nullable disable

using CommunityToolkit.Maui.Alerts;
using SkiaSharp;
using SnapDoc.Services;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace SnapDoc;

public class Helper
{
    public static void HeaderUpdate()
    {
        // aktualisiere den Header Text
        SettingsService.Instance.FlyoutHeaderTitle = GlobalJson.Data.Object_name;
        SettingsService.Instance.FlyoutHeaderDesc = GlobalJson.Data.Client_name;

        // aktualisiere das Thumbnail Bild
        if (GlobalJson.Data.TitleImage == "banner_thumbnail.png")
            SettingsService.Instance.FlyoutHeaderImage = "banner_thumbnail.png";
        else
            SettingsService.Instance.FlyoutHeaderImage = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);

#if WINDOWS
        // zwinge das UI zum Aktualisieren
        var currentWindow = Application.Current.Windows[0];
        if (currentWindow != null)
        {
            var currentWidth = currentWindow.Width;
            var currentHeight = currentWindow.Height;
            currentWindow.Width = currentWidth + 1;  // Erhöht die Breite um 1 Pixel
            currentWindow.Height = currentHeight + 1;  // Erhöht die Höhe um 1 Pixel
        }
#endif
    }

    public static SKBitmap ConvertToGrayscale(SKBitmap originalBitmap)
    {
        // Graustufen-ColorMatrix erstellen basierend auf Luminanz
        float[] grayscaleMatrix = [
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0,      0,      0,      1, 0];

        using var colorFilter = SKColorFilter.CreateColorMatrix(grayscaleMatrix);
        var grayBitmap = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
        using var canvas = new SKCanvas(grayBitmap);
        var paint = new SKPaint
        {
            ColorFilter = colorFilter
        };

        canvas.DrawBitmap(originalBitmap, 0, 0, paint);
        canvas.Flush();

        return grayBitmap;
    }

    public static void PackDirectory(string sourceDirectory, string destinationZipFile)
    {
        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException($"Das Quellverzeichnis '{sourceDirectory}' wurde nicht gefunden.");

        if (File.Exists(destinationZipFile))
            File.Delete(destinationZipFile);

        ZipFile.CreateFromDirectory(sourceDirectory, destinationZipFile, CompressionLevel.Optimal, includeBaseDirectory: true);
    }

    public static void UnpackDirectory(string zipFilePath, string extractPath)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException($"Die Zip-Datei '{zipFilePath}' wurde nicht gefunden.");

        ZipFile.ExtractToDirectory(zipFilePath, extractPath);
    }

    public static async Task CopyFileFromResourcesAsync(string fileName, string destinationPath)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        if (stream == null)
        {
            return;
        }

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);
    }

    public static List<IconItem> LoadIconItems(string filePath, out List<string> allCategories, string category = "alle Icons")
    {
        var iconItems = new List<IconItem>();
        var categories = new HashSet<string>();

        try
        {
            categories.Add("alle Icons");
            XDocument doc = XDocument.Load(filePath);
            foreach (var itemElement in doc.Descendants("Item"))
            {
                // Erfasse alle Kategorien
                var categoryValue = itemElement.Element("Category")?.Value ?? string.Empty;
                if (!string.IsNullOrEmpty(categoryValue))
                {
                    categories.Add(categoryValue); // Fügt die Kategorie hinzu, wenn sie nicht leer ist
                }

                // Wenn eine Kategorie zum Filtern übergeben wurde
                if (category != "alle Icons" && !categoryValue.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Springe zum nächsten Element, wenn die Kategorie nicht übereinstimmt
                }

                var fileName = itemElement.Element("FileName")?.Value ?? string.Empty;
                if (fileName.StartsWith("customicons", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.Combine(Settings.DataDirectory, fileName);
                }

                var iconItem = new IconItem(
                    fileName,
                    itemElement.Element("Description")?.Value ?? string.Empty,
                    new Point(
                        double.Parse(itemElement.Element("AnchorPoint")?.Attribute("X")?.Value ?? "0.0", CultureInfo.InvariantCulture),
                        double.Parse(itemElement.Element("AnchorPoint")?.Attribute("Y")?.Value ?? "0.0", CultureInfo.InvariantCulture)),
                    new Size(
                        double.Parse(itemElement.Element("Size")?.Attribute("Width")?.Value ?? "0", CultureInfo.InvariantCulture),
                        double.Parse(itemElement.Element("Size")?.Attribute("Height")?.Value ?? "0", CultureInfo.InvariantCulture)),
                    bool.Parse(itemElement.Element("RotationLocked")?.Value ?? "false"),
                    bool.Parse(itemElement.Element("AutoScaleLocked")?.Value ?? "false"),
                    new SKColor(
                        byte.Parse(itemElement.Element("Color")?.Attribute("Red")?.Value ?? "0"),
                        byte.Parse(itemElement.Element("Color")?.Attribute("Green")?.Value ?? "0"),
                        byte.Parse(itemElement.Element("Color")?.Attribute("Blue")?.Value ?? "0")),
                    double.Parse(itemElement.Element("Scale")?.Value ?? "1.0", CultureInfo.InvariantCulture),
                    itemElement.Element("Category")?.Value ?? string.Empty,
                    SettingsService.Instance.DefaultPinIcon==fileName
                );

                iconItems.Add(iconItem);
            }
        }
        catch (Exception ex)
        {
            Toast.Make($"Fehler in der Icon-Datenbank." + ex.Message).Show();
        }

        allCategories = [.. categories];
        return iconItems;
    }

    public static void UpdateIconItem(string filePath, IconItem updatedIconItem)
    {
        try
        {
            XDocument doc = XDocument.Load(filePath);
            var itemElement = doc.Descendants("Item")
                .FirstOrDefault(x => x.Element("FileName")?.Value == updatedIconItem.FileName);

            if (itemElement != null)
            {
                // Update values
                itemElement.Element("Description").Value = updatedIconItem.DisplayName;
                itemElement.Element("AnchorPoint").SetAttributeValue("X", updatedIconItem.AnchorPoint.X.ToString(CultureInfo.InvariantCulture));
                itemElement.Element("AnchorPoint").SetAttributeValue("Y", updatedIconItem.AnchorPoint.Y.ToString(CultureInfo.InvariantCulture));
                itemElement.Element("Size").SetAttributeValue("Width", updatedIconItem.IconSize.Width.ToString());
                itemElement.Element("Size").SetAttributeValue("Height", updatedIconItem.IconSize.Height.ToString());
                itemElement.Element("RotationLocked").Value = updatedIconItem.IsRotationLocked.ToString();
                itemElement.Element("AutoScaleLocked").Value = updatedIconItem.IsAutoScaleLocked.ToString();
                itemElement.Element("Color").SetAttributeValue("Red", updatedIconItem.PinColor.Red.ToString());
                itemElement.Element("Color").SetAttributeValue("Green", updatedIconItem.PinColor.Green.ToString());
                itemElement.Element("Color").SetAttributeValue("Blue", updatedIconItem.PinColor.Blue.ToString());
                itemElement.Element("Scale").Value = updatedIconItem.IconScale.ToString(CultureInfo.InvariantCulture);
                itemElement.Element("Category").Value = updatedIconItem.Category;
            }
            else
            {
                // Add new item if not found
                doc.Root.Add(new XElement("Item",
                    new XElement("FileName", updatedIconItem.FileName),
                    new XElement("Description", updatedIconItem.DisplayName),
                    new XElement("AnchorPoint",
                        new XAttribute("X", updatedIconItem.AnchorPoint.X),
                        new XAttribute("Y", updatedIconItem.AnchorPoint.Y)),
                    new XElement("Size",
                        new XAttribute("Width", updatedIconItem.IconSize.Width),
                        new XAttribute("Height", updatedIconItem.IconSize.Height)),
                    new XElement("RotationLocked", updatedIconItem.IsRotationLocked),
                    new XElement("AutoScaleLocked", updatedIconItem.IsAutoScaleLocked),
                    new XElement("Color",
                        new XAttribute("Red", updatedIconItem.PinColor.Red),
                        new XAttribute("Green", updatedIconItem.PinColor.Green),
                        new XAttribute("Blue", updatedIconItem.PinColor.Blue)),
                    new XElement("Scale", updatedIconItem.IconScale),
                    new XElement("Category", updatedIconItem.Category)
                ));
            }

            // Save the changes back to the file
            doc.Save(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing to the XML file: " + ex.Message);
        }
    }

    public static void DeleteIconItem(string filePath, string fileName)
    {
        try
        {
            // XML-Datei laden
            XDocument doc = XDocument.Load(filePath);

            // Element finden, das gelöscht werden soll
            var itemElement = doc.Descendants("Item")
                .FirstOrDefault(x => x.Element("FileName")?.Value == fileName);

            if (itemElement != null)
            {
                // Element löschen
                itemElement.Remove();

                // Änderungen zurück in die Datei speichern
                doc.Save(filePath);

                Console.WriteLine($"Item mit FileName '{fileName}' wurde erfolgreich gelöscht.");
            }
            else
            {
                Console.WriteLine($"Kein Item mit FileName '{fileName}' gefunden.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Fehler beim Löschen des Items in der XML-Datei: " + ex.Message);
        }
    }

    public static void BitmapResizer(string sourcePath, string destinationPath, double scaleFactor)
    {
        using var inputBitmap = SKBitmap.Decode(sourcePath);

        int newWidth = (int)(inputBitmap.Width * scaleFactor);
        int newHeight = (int)(inputBitmap.Height * scaleFactor);
        var resizedBitmap = new SKBitmap(newWidth, newHeight);
        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);

        inputBitmap.ScalePixels(resizedBitmap, samplingOptions);

        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoQuality);
        using var stream = File.OpenWrite(destinationPath);
        data.SaveTo(stream);
    }

    // REFRAME Webservice Aufruf für Koorinaten-Transformation
    private static readonly HttpClient _httpClient = new();
    public static async Task<(double E, double N)> Wgs84ToLv95Async(double latitude, double longitude)
    {
        try
        {
            string url = $"https://geodesy.geo.admin.ch/reframe/wgs84tolv95?easting={longitude}&northing={latitude}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double e, n;

            if (root.TryGetProperty("easting", out var eProp) && root.TryGetProperty("northing", out var nProp))
            {
                e = eProp.GetDouble();
                n = nProp.GetDouble();
            }
            else if (root.TryGetProperty("coordinates", out var coords) && coords.GetArrayLength() >= 2)
            {
                e = coords[0].GetDouble();
                n = coords[1].GetDouble();
            }
            else
            {
                throw new Exception("Unbekanntes REFRAME-Format");
            }

            return (e, n);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"REFRAME fehlgeschlagen ({ex.Message}), Fallback auf Näherungsformel...");
            return Wgs84ToLv95Approx(latitude, longitude);
        }
    }

    // Näherungsformel von Dupraz/Marti (swisstopo 1999) – Genauigkeit ~1 m
    public static (double E, double N) Wgs84ToLv95Approx(double latitude, double longitude)
    {
        // Schritt 1: Umrechnung in Sexagesimalsekunden
        double latSec = latitude * 3600.0;
        double lonSec = longitude * 3600.0;

        // Schritt 2: Hilfsgrössen (Differenz zu Bern in 10000")
        double latAux = (latSec - 169028.66) / 10000.0;
        double lonAux = (lonSec - 26782.5) / 10000.0;

        // Schritt 3: LV95 berechnen (nach swisstopo-Dokument)
        double e = 2600072.37
                   + 211455.93 * lonAux
                   - 10938.51 * lonAux * latAux
                   - 0.36 * lonAux * latAux * latAux
                   - 44.54 * Math.Pow(lonAux, 3);

        double n = 1200147.07
                   + 308807.95 * latAux
                   + 3745.25 * lonAux * lonAux
                   + 76.63 * latAux * latAux
                   - 194.56 * lonAux * lonAux * latAux
                   + 119.79 * Math.Pow(latAux, 3);

        return (e, n);
    }

    // Hilfsmethoden für Rotation
    public static double SliderToRotation(double sliderValue)
    {
        if (sliderValue >= 0)
            return sliderValue;

        return 360 + sliderValue;
    }

    public static double ToSliderValue(double angle)
    {
        angle %= 360;
        if (angle < 0)
            angle += 360;

        if (angle > 180)
            return angle - 360;

        return angle;
    }

    public static double NormalizeAngle360(double angle)
    {
        angle %= 360;
        if (angle < 0)
            angle += 360;
        return angle;
    }

    public static class IconLookup
    {
        private static readonly Dictionary<string, IconItem> _icons =
            new(StringComparer.OrdinalIgnoreCase);

        private static IconItem _fallback;

        private static bool _initialized = false;

        public static void Initialize(IEnumerable<IconItem> icons)
        {
            if (_initialized)
                return;

            // Dictionary befüllen
            foreach (var icon in icons)
            {
                if (!_icons.ContainsKey(icon.FileName))
                    _icons[icon.FileName] = icon;
            }

            // Fallback = erstes Icon
            _fallback = _icons.Values.FirstOrDefault();

            _initialized = true;
        }

        public static IconItem Get(string fileName)
        {
            if (_icons.TryGetValue(fileName, out var icon))
                return icon;

            return _fallback;
        }
    }
}