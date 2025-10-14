#nullable disable
namespace SnapDoc.Services;

public static class MauiResourceLoader
{
    /// <summary>
    /// Lädt eine Datei aus dem App-Paket (plattformübergreifend).
    /// Funktioniert mit Drawable-Ressourcen (Android) und MauiAssets.
    /// </summary>
    /// <param name="fileName">Dateiname oder relativer Pfad zur Ressource</param>
    /// <returns>Stream oder null, wenn nicht gefunden</returns>
    public static async Task<Stream?> GetAppPackageFileStreamAsync(string fileName)
    {
        Stream? stream = null;

        try
        {
#if ANDROID
            var context = Android.App.Application.Context;
            var resources = context.Resources;

            // 1️⃣ Versuch über "drawable" (für alte & eingebettete Ressourcen)
            var resourceId = resources.GetIdentifier(
                Path.GetFileNameWithoutExtension(fileName),
                "drawable",
                context.PackageName);

            if (resourceId > 0)
            {
                var imageUri = new Android.Net.Uri.Builder()
                    .Scheme(Android.Content.ContentResolver.SchemeAndroidResource)
                    .Authority(resources.GetResourcePackageName(resourceId))
                    .AppendPath(resources.GetResourceTypeName(resourceId))
                    .AppendPath(resources.GetResourceEntryName(resourceId))
                    .Build();

                stream = context.ContentResolver.OpenInputStream(imageUri);
            }

            // 2️⃣ Versuch über "raw" (wenn die Datei in Resources/raw liegt)
            if (stream == null)
            {
                resourceId = resources.GetIdentifier(
                    Path.GetFileNameWithoutExtension(fileName),
                    "raw",
                    context.PackageName);

                if (resourceId > 0)
                    stream = resources.OpenRawResource(resourceId);
            }

            // 3️⃣ Fallback auf MAUI-Asset-Mechanismus
            if (stream == null)
                stream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);

#elif WINDOWS
            stream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);

#elif IOS || MACCATALYST
            var root = Foundation.NSBundle.MainBundle.BundlePath;
            var path = Path.Combine(root, fileName);
            if (File.Exists(path))
                stream = File.OpenRead(path);
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiResourceLoader] Fehler beim Laden von '{fileName}': {ex.Message}");
        }

        return stream;
    }

    /// <summary>
    /// Kopiert eine Datei aus dem App-Paket in ein Zielverzeichnis.
    /// </summary>
    public static async Task<bool> CopyAppPackageFileAsync(string targetDirectory, string fileName)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);
            var targetPath = Path.Combine(targetDirectory, fileName);

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            using var input = await GetAppPackageFileStreamAsync(fileName);
            if (input == null)
                return false;

            using var output = File.Create(targetPath);
            await input.CopyToAsync(output);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiResourceLoader] Fehler beim Kopieren von '{fileName}': {ex.Message}");
            return false;
        }
    }
}