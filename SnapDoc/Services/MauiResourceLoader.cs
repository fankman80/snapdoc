#nullable disable
namespace SnapDoc.Services;

public static class MauiResourceLoader
{
    public static async Task<Stream> GetAppPackageFileStreamAsync(string fileName)
    {
        Stream stream = null;

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
            stream ??= await FileSystem.Current.OpenAppPackageFileAsync(fileName);

#elif WINDOWS
            stream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);

#elif IOS || MACCATALYST
            // 1. Versuch: Der Standard-MAUI-Weg (funktioniert für alle MauiAssets)
            try 
            {
                stream = await FileSystem.Current.OpenAppPackageFileAsync(fileName);
            }
            catch (FileNotFoundException)
            {
                // Falls nicht gefunden, probieren wir den nativen iOS-Weg
                var name = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName).TrimStart('.');
                
                // PathForResource sucht die Datei im gesamten Bundle, unabhängig von der Ordnerstruktur
                var path = Foundation.NSBundle.MainBundle.PathForResource(name, extension);
                
                if (path != null)
                    stream = File.OpenRead(path);
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiResourceLoader] Fehler beim Laden von '{fileName}': {ex.Message}");
        }

        return stream;
    }

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