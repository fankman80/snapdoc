using SnapDoc.Resources.Languages;
using System.Globalization;

namespace SnapDoc.Controls;

public partial class LocalizedLabel : Label
{
    public static readonly BindableProperty ResourceKeyProperty =
        BindableProperty.Create(
            nameof(ResourceKey),
            typeof(string),
            typeof(LocalizedLabel),
            propertyChanged: (_, __, ___) => ((LocalizedLabel)_).UpdateText());

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(
            nameof(Value),
            typeof(object),
            typeof(LocalizedLabel),
            propertyChanged: (_, __, ___) => ((LocalizedLabel)_).UpdateText());

    public static readonly BindableProperty ValueFormatProperty =
        BindableProperty.Create(
            nameof(ValueFormat),
            typeof(string),
            typeof(LocalizedLabel),
            "{0}",
            propertyChanged: (_, __, ___) => ((LocalizedLabel)_).UpdateText());

    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    public string ResourceKey
    {
        get => (string)GetValue(ResourceKeyProperty);
        set => SetValue(ResourceKeyProperty, value);
    }

    public object Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private void UpdateText()
    {
        if (string.IsNullOrEmpty(ResourceKey))
            return;

        var label = AppResources.ResourceManager.GetString(
            ResourceKey,
            CultureInfo.CurrentUICulture);

        if (Value != null)
        {
            string formattedValue;
            try
            {
                // FormatValue als Standard .NET String.Format
                formattedValue = string.Format(CultureInfo.CurrentUICulture, ValueFormat, Value ?? string.Empty);
            }
            catch
            {
                // Fallback: Wert ist null → leeren String ausgeben
                formattedValue = string.Empty;
            }

            Text = $"{label}{formattedValue}";
        }
        else
        {
            Text = label;
        }
    }
}
