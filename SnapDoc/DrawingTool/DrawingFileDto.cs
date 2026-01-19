
namespace SnapDoc.DrawingTool;

public class DrawingStyleDto
{
    public string LineColor { get; set; } = "#FF000000";
    public string FillColor { get; set; } = "#00000000";
    public float LineThickness { get; set; }
    public string TextColor { get; set; } = "#FF000000";
    public float TextSize { get; set; }
    public int TextAlignment { get; set; }
    public int TextStyle { get; set; }
    public bool AutoSizeText { get; set; }
    public int TextPadding { get; set; }
}

public class DrawingFileDto
{
    public BoundsDto? Bounds { get; set; }
    public DrawingStyleDto? Style { get; set; }
    public PolyDto? Poly { get; set; }
    public FreeDto? Free { get; set; }
    public RectDto? Rect { get; set; }
}

public class BoundsDto
{
    public float Width { get; set; }
    public float Height { get; set; }
}


public class PolyDto
{
    public List<PointDto> Points { get; set; } = [];
    public bool IsClosed { get; set; }
}

public class FreeDto
{
    public List<List<PointDto>> Strokes { get; set; } = [];
}

public class RectDto
{
    public List<PointDto> Points { get; set; } = [];
    public float RotationDeg { get; set; }
    public string? Text { get; set; }
}


public record PointDto(float X, float Y);
