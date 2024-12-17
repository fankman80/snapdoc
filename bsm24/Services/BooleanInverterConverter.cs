#nullable disable

using System.Globalization;

namespace bsm24.Services
{
    public class BooleanInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue; // Umkehrt den Bool-Wert
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return !booleanValue; // Umkehrt den Bool-Wert beim Rückkonvertieren
            }
            return false;
        }
    }
}

