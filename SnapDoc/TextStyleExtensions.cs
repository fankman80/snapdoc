using SkiaSharp;

namespace SnapDoc;

public static class RectangleTextStyleExtensions
{
    public static SKTypeface ToTypeface(
        this RectangleTextStyle style,
        string? fontFamily = null)
    {
        bool bold = style.HasFlag(RectangleTextStyle.Bold);
        bool italic = style.HasFlag(RectangleTextStyle.Italic);

        if (bold && italic)
        {
            return SKTypeface.FromFamilyName(
                fontFamily,
                new SKFontStyle(
                    SKFontStyleWeight.Bold,
                    SKFontStyleWidth.Normal,
                    SKFontStyleSlant.Italic));
        }

        if (bold)
            return SKTypeface.FromFamilyName(fontFamily, SKFontStyle.Bold);

        if (italic)
            return SKTypeface.FromFamilyName(fontFamily, SKFontStyle.Italic);

        return SKTypeface.FromFamilyName(fontFamily, SKFontStyle.Normal);
    }
}