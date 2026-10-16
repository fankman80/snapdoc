namespace SnapDoc.Services;

public static class TileCache
{
    public static string CacheDirectory => Path.Combine(FileSystem.AppDataDirectory, "Tiles");

    public static string GetPlanRootName(string file)
    {
        string name = Path.GetFileNameWithoutExtension(file);
        int idx = name.LastIndexOf("_r", StringComparison.Ordinal);
        return idx > 0 && name[(idx + 2)..].All(char.IsDigit) ? name[..idx] : name;
    }

    public static void DeleteForPlanFile(string planFile)
    {
        if (string.IsNullOrWhiteSpace(planFile)) return;

        string baseName = GetPlanRootName(planFile);
        if (string.IsNullOrWhiteSpace(baseName)) return;

        DeleteDirectories($"*{baseName}*");
    }

    public static int ClearAll()
    {
        int failed = DeleteDirectories("*");

        // Lose Dateien direkt im Cache-Ordner
        if (Directory.Exists(CacheDirectory))
        {
            foreach (var file in Directory.GetFiles(CacheDirectory))
            {
                try { File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return failed;
    }

    private static int DeleteDirectories(string pattern)
    {
        if (!Directory.Exists(CacheDirectory)) return 0;

        int failed = 0;
        foreach (var dir in Directory.GetDirectories(CacheDirectory, pattern))
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { failed++; }
            catch (UnauthorizedAccessException) { failed++; }
        }
        return failed;
    }
}