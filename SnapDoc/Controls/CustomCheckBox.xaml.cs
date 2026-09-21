#nullable disable
using System.Windows.Input;

namespace SnapDoc.Controls;

public partial class CustomCheckBox : ContentView
{
    // ---------- Status ----------
    public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
        nameof(IsChecked), typeof(bool), typeof(CustomCheckBox), false,
        BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

    public static readonly BindableProperty IsEnabledCheckProperty = BindableProperty.Create(
        nameof(IsEnabledCheck), typeof(bool), typeof(CustomCheckBox), true,
        propertyChanged: OnVisualStateChanged);

    // ---------- Farben ----------
    public static readonly BindableProperty CheckedBackgroundColorProperty = BindableProperty.Create(
        nameof(CheckedBackgroundColor), typeof(Color), typeof(CustomCheckBox), Color.FromArgb("#007AFF"),
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty UncheckedBackgroundColorProperty = BindableProperty.Create(
        nameof(UncheckedBackgroundColor), typeof(Color), typeof(CustomCheckBox), Colors.Transparent,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty CheckedBorderColorProperty = BindableProperty.Create(
        nameof(CheckedBorderColor), typeof(Color), typeof(CustomCheckBox), null,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor), typeof(Color), typeof(CustomCheckBox), Colors.DarkGray,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty CheckColorProperty = BindableProperty.Create(
        nameof(CheckColor), typeof(Color), typeof(CustomCheckBox), Colors.White,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty DisabledColorProperty = BindableProperty.Create(
        nameof(DisabledColor), typeof(Color), typeof(CustomCheckBox), Colors.LightGray,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(CustomCheckBox), Label.TextColorProperty.DefaultValue);

    // ---------- Geometrie / Typo ----------
    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), typeof(double), typeof(CustomCheckBox), 1.5);

    public static readonly BindableProperty BoxSizeProperty = BindableProperty.Create(
        nameof(BoxSize), typeof(double), typeof(CustomCheckBox), 26.0,
        propertyChanged: OnBoxSizeChanged);

    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing), typeof(double), typeof(CustomCheckBox), 10.0);

    public static readonly BindableProperty ControlPaddingProperty = BindableProperty.Create(
        nameof(ControlPadding), typeof(Thickness), typeof(CustomCheckBox), new Thickness(0, 4));

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(CustomCheckBox), string.Empty);

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily), typeof(string), typeof(CustomCheckBox), (string)Label.FontFamilyProperty.DefaultValue);

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(CustomCheckBox), Label.FontSizeProperty.DefaultValue);

    public static readonly BindableProperty CheckGlyphProperty = BindableProperty.Create(
        nameof(CheckGlyph), typeof(string), typeof(CustomCheckBox), MaterialIcons.Check_small);

    public static readonly BindableProperty CheckGlyphFontFamilyProperty = BindableProperty.Create(
        nameof(CheckGlyphFontFamily), typeof(string), typeof(CustomCheckBox), "MaterialOutlined",
        propertyChanged: OnVisualStateChanged);
    
    public static readonly BindableProperty CheckGlyphScaleProperty = BindableProperty.Create(
        nameof(CheckGlyphScale), typeof(double), typeof(CustomCheckBox), 0.95,
        propertyChanged: OnVisualStateChanged);

    public static readonly BindableProperty AnimateProperty = BindableProperty.Create(
        nameof(Animate), typeof(bool), typeof(CustomCheckBox), true);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(CustomCheckBox), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(CustomCheckBox), null);

    // ---------- CLR-Wrapper ----------
    public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
    public bool IsEnabledCheck { get => (bool)GetValue(IsEnabledCheckProperty); set => SetValue(IsEnabledCheckProperty, value); }
    public Color CheckedBackgroundColor { get => (Color)GetValue(CheckedBackgroundColorProperty); set => SetValue(CheckedBackgroundColorProperty, value); }
    public Color UncheckedBackgroundColor { get => (Color)GetValue(UncheckedBackgroundColorProperty); set => SetValue(UncheckedBackgroundColorProperty, value); }
    public Color CheckedBorderColor { get => (Color)GetValue(CheckedBorderColorProperty); set => SetValue(CheckedBorderColorProperty, value); }
    public Color BorderColor { get => (Color)GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }
    public Color CheckColor { get => (Color)GetValue(CheckColorProperty); set => SetValue(CheckColorProperty, value); }
    public Color DisabledColor { get => (Color)GetValue(DisabledColorProperty); set => SetValue(DisabledColorProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public double BoxSize { get => (double)GetValue(BoxSizeProperty); set => SetValue(BoxSizeProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    public Thickness ControlPadding { get => (Thickness)GetValue(ControlPaddingProperty); set => SetValue(ControlPaddingProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string CheckGlyph { get => (string)GetValue(CheckGlyphProperty); set => SetValue(CheckGlyphProperty, value); }
    public string CheckGlyphFontFamily { get => (string)GetValue(CheckGlyphFontFamilyProperty); set => SetValue(CheckGlyphFontFamilyProperty, value); }
    public double CheckGlyphScale { get => (double)GetValue(CheckGlyphScaleProperty); set => SetValue(CheckGlyphScaleProperty, value); }
    public bool Animate { get => (bool)GetValue(AnimateProperty); set => SetValue(AnimateProperty, value); }
    public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public event EventHandler<CheckedChangedEventArgs> CheckedChanged;

    public CustomCheckBox()
    {
        InitializeComponent();
        UpdateShape();
        UpdateVisualState(false);
    }

    // ---------- PropertyChanged-Handler ----------
    private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CustomCheckBox control) return;

        control.UpdateVisualState(control.Animate);
        control.CheckedChanged?.Invoke(control, new CheckedChangedEventArgs((bool)newValue));

        if (control.Command?.CanExecute(control.CommandParameter) == true)
            control.Command.Execute(control.CommandParameter);
    }

    private static void OnVisualStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomCheckBox control)
            control.UpdateVisualState(false);
    }

    private static void OnBoxSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is CustomCheckBox control)
        {
            control.UpdateShape();
            control.UpdateVisualState(false);
        }
    }

    // ---------- Darstellung ----------
    private void UpdateShape()
    {
        // Radius = halbe Kantenlänge => exakter Kreis
        BoxShape.CornerRadius = new CornerRadius(BoxSize / 2.0);
    }

    private void UpdateVisualState(bool animated)
    {
        var checkedBorder = CheckedBorderColor ?? CheckedBackgroundColor;

        if (!IsEnabledCheck)
        {
            BoxBorder.Stroke = DisabledColor;
            BoxBorder.Background = IsChecked ? DisabledColor : UncheckedBackgroundColor;
        }
        else
        {
            BoxBorder.Stroke = IsChecked ? checkedBorder : BorderColor;
            BoxBorder.Background = IsChecked ? CheckedBackgroundColor : UncheckedBackgroundColor;
        }

        GlyphLabel.TextColor = CheckColor;
        GlyphLabel.FontSize = BoxSize * CheckGlyphScale;
        GlyphLabel.FontAttributes = string.IsNullOrEmpty(CheckGlyphFontFamily)
            ? FontAttributes.Bold
            : FontAttributes.None;
        TextLabel.Opacity = IsEnabledCheck ? 1.0 : 0.5;

        if (animated)
            _ = AnimateStateAsync();
        else
        {
            GlyphLabel.Opacity = IsChecked ? 1 : 0;
            GlyphLabel.Scale = IsChecked ? 1 : 0.6;
        }
    }

    private async Task AnimateStateAsync()
    {
        if (IsChecked)
        {
            GlyphLabel.Scale = 0.6;
            GlyphLabel.Opacity = 0;
            await Task.WhenAll(
                GlyphLabel.FadeTo(1, 120, Easing.CubicOut),
                GlyphLabel.ScaleTo(1.15, 110, Easing.CubicOut));
            await GlyphLabel.ScaleTo(1.0, 80, Easing.CubicIn);
        }
        else
        {
            await Task.WhenAll(
                GlyphLabel.FadeTo(0, 100, Easing.CubicIn),
                GlyphLabel.ScaleTo(0.6, 100, Easing.CubicIn));
        }
    }

    // ---------- Interaktion ----------
    private async void OnTapped(object sender, TappedEventArgs e)
    {
        if (!IsEnabledCheck) return;

        if (Animate)
        {
            await BoxBorder.ScaleTo(0.88, 60, Easing.CubicOut);
            await BoxBorder.ScaleTo(1.0, 60, Easing.CubicIn);
        }

        IsChecked = !IsChecked;
    }
}
