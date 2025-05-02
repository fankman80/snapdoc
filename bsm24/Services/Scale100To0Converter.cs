#nullable disable

using System.Globalization;

namespace bsm24.Services;

public class Scale100To0Converter : IValueConverter
{
    // value kommt aus deiner ViewModel‑Property (0…100) → UI (0…1)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float d100)
        {
            // skaliere auf 0…1
            return ((int)(d100 * 100)).ToString();
        }
        return "0";
    }

    // value kommt aus dem Entry als String (0…1) → ViewModel (0…100)
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        if (float.TryParse(str, NumberStyles.Any, culture, out var d01))
        {
            // skaliere zurück auf 0…100
            return Math.Clamp(d01 / 100, 0, 1);
        }
        return 0d;
    }
}