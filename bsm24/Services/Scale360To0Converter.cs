#nullable disable

using ExCSS;
using System.Globalization;

namespace bsm24.Services;

public class Scale360To0Converter : IValueConverter
{
    // value kommt aus deiner ViewModel‑Property (0…360) → UI (0…1)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d360)
        {
            // skaliere auf 0…1
            var degree = (int)(d360 * 360);
            return $"{degree}°";
        }
        return "0°";
    }

    // value kommt aus dem Entry als String (0…1) → ViewModel (0…360)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        str = str.Replace("°", "").Trim();
        if (double.TryParse(str, NumberStyles.Any, culture, out var d01))
        {            
            // skaliere zurück auf 0…360
            return Math.Clamp(d01 / 360, 0, 1);
        }
        return 0d;
    }
}