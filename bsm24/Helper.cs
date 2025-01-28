
#nullable disable

using System.Reflection;
using bsm24.Services;
using SkiaSharp;
using System.IO.Compression;

namespace bsm24;

public class Helper
{
    public static void FlyoutItemState(string itemRoute, bool isVisible)
    {
        if ((Application.Current.Windows[0].Page as AppShell).Items
        .SelectMany(item => item.Items) // Alle FlyoutItem/ShellSections durchsuchen
        .SelectMany(section => section.Items) // Alle ShellContent-Items durchsuchen
        .FirstOrDefault(content => content.Route == itemRoute) is ShellContent shellContent)
            shellContent.FlyoutItemIsVisible = isVisible;
    }

    public static void AddMenuItem(string title, string glyph, string methodName)
    {
        var newMenuItem = new MenuItem
        {
            Text = title,
            AutomationId = "990",
            IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = glyph,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["PrimaryDark"]
                        : (Color)Application.Current.Resources["Primary"]
            }
        };

        if (Application.Current.Windows[0].Page is AppShell appShell)
        {
            var methodInfo = appShell.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo != null)
                newMenuItem.Clicked += (s, e) => methodInfo.Invoke(appShell, [s, e]);
            else
                Console.WriteLine($"Methode '{methodName}' wurde nicht gefunden.");
        }

        if (Shell.Current.Items is IList<ShellItem> shellItems)
            shellItems.Add(newMenuItem);
    }

    public static void AddDivider()
    {
        var menuItem = new MenuItem
        {
            Text = "----------- Pläne -----------",
            IsEnabled = false,
            AutomationId = "990",
        };

        if (Shell.Current.Items is IList<ShellItem> shellItems)
            shellItems.Add(menuItem);
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
            SettingsService.Instance.FlyoutHeaderImage = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);

#if WINDOWS
        // zwinge das UI zum Aktualisieren
        var currentWindow = Application.Current.Windows.FirstOrDefault();
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


    public static ImageSource SKBitmapToImageSource(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90); // Du kannst PNG oder JPEG verwenden

        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Seek(0, SeekOrigin.Begin); // Stream zurücksetzen

        return ImageSource.FromStream(() => stream);
    }

    public static async Task<Location> GetCurrentLocationAsync()
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync();
            if (location != null)
            {
                return location;
            }
            location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Best,
                Timeout = TimeSpan.FromSeconds(10)
            });
            return location;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to get location: {ex.Message}");
            return null;
        }
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
}
