namespace SnapDoc.Controls;

public partial class BorderedEntry : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(BorderedEntry), defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(BorderedEntry), string.Empty);

    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(BorderedEntry), Colors.Gray);

    public static readonly BindableProperty BorderColorProperty =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(BorderedEntry), Colors.Gray);

    public static readonly BindableProperty FocusedBorderColorProperty =
        BindableProperty.Create(nameof(FocusedBorderColor), typeof(Color), typeof(BorderedEntry), Color.FromArgb("#512BD4"));

    public static readonly BindableProperty BorderThicknessProperty =
        BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(BorderedEntry), 1.0);

    public static readonly BindableProperty FocusedBorderThicknessProperty =
        BindableProperty.Create(nameof(FocusedBorderThickness), typeof(double), typeof(BorderedEntry), 1.0);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(BorderedEntry), new CornerRadius(6));

    public static readonly BindableProperty IsPasswordProperty =
        BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(BorderedEntry), false);

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(BorderedEntry), Keyboard.Default);

    public static new readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(BorderedEntry), Colors.White);

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

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public Color PlaceholderColor { get => (Color)GetValue(PlaceholderColorProperty); set => SetValue(PlaceholderColorProperty, value); }
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    public Color FocusedBorderColor { get => (Color)GetValue(FocusedBorderColorProperty); set => SetValue(FocusedBorderColorProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public double FocusedBorderThickness { get => (double)GetValue(FocusedBorderThicknessProperty); set => SetValue(FocusedBorderThicknessProperty, value); }
    public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool IsPassword { get => (bool)GetValue(IsPasswordProperty); set => SetValue(IsPasswordProperty, value); }
    public Keyboard Keyboard { get => (Keyboard)GetValue(KeyboardProperty); set => SetValue(KeyboardProperty, value); }
    public new Color BackgroundColor { get => (Color)GetValue(BackgroundColorProperty); set => SetValue(BackgroundColorProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public BorderedEntry()
    {
        InitializeComponent();
        this.Loaded += (s, e) => UpdateFloatingLabelState(animate: false);
    }

    private void InnerEntry_Focused(object sender, FocusEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
    }

    private void InnerEntry_Unfocused(object sender, FocusEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
    }

    private void InnerEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
    }

    private void UpdateFloatingLabelState(bool animate)
    {
        bool hasText = !string.IsNullOrEmpty(InnerEntry.Text);
        bool isFocused = InnerEntry.IsFocused;
        bool shouldFloat = isFocused || hasText;

        // Label-Farbe anpassen 
        FloatingLabel.TextColor = isFocused ? FocusedBorderColor : PlaceholderColor;

        if (shouldFloat)
        {
            if (animate)
            {
                // TranslateTo und ScaleTo statt TranslateToAsync / ScaleToAsync
                FloatingLabel.TranslateToAsync(0, -25, 150, Easing.CubicOut);
                FloatingLabel.ScaleToAsync(0.8, 150, Easing.CubicOut);
            }
            else
            {
                FloatingLabel.TranslationY = -25;
                FloatingLabel.Scale = 0.8;
            }
        }
        else
        {
            if (animate)
            {
                FloatingLabel.TranslateToAsync(0, 0, 150, Easing.CubicIn);
                FloatingLabel.ScaleToAsync(1.0, 150, Easing.CubicIn);
            }
            else
            {
                FloatingLabel.TranslationY = 0;
                FloatingLabel.Scale = 1.0;
            }
        }
    }
}