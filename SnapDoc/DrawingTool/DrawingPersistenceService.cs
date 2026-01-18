using SkiaSharp;
using System.Text.Json;

namespace SnapDoc.DrawingTool;

public static class DrawingPersistenceService
{
    public static void Save(string path, CombinedDrawable drawable)
    {
        var dto = DrawingMapper.ToDto(drawable);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(dto, new JsonSerializerOptions
            {
                WriteIndented = true
            })
        );
    }

    public static DrawingFileDto Load(string path, CombinedDrawable drawable, SKPoint targetCenter)
    {
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<DrawingFileDto>(json);

        if (dto != null)
            DrawingMapper.FromDto(dto, drawable, targetCenter);

        return dto!;
    }
}