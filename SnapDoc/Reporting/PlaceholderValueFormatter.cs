#nullable enable
using System.Globalization;

namespace SnapDoc.Reporting;

public static class PlaceholderValueFormatter
{
    public static string? Format(object? value, ArgumentSet arguments, string defaultCulture = "de-CH")
    {
        if (value == null)
            return arguments.Get("fallback");

        CultureInfo culture = GetCulture( arguments.Get("culture"), defaultCulture);

        string? text;

        if (value is bool boolean)
            text = boolean ? arguments.Get("true") ?? "Ja" : arguments.Get("false") ?? "Nein";
        else if (IsNumeric(value))
        {
            string? format = arguments.Get("format");

            text = value switch
            {
                byte v => v.ToString(format, culture),
                sbyte v => v.ToString(format, culture),
                short v => v.ToString(format, culture),
                ushort v => v.ToString(format, culture),
                int v => v.ToString(format, culture),
                uint v => v.ToString(format, culture),
                long v => v.ToString(format, culture),
                ulong v => v.ToString(format, culture),
                float v => v.ToString(format, culture),
                double v => v.ToString(format, culture),
                decimal v => v.ToString(format, culture),

                _ => value.ToString()
            };
        }
        else
            text = value.ToString();

        if (IsZero(value) && arguments.GetBool( "emptyIfZero", false))
            text = string.Empty;

        if (arguments.GetBool("trim", false))
            text = text?.Trim();

        if (arguments.GetBool("uppercase", false))
            text = text?.ToUpper(culture);

        if (arguments.GetBool("lowercase", false))
            text = text?.ToLower(culture);

        if (string.IsNullOrEmpty(text))
            return arguments.Get("fallback");

        return arguments.Get("prefix") + text + arguments.Get("suffix");
    }

    private static CultureInfo GetCulture(string? name, string fallback)
    {
        try
        {
            return CultureInfo.GetCultureInfo(name ?? fallback);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(fallback);
        }
    }

    private static bool IsZero(object value)
    {
        return value switch
        {
            byte number => number == 0,
            short number => number == 0,
            int number => number == 0,
            long number => number == 0,
            float number => number == 0,
            double number => number == 0,
            decimal number => number == 0,
            _ => false
        };
    }

    private static bool IsNumeric(object value)
    {
        return value is
            byte or
            sbyte or
            short or
            ushort or
            int or
            uint or
            long or
            ulong or
            float or
            double or
            decimal;
    }
}