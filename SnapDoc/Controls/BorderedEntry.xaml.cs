using SkiaSharp.Views.Maui;
using SkiaSharp;

namespace SnapDoc.Controls;

public partial class BorderedEntry : ContentView
{
    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(BorderedEntry), defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnTextChanged);
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(BorderedEntry), string.Empty);
    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(BorderedEntry), Colors.Gray);
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(BorderedEntry), Colors.Gray);
    public static readonly BindableProperty FocusedBorderColorProperty = BindableProperty.Create(nameof(FocusedBorderColor), typeof(Color), typeof(BorderedEntry), Color.FromArgb("#512BD4"));
    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(nameof(BorderThickness), typeof(double), typeof(BorderedEntry), 1.0);
    public static readonly BindableProperty FocusedBorderThicknessProperty = BindableProperty.Create(nameof(FocusedBorderThickness), typeof(double), typeof(BorderedEntry), 1.0);
    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(BorderedEntry), new CornerRadius(8));
    public static readonly BindableProperty IsPasswordProperty = BindableProperty.Create(nameof(IsPassword), typeof(bool), typeof(BorderedEntry), false);
    public static readonly BindableProperty KeyboardProperty = BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(BorderedEntry), Keyboard.Default);
    public static new readonly BindableProperty BackgroundColorProperty = BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(BorderedEntry), Colors.White);
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(BorderedEntry), defaultValue: (Color)Entry.TextColorProperty.DefaultValue);
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create( nameof(FontSize), typeof(double), typeof(BorderedEntry), defaultValue: (double)Entry.FontSizeProperty.DefaultValue);
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create( nameof(FontFamily), typeof(string), typeof(BorderedEntry), defaultValue: (string)Entry.FontFamilyProperty.DefaultValue);

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

        this.Loaded += (s, e) =>
        {
            UpdateFloatingLabelState(animate: false);
            BorderCanvas?.InvalidateSurface();
        };

        this.Unloaded += (s, e) =>
        {
            FloatingLabel.CancelAnimations();
        };

        FloatingLabel.SizeChanged += (s, e) =>
        {
            UpdateFloatingLabelState(animate: false);
            BorderCanvas?.InvalidateSurface();
        };
    }

    private void InnerEntry_Focused(object sender, FocusEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
        BorderCanvas.InvalidateSurface();
    }

    private void InnerEntry_Unfocused(object sender, FocusEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
        BorderCanvas.InvalidateSurface();
    }

    private void InnerEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateFloatingLabelState(animate: true);
        TextChanged?.Invoke(this, e);
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var info = e.Info;
        var canvas = e.Surface.Canvas;

        canvas.Clear();

        // Aktuellen Zustand auslesen
        bool isFocused = InnerEntry.IsFocused;
        bool hasText = !string.IsNullOrEmpty(InnerEntry.Text);
        bool shouldFloat = isFocused || hasText;

        Color currentBorderColor = isFocused ? FocusedBorderColor : BorderColor;
        double currentThickness = isFocused ? FocusedBorderThickness : BorderThickness;

        // Skalierungsfaktor für scharfe Darstellung (DIPs zu Pixeln)
        float scale = (float)DeviceDisplay.MainDisplayInfo.Density;
        float strokeWidth = (float)currentThickness * scale;
        float cornerRadius = (float)CornerRadius.TopLeft * scale;
        float hs = strokeWidth / 2f; // Halbe Strichdicke für saubere Ränder
        float r = cornerRadius;
        float w = info.Width;
        float h = info.Height;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = currentBorderColor.ToSKColor(),
            StrokeWidth = strokeWidth,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round // Runde Enden an der Unterbrechung
        };

        using var pathBuilder = new SKPathBuilder();

        if (shouldFloat && !string.IsNullOrEmpty(Placeholder))
        {
            double actualLabelWidth = FloatingLabel.Width > 0
                ? FloatingLabel.Width
                : (Placeholder.Length * FontSize * 0.6);
            float dipGapStart = 10f - 2f;
            float dipGapEnd = 10f + (float)(actualLabelWidth * 0.8) + 2f;
            float gapStart = dipGapStart * scale;
            float gapEnd = dipGapEnd * scale;


            pathBuilder.MoveTo(r + hs, hs);
            pathBuilder.LineTo(gapStart, hs);
            pathBuilder.MoveTo(gapEnd, hs);
            pathBuilder.LineTo(w - r - hs, hs);
            pathBuilder.ArcTo(new SKRect(w - 2 * r - hs, hs, w - hs, 2 * r + hs), 270, 90, false);
            pathBuilder.LineTo(w - hs, h - r - hs);
            pathBuilder.ArcTo(new SKRect(w - 2 * r - hs, h - 2 * r - hs, w - hs, h - hs), 0, 90, false);
            pathBuilder.LineTo(r + hs, h - hs);
            pathBuilder.ArcTo(new SKRect(hs, h - 2 * r - hs, 2 * r + hs, h - hs), 90, 90, false);
            pathBuilder.LineTo(hs, r + hs);
            pathBuilder.ArcTo(new SKRect(hs, hs, 2 * r + hs, 2 * r + hs), 180, 90, false);
        }
        else
        {
            pathBuilder.MoveTo(r + hs, hs);
            pathBuilder.LineTo(w - r - hs, hs);
            pathBuilder.ArcTo(new SKRect(w - 2 * r - hs, hs, w - hs, 2 * r + hs), 270, 90, false);
            pathBuilder.LineTo(w - hs, h - r - hs);
            pathBuilder.ArcTo(new SKRect(w - 2 * r - hs, h - 2 * r - hs, w - hs, h - hs), 0, 90, false);
            pathBuilder.LineTo(r + hs, h - hs);
            pathBuilder.ArcTo(new SKRect(hs, h - 2 * r - hs, 2 * r + hs, h - hs), 90, 90, false);
            pathBuilder.LineTo(hs, r + hs);
            pathBuilder.ArcTo(new SKRect(hs, hs, 2 * r + hs, 2 * r + hs), 180, 90, false);
        }

        using var path = pathBuilder.Detach();
        canvas.DrawPath(path, paint);
    }

    private void UpdateFloatingLabelState(bool animate)
    {
        if (this.Handler == null || !this.IsLoaded)
            return;

        bool hasText = !string.IsNullOrEmpty(Text);
        bool isFocused = InnerEntry.IsFocused;
        bool shouldFloat = isFocused || hasText;
        double fineTuningOffsetY = 6.0;

        Dispatcher.Dispatch(() =>
        {
            if (this.Handler == null || !this.IsLoaded)
                return;

            FloatingLabel.TextColor = isFocused ? FocusedBorderColor : PlaceholderColor;

            double floatingY;
            if (FloatingLabel.Height > 0)
                floatingY = -(FloatingLabel.Y + (FloatingLabel.Height / 2.0)) + fineTuningOffsetY;
            else
                floatingY = -16;

            if (shouldFloat)
            {
                if (animate)
                {
                    FloatingLabel.TranslateToAsync(0, floatingY, 150, Easing.CubicOut);
                    FloatingLabel.ScaleToAsync(0.8, 150, Easing.CubicOut);
                }
                else
                {
                    FloatingLabel.TranslationY = floatingY;
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
        });
    }

    private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is BorderedEntry control)
        {
            if (control.InnerEntry != null && control.InnerEntry.Text != control.Text)
                control.InnerEntry.Text = control.Text;

            control.UpdateFloatingLabelState(animate: false);
            control.BorderCanvas?.InvalidateSurface();
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        UpdateFloatingLabelState(animate: false);
        BorderCanvas?.InvalidateSurface();
    }
}