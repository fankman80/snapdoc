using SkiaSharp;

namespace SnapDoc;

public static class RectangleTextStyleExtensions
{
    public static SKTypeface ToTypeface(this RectangleTextStyle style)
    {
        return style switch
        {
            RectangleTextStyle.Bold | RectangleTextStyle.Italic =>
                SkiaFontLoader.Load("OpenSans-BoldItalic.ttf"),

            RectangleTextStyle.Bold =>
                SkiaFontLoader.Load("OpenSans-Semibold.ttf"),

            RectangleTextStyle.Italic =>
                SkiaFontLoader.Load("OpenSans-Italic.ttf"),

            _ =>
                SkiaFontLoader.Load("OpenSans-Regular.ttf")
        };
    }
}

public static class SkiaFontLoader
{
    static readonly Dictionary<string, SKTypeface> _cache = [];

    public static SKTypeface Load(string fileName)
    {
        if (_cache.TryGetValue(fileName, out var tf))
            return tf;

        using var stream = FileSystem.OpenAppPackageFileAsync(fileName)
                                     .GetAwaiter()
                                     .GetResult();

        tf = SKTypeface.FromStream(stream);
        _cache[fileName] = tf;
        return tf;
    }
}