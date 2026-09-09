#nullable disable
using SkiaSharp;
using System.Diagnostics;

namespace SnapDoc.Services;

/// <summary>
/// Laedt Icon-/Pin-Bitmaps fuer SkiaSharp aus verschiedenen Quellen:
/// 1. direkter Dateipfad (z. B. importierte Custom-Icons)
/// 2. Extraktions-Cache (bereits einmal aus dem App-Package geholt)
/// 3. plattformspezifische MauiImage-Ressourcen (Android drawable, iOS Bundle, Windows Assets)
/// </summary>
public static class IconBitmapLoader
{
    /// <summary>
    /// Laedt eine Bitmap zum angegebenen Pfad oder Dateinamen.
    /// Gibt null zurueck, wenn die Quelle nicht gefunden oder nicht dekodiert werden kann.
    /// Blockierend - fuer den UI-Thread bitte <see cref="LoadAsync"/> verwenden.
    /// </summary>
    public static SKBitmap Load(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        // 1. Direkter Dateipfad
        var bitmap = TryDecodeFile(iconPath);
        if (bitmap != null)
            return bitmap;

        string fileName = Path.GetFileName(iconPath);
        string cachePath = GetCachePath(fileName);

        // 2. Bereits extrahierte Kopie im Cache
        if (cachePath != null)
        {
            bitmap = TryDecodeFile(cachePath);
            if (bitmap != null)
                return bitmap;
        }

        // 3. Aus dem App-Package extrahieren
        try
        {
            return LoadFromPackage(iconPath, fileName, cachePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IconBitmapLoader: Extrahieren von '{iconPath}' fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Laedt eine Bitmap im Hintergrund-Thread.
    /// </summary>
    public static Task<SKBitmap> LoadAsync(string iconPath)
        => Task.Run(() => Load(iconPath));

    // ------------------------------------------------------------------
    //  Hilfsmethoden
    // ------------------------------------------------------------------

    private static SKBitmap TryDecodeFile(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            using var stream = File.OpenRead(path);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IconBitmapLoader: Dekodieren von '{path}' fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private static string GetCachePath(string fileName)
    {
        try
        {
            string cacheFolder = Settings.CacheDirectory;
            Directory.CreateDirectory(cacheFolder);
            return Path.Combine(cacheFolder, fileName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"IconBitmapLoader: Cache-Ordner nicht verfuegbar: {ex.Message}");
            return null;
        }
    }

    private static SKBitmap DecodeAndCache(Stream source, string cachePath)
    {
        if (cachePath == null)
        {
            using var ms = new MemoryStream();
            source.CopyTo(ms);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }

        using (var target = File.Create(cachePath))
            source.CopyTo(target);

        return TryDecodeFile(cachePath);
    }

    // ------------------------------------------------------------------
    //  Plattformspezifisch
    // ------------------------------------------------------------------

    private static SKBitmap LoadFromPackage(string iconPath, string fileName, string cachePath)
    {
#if ANDROID
        var context = global::Android.App.Application.Context;
        string resourceName = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        int resId = context.Resources.GetIdentifier(resourceName, "drawable", context.PackageName);

        if (resId == 0)
        {
            Debug.WriteLine($"IconBitmapLoader: Kein drawable '{resourceName}' gefunden.");
            return null;
        }

        using var resourceStream = context.Resources.OpenRawResource(resId);
        return DecodeAndCache(resourceStream, cachePath);

#elif IOS || MACCATALYST
        string imageName = Path.GetFileNameWithoutExtension(fileName);
        using var uiImage = UIKit.UIImage.FromBundle(imageName);

        if (uiImage == null)
        {
            Debug.WriteLine($"IconBitmapLoader: Kein Bundle-Image '{imageName}' gefunden.");
            return null;
        }

        using var nsData = uiImage.AsPNG();
        if (nsData == null)
            return null;

        using var pngStream = nsData.AsStream();
        return DecodeAndCache(pngStream, cachePath);

#elif WINDOWS
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        string baseDir = AppContext.BaseDirectory;

        string[] searchDirs =
        [
            Path.Combine(baseDir, "Assets", "pins"),
            Path.Combine(baseDir, "Assets"),
            baseDir
        ];

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            string candidate = Path.Combine(dir, fileName);

            if (!File.Exists(candidate))
                candidate = Path.Combine(dir, $"{nameWithoutExt}.scale-100{ext}");

            if (!File.Exists(candidate))
                candidate = Directory.GetFiles(dir, $"{nameWithoutExt}.scale-*{ext}").FirstOrDefault();

            var bitmap = TryDecodeFile(candidate);
            if (bitmap != null)
                return bitmap;
        }

        Debug.WriteLine($"IconBitmapLoader: '{fileName}' in keinem Assets-Ordner gefunden.");
        return null;

#else
        using var packageStream = FileSystem.OpenAppPackageFileAsync(iconPath).GetAwaiter().GetResult();
        return DecodeAndCache(packageStream, cachePath);
#endif
    }
}