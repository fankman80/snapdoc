using SkiaSharp;
using System.Text.Json;

namespace SnapDoc.DrawingTool;

public static class DrawingPersistenceService
{
    public static void Save(string path, CombinedDrawable drawable, float initialRotation)
    {
        var dto = DrawingMapper.ToDto(drawable, initialRotation);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(dto, GlobalJson.GetOptions())
        );
    }

    public static DrawingFileDto Load(string path, CombinedDrawable drawable, SKPoint targetCenter, DrawingController controller)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<DrawingFileDto>(json);

        if (dto != null)
            DrawingMapper.FromDto(dto, drawable, targetCenter, controller);

        return dto!;
    }
}