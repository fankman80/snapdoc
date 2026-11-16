#nullable disable
using CommunityToolkit.Maui.Extensions;
using SnapDoc.Views;

namespace SnapDoc.Controls;

public partial class ColorSwatchBar : ContentView
{
    public static readonly string[] Palette =
    [
        "#FFFFFF", "#000000", "#E53935", "#FB8C00", "#FDD835",
        "#43A047", "#1BB0CE", "#1E88E5", "#8E24AA", "#9E9E9E"
    ];

    public static readonly BindableProperty SelectedColorProperty = BindableProperty.Create(
        nameof(SelectedColor), typeof(Color), typeof(ColorSwatchBar), Colors.Black, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((ColorSwatchBar)b).UpdateSelection());

    public static readonly BindableProperty ShowNoneSwatchProperty = BindableProperty.Create(
        nameof(ShowNoneSwatch), typeof(bool), typeof(ColorSwatchBar), false,
        propertyChanged: (b, _, _) => ((ColorSwatchBar)b).Build());

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool ShowNoneSwatch
    {
        get => (bool)GetValue(ShowNoneSwatchProperty);
        set => SetValue(ShowNoneSwatchProperty, value);
    }

    public float DefaultAlpha { get; set; } = 1f;

    public event EventHandler ColorChanged;

    internal static Color Accent =>
        Application.Current?.Resources.TryGetValue("PrimaryDark", out var c) == true && c is Color color
            ? color
            : Color.FromArgb("#1BB0CE");

    private readonly WrapLayout _row;
    private readonly List<(Border Border, Color Color)> _swatches = [];
    private Border _noneSwatch;
    private Border _customSwatch;

    public ColorSwatchBar()
    {
        _row = new WrapLayout { Spacing = 10, RowSpacing = 10, Padding = new Thickness(4) };
        Content = _row;
        Build();
    }

    private void Build()
    {
        _row.Children.Clear();
        _swatches.Clear();
        _noneSwatch = null;

        if (ShowNoneSwatch)
        {
            // Weisser Kreis mit rotem Schrägstrich
            var slash = new BoxView
            {
                Color = Color.FromArgb("#E53935"),
                WidthRequest = 3,
                HeightRequest = 30,
                Rotation = 45,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
            _noneSwatch = CreateSwatch(Colors.White, slash);
            AddTap(_noneSwatch, () => Pick(null));
            _row.Children.Add(_noneSwatch);
        }

        foreach (var hex in Palette)
        {
            var color = Color.FromArgb(hex);
            var swatch = CreateSwatch(color);
            AddTap(swatch, () => Pick(color));
            _swatches.Add((swatch, color));
            _row.Children.Add(swatch);
        }

        // Eigener Farbwähler (Regenbogen-Verlauf)
        _customSwatch = CreateSwatch(Colors.Transparent);
        _customSwatch.Background = new LinearGradientBrush(
        [
            new GradientStop(Color.FromArgb("#E53935"), 0f),
            new GradientStop(Color.FromArgb("#FDD835"), 0.25f),
            new GradientStop(Color.FromArgb("#43A047"), 0.5f),
            new GradientStop(Color.FromArgb("#1E88E5"), 0.75f),
            new GradientStop(Color.FromArgb("#8E24AA"), 1f)
        ], new Point(0, 0), new Point(1, 1));
        AddTap(_customSwatch, OpenCustomPicker);
        _row.Children.Add(_customSwatch);

        UpdateSelection();
    }

    private static Border CreateSwatch(Color color, View content = null) => new()
    {
        WidthRequest = 32,
        HeightRequest = 32,
        Padding = 0,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
        StrokeThickness = 1.5,
        Stroke = Color.FromArgb("#55FFFFFF"),
        BackgroundColor = color,
        Content = content
    };

    private static void AddTap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(tap);
    }

    private void Pick(Color color)
    {
        var current = SelectedColor ?? Colors.Black;
        SelectedColor = color == null
            ? current.WithAlpha(0f)
            : color.WithAlpha(current.Alpha > 0f ? current.Alpha : DefaultAlpha);
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OpenCustomPicker()
    {
        var current = SelectedColor ?? Colors.Black;
        var popup = new PopupColorPicker(current, fillOpacity: (byte)(current.Alpha * 255), fillOpacityVisibility: true);
        var result = await Shell.Current.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        SelectedColor = Color.FromArgb(result.Result.ColorHex).WithAlpha(result.Result.FillOpacity / 255f);
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelection()
    {
        var c = SelectedColor ?? Colors.Black;
        bool isNone = c.Alpha <= 0.001f;
        bool matched = isNone && _noneSwatch != null;

        foreach (var (border, color) in _swatches)
        {
            bool sel = !isNone && SameRgb(color, c);
            matched |= sel;
            Mark(border, sel);
        }

        if (_noneSwatch != null)
            Mark(_noneSwatch, isNone);

        // Eigene Farbe: hervorheben, wenn keine Palettenfarbe passt
        Mark(_customSwatch, !matched);
    }

    private static void Mark(Border border, bool selected)
    {
        if (border == null) return;
        border.Stroke = selected ? Accent : Color.FromArgb("#55FFFFFF");
        border.StrokeThickness = selected ? 3 : 1.5;
        border.Scale = selected ? 1.12 : 1.0;
    }

    private static bool SameRgb(Color a, Color b) =>
        Math.Abs(a.Red - b.Red) < 0.01f &&
        Math.Abs(a.Green - b.Green) < 0.01f &&
        Math.Abs(a.Blue - b.Blue) < 0.01f;
}
