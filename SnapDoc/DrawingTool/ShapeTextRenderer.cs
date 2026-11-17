using SkiaSharp;

namespace SnapDoc.DrawingTool;

public sealed class ShapeTextRenderer
{
    private SKFont? _font;
    private string? _layoutText;
    private float _layoutW, _layoutH, _layoutSize;
    private RectangleTextStyle _layoutStyle;
    private int _layoutPad;
    private bool _layoutAuto;
    private List<string>? _layoutLines;

    private SKFont GetFont(RectangleTextStyle style, float size)
    {
        var tf = style.ToTypeface();
        if (_font == null || !ReferenceEquals(_font.Typeface, tf))
        {
            _font?.Dispose();
            _font = new SKFont(tf, size);
        }
        _font.Size = size;
        return _font;
    }

    public float Draw(SKCanvas canvas, string text, SKPoint center, float width, float height,
                      float angleDeg, RectangleTextStyle style, RectangleTextAlignment alignment,
                      int padding, bool autoSize, float textSize, float minSize, float maxSize,
                      SKColor color, float density)
    {
        if (string.IsNullOrWhiteSpace(text)) return textSize;

        float maxW = width / 100f * (100 - padding);
        float maxH = height / 100f * (100 - padding);

        bool dirty = _layoutLines == null || _layoutText != text ||
                     _layoutW != width || _layoutH != height ||
                     _layoutStyle != style || _layoutPad != padding ||
                     _layoutAuto != autoSize ||
                     (!autoSize && _layoutSize != textSize);

        if (dirty)
        {
            if (autoSize)
                textSize = CalculateAutoFontSize(text, style, maxW, maxH, minSize, maxSize);

            _layoutLines = BreakTextIntoLines(text, GetFont(style, textSize), maxW);
            (_layoutText, _layoutW, _layoutH, _layoutStyle, _layoutPad, _layoutAuto, _layoutSize)
                = (text, width, height, style, padding, autoSize, textSize);
        }
        else
        {
            textSize = _layoutSize;
        }

        var font = GetFont(style, textSize);
        using var paint = new SKPaint { IsAntialias = true, Color = color };

        var lines = _layoutLines!;
        var m = font.Metrics;
        float lineHeight = m.Descent - m.Ascent;
        float y = -(lines.Count * lineHeight) / 2f - m.Ascent;

        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.RotateDegrees(angleDeg);
        canvas.ClipRect(new SKRect(-width / 2f, -height / 2f, width / 2f, height / 2f));

        foreach (var line in lines)
        {
            float lw = font.MeasureText(line);
            float x = alignment switch
            {
                RectangleTextAlignment.Left => -width / 2f + padding * density,
                RectangleTextAlignment.Right => width / 2f - padding * density - lw,
                _ => -lw / 2f
            };
            canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
            y += lineHeight;
        }

        canvas.Restore();
        return textSize;
    }

    private float CalculateAutoFontSize(string text, RectangleTextStyle style,
                                        float maxW, float maxH, float min, float max)
    {
        float low = min, high = max, best = min;
        while (high - low > 0.5f)
        {
            float mid = (low + high) / 2f;
            if (DoesTextFit(text, GetFont(style, mid), maxW, maxH)) { best = mid; low = mid; }
            else high = mid;
        }
        return best;
    }

    private static bool DoesTextFit(string text, SKFont font, float maxW, float maxH)
    {
        var lines = BreakTextIntoLines(text, font, maxW);
        var m = font.Metrics;
        if (lines.Count * (m.Descent - m.Ascent) > maxH) return false;
        foreach (var line in lines)
            if (font.MeasureText(line) > maxW) return false;
        return true;
    }

    private static List<string> BreakTextIntoLines(string text, SKFont font, float maxWidth)
    {
        var result = new List<string>();
        foreach (var hardLine in text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None))
        {
            if (string.IsNullOrEmpty(hardLine)) { result.Add(string.Empty); continue; }

            string line = "";
            foreach (var word in hardLine.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var test = line.Length == 0 ? word : $"{line} {word}";
                if (font.MeasureText(test) <= maxWidth) line = test;
                else
                {
                    if (line.Length > 0) result.Add(line);
                    line = word;
                }
            }
            if (line.Length > 0) result.Add(line);
        }
        return result;
    }
}