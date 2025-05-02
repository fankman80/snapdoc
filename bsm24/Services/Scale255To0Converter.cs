#nullable disable

using System.Globalization;

namespace bsm24.Services;

public class Scale255To0Converter : IValueConverter
{
    // value kommt aus deiner ViewModel‑Property (0…255) → UI (0…1)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d255)
        {
            // skaliere auf 0…1
            return ((int)(d255 * 255)).ToString();
        }
        return "0";
    }

    // value kommt aus dem Entry als String (0…1) → ViewModel (0…255)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        if (double.TryParse(str, NumberStyles.Any, culture, out var d01))
        {            
            // skaliere zurück auf 0…255
            return Math.Clamp(d01 / 255, 0, 1);
        }
        return 0d;
    }
}