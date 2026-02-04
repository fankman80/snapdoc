#nullable disable

using SkiaSharp;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapDoc.Services;

public class SkColorToHexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SKColor skColor)
        {
            // ARGB → #AARRGGBB
            return $"#{skColor.Alpha:X2}{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
        }
        return Colors.Transparent.ToHex(); // Fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Optional: Konvertiere von Hex zurück in SKColor (nicht zwingend erforderlich)
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = Color.Parse(hex);
                return new SKColor(
                    (byte)(color.Red * 255),
                    (byte)(color.Green * 255),
                    (byte)(color.Blue * 255),
                    (byte)(color.Alpha * 255));
            }
            catch
            {
                return SKColors.Transparent;
            }
        }
        return SKColors.Transparent;
    }
}

public class RawAssetToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string filename)
        {
            var stream = FileSystem.OpenAppPackageFileAsync(filename).Result;
            return ImageSource.FromStream(() => stream);
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ExportToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool allow = value is bool b && b;
        return allow ? MaterialIcons.Visibility : MaterialIcons.Visibility_off;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SelectedToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool allow = value is bool b && b;
        return allow ? MaterialIcons.Check_box : MaterialIcons.Layers;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToFontAttributesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isBold && isBold)
            return FontAttributes.Bold;

        return FontAttributes.None;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class ColorWithoutAlphaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Color color)
            return Colors.Transparent;

        // Alpha ignorieren → 100 % Deckkraft
        return new Color(color.Red, color.Green, color.Blue, 1f);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HexToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex)
            return Color.FromArgb(hex);
        return Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class SKColorConverter : JsonConverter<SKColor>
{
    public override SKColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var colorString = reader.GetString();
        if (!string.IsNullOrEmpty(colorString))
        {
            return SKColor.Parse(colorString); // Hex-String in SKColor umwandeln
        }
        return SKColors.Transparent; // Fallback-Wert
    }

    public override void Write(Utf8JsonWriter writer, SKColor value, JsonSerializerOptions options)
    {
        var colorString = value.ToString(); // SKColor in Hex-String umwandeln
        writer.WriteStringValue(colorString);
    }
}