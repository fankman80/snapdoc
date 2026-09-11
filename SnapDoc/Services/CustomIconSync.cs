using System.Text.Json;

namespace SnapDoc.Services;

public static class CustomIconSync
{
    public static void Upload(IconItem item)
    {
        var project = ProjectItem.Current;
        if (!project.IsCloudLinked) return;

        string iconDir = Path.Combine(Settings.DataDirectory, "customicons");
        string pngPath = Path.Combine(iconDir, item.FileName);
        if (!File.Exists(pngPath)) return;

        var meta = new CustomIconMetadata
        {
            DisplayName = item.DisplayName,
            AnchorX = item.AnchorPoint.X,
            AnchorY = item.AnchorPoint.Y,
            SizeWidth = item.IconSize.Width,
            SizeHeight = item.IconSize.Height,
            IsRotationLocked = item.IsRotationLocked,
            IsAutoScaleLocked = item.IsAutoScaleLocked,
            PinColorHex = item.PinColor.ToString(),
            IconScale = item.IconScale,
            Category = item.Category
        };

        // Nur temporaer erzeugen, um sie hochzuladen - lokal nicht benoetigt
        string metaFileName = Path.ChangeExtension(item.FileName, ".json");
        string tempDir = Path.Combine(Settings.CacheDirectory, "iconmeta_upload");
        Directory.CreateDirectory(tempDir);
        string tempMetaPath = Path.Combine(tempDir, metaFileName);
        File.WriteAllText(tempMetaPath, JsonSerializer.Serialize(meta));

        SaveManager.NotifyDataChanged(
        [
            (pngPath, project.CustomIconsFolder), (tempMetaPath, project.CustomIconsFolder)
        ]);
    }
}