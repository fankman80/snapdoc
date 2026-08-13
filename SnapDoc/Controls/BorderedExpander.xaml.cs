namespace SnapDoc.Controls;

public partial class BorderedExpander : ContentView
{
    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(BorderedEntry),
            defaultValue: (Color)Entry.TextColorProperty.DefaultValue);

    public static readonly BindableProperty FontSizeProperty =
            BindableProperty.Create(
                nameof(FontSize),
                typeof(double),
                typeof(BorderedEntry),
                defaultValue: (double)Entry.FontSizeProperty.DefaultValue);

    public static readonly BindableProperty FontFamilyProperty =
            BindableProperty.Create(
                nameof(FontFamily),
                typeof(string),
                typeof(BorderedEntry),
                defaultValue: (string)Entry.FontFamilyProperty.DefaultValue);

    public static readonly BindableProperty HeaderTextProperty =
        BindableProperty.Create(nameof(HeaderText), typeof(string), typeof(BorderedExpander), string.Empty);

    public static readonly BindableProperty CornerRadiusProperty =
    BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(BorderedEditor), new CornerRadius(8));

    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public BorderedExpander()
    {
        InitializeComponent();
    }
}