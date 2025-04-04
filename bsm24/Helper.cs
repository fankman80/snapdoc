
#nullable disable

using bsm24.Services;
using CommunityToolkit.Maui.Alerts;
using SkiaSharp;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Xml.Linq;
using bsm24.Views;

namespace bsm24;

public class Helper
{
    public static void AddMenuItem(string title, string glyph, string methodName)
    {
        var shellItem = new FlyoutItem
        {
            Title = title,
            AutomationId = "root_menu",
            Icon = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = glyph,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                                    ? (Color)Application.Current.Resources["PrimaryDarkText"]
                                    : (Color)Application.Current.Resources["PrimaryText"]
            }
        };

        // Den Typ der gewünschten Page per Reflection ermitteln
        Type pageType = Type.GetType($"bsm24.Views.{methodName}"); // Namespace anpassen!

        if (pageType != null && typeof(ContentPage).IsAssignableFrom(pageType))
        {
            var shellContent = new ShellContent
            {
                Route = methodName, // Route muss vorher in AppShell registriert sein!
                ContentTemplate = new DataTemplate(() => Activator.CreateInstance(pageType) as ContentPage) // Dynamisch instanziieren
            };

            // FlyoutItem bekommt ShellContent
            shellItem.Items.Add(shellContent);

            // Item zum Shell hinzufügen
            Shell.Current.Items.Add(shellItem);
        }
    }

    public static void AddDivider()
    {
        var flyoutItem = new FlyoutItem
        {
            Title = "────────────────────────────────────────────────────────",
            IsEnabled = false,
            Items = { new ShellContent{} },
            AutomationId = "root_menu",
        };

        if (Shell.Current.Items is IList<ShellItem> shellItems)
            shellItems.Add(flyoutItem);
    }

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
                    new SKColor(
                        byte.Parse(itemElement.Element("Color")?.Attribute("Red")?.Value ?? "0"),
                        byte.Parse(itemElement.Element("Color")?.Attribute("Green")?.Value ?? "0"),
                        byte.Parse(itemElement.Element("Color")?.Attribute("Blue")?.Value ?? "0")),
                    double.Parse(itemElement.Element("Scale")?.Value ?? "1.0", CultureInfo.InvariantCulture),
                    itemElement.Element("Category")?.Value ?? string.Empty
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
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = File.OpenWrite(destinationPath);
        data.SaveTo(stream);
    }

    public static void MoveItem(string automationId, int direction)
    {
        // Dictionary in eine Liste umwandeln, um Reihenfolge zu manipulieren
        var plansList = GlobalJson.Data.Plans.ToList();

        // Index des Elements finden
        int index = plansList.FindIndex(p => p.Key == automationId);
        int newIndex = index + direction;

        if (index >= 0 && newIndex >= 0 && newIndex < plansList.Count)
        {
            // Elemente tauschen
            var temp = plansList[index];
            plansList[index] = plansList[newIndex];
            plansList[newIndex] = temp;

            // Das Dictionary neu aufbauen (Reihenfolge der Liste beibehalten)
            GlobalJson.Data.Plans = plansList.ToDictionary(p => p.Key, p => p.Value);

            // FlyoutItem in Shell aktualisieren
            MoveFlyoutItem(automationId, direction);

            // Speichern
            GlobalJson.SaveToFile();
        }
    }

    public static void MoveFlyoutItem(string automationId, int direction)
    {
        var shellItems = Shell.Current.Items.ToList(); // In eine Liste umwandeln, da Shell.Items keine List<T> ist
        int index = shellItems.FindIndex(item => item is FlyoutItem flyoutItem && flyoutItem.AutomationId == automationId);
        int newIndex = index + direction;

        if (index >= 0 && newIndex >= 0 && newIndex < shellItems.Count)
        {
            // FlyoutItem verschieben
            var temp = shellItems[index];
            shellItems[index] = shellItems[newIndex];
            shellItems[newIndex] = temp;

            // Shell leeren und neu befüllen (Reihenfolge aktualisieren)
            Shell.Current.Items.Clear();
            foreach (var item in shellItems)
            {
                Shell.Current.Items.Add(item);
            }
        }
    }
}