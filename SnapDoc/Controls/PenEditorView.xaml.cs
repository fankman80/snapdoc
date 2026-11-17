#nullable disable
using Microsoft.Maui.Controls.Shapes;
using SnapDoc.Resources.Languages;
using System.Text.RegularExpressions;

namespace SnapDoc.Controls;

public partial class PenEditorView : ContentView
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    private static readonly int[] Widths = [0, 1, 2, 3, 5, 8, 12, 16];

    private static readonly (string Name, string Pattern)[] Patterns =
    [
        (AppResources.keine_linie, ""),
        (AppResources.gestrichelt, "12 6"),
        (AppResources.gepunktet, "2 5"),
        (AppResources.strichpunkt, "12 4 2 4")
    ];

    public event EventHandler StyleChanged;

    private bool _loading;
    private readonly List<(Border Border, int Width)> _widthItems = [];
    private readonly List<(Border Border, string Pattern)> _patternItems = [];
    private Border _customItem;
    private Color lineColor = Colors.Black;
    public Color LineColor
    {
        get => lineColor;
        set { if (lineColor != value) { lineColor = value; OnPropertyChanged(); Raise(); } }
    }

    private int lineWidth = 3;
    public int LineWidth
    {
        get => lineWidth;
        set
        {
            if (lineWidth != value)
            {
                lineWidth = value;
                OnPropertyChanged();
                UpdateWidthSelection();
                UpdatePatternEnabled();
                Raise();
            }
        }
    }

    private void UpdatePatternEnabled()
    {
        if (PatternSection == null) return;   // Setter kann vor InitializeComponent laufen
        PatternSection.IsEnabled = lineWidth > 0;
        PatternSection.Opacity = lineWidth > 0 ? 1 : 0.4;
    }

    private string strokeStyle = "";
    public string StrokeStyle
    {
        get => strokeStyle;
        set
        {
            value ??= "";
            if (strokeStyle != value)
            {
                strokeStyle = value;
                OnPropertyChanged();
                UpdatePatternSelection();
                Raise();
            }
        }
    }

    public PenEditorView()
    {
        InitializeComponent();
        BindingContext = this;
        BuildWidthPresets();
        BuildPatternPresets();
    }

    public void Load(Color color, int width, string pattern)
    {
        _loading = true;
        LineColor = color;
        LineWidth = width;
        StrokeStyle = pattern;
        _loading = false;

        // Feld nur zeigen, wenn ein eigenes Muster aktiv ist
        var current = Normalize(StrokeStyle);
        CustomPatternEntry.IsVisible = !_patternItems.Any(i => Normalize(i.Pattern) == current);

        ScrollToSelection();
    }

    private void ScrollToSelection()
    {
        // Erst nach dem Layout-Durchlauf scrollen
        Dispatcher.Dispatch(async () =>
        {
            var w = _widthItems.FirstOrDefault(i => i.Width == LineWidth).Border;
            if (w != null)
                await WidthScroll.ScrollToAsync(w, ScrollToPosition.Center, false);

            var current = Normalize(StrokeStyle);
            var p = _patternItems.FirstOrDefault(i => Normalize(i.Pattern) == current).Border ?? _customItem;
            if (p != null)
                await PatternScroll.ScrollToAsync(p, ScrollToPosition.Center, false);
        });
    }

    private void Raise()
    {
        if (!_loading)
            StyleChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BuildWidthPresets()
    {
        foreach (var w in Widths)
        {
            View inner;
            if (w == 0)
            {
                // "Keine Linie": Schrägstrich wie bei "keine Farbe"
                inner = new BoxView
                {
                    Color = Color.FromArgb("#E53935"),
                    WidthRequest = 2.5,
                    HeightRequest = 26,
                    Rotation = 45,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }
            else
            {
                double dot = Math.Clamp(w + 3, 5, 22);
                inner = new Ellipse
                {
                    Fill = Colors.White,
                    WidthRequest = dot,
                    HeightRequest = dot,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };
            }

            var item = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                StrokeThickness = 1.5,
                Content = inner
            };
            ToolTipProperties.SetText(item, w == 0 ? AppResources.keine_linie : $"{w} px");
            AddTap(item, () => LineWidth = w);
            _widthItems.Add((item, w));
            WidthPresets.Children.Add(item);
        }
        UpdateWidthSelection();
    }

    private void BuildPatternPresets()
    {
        foreach (var (name, pattern) in Patterns)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 48,
                Y2 = 0,
                Stroke = Colors.White,
                StrokeThickness = 3,
                VerticalOptions = LayoutOptions.Center
            };
            var dashes = Helper.ParseDashArray(pattern);
            if (dashes is { Length: > 0 })
            {
                // StrokeDashArray in MAUI ist relativ zur Strichbreite
                line.StrokeDashArray = [.. dashes.Select(d => (double)d / 3.0)];
            }

            var item = new Border
            {
                HeightRequest = 36,
                Padding = new Thickness(14, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                StrokeThickness = 1.5,
                Content = line
            };
            ToolTipProperties.SetText(item, name);
            AddTap(item, () => StrokeStyle = pattern);
            _patternItems.Add((item, pattern));
            PatternPresets.Children.Add(item);
        }

        _customItem = new Border
        {
            HeightRequest = 36,
            Padding = new Thickness(14, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            StrokeThickness = 1.5,
            Content = new Label
            {
                Text = "✎",
                TextColor = Colors.White,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Center
            }
        };
        ToolTipProperties.SetText(_customItem, AppResources.eigenes_muster_platzhalter);
        AddTap(_customItem, () =>
        {
            CustomPatternEntry.IsVisible = !CustomPatternEntry.IsVisible;
            if (CustomPatternEntry.IsVisible)
                CustomPatternEntry.Focus();
        });
        PatternPresets.Children.Add(_customItem);
        UpdatePatternSelection();
    }

    private void UpdateWidthSelection()
    {
        foreach (var (border, w) in _widthItems)
            Mark(border, w == LineWidth);
    }

    private void UpdatePatternSelection()
    {
        var current = Normalize(StrokeStyle);
        bool anyPreset = false;

        foreach (var (border, p) in _patternItems)
        {
            bool sel = Normalize(p) == current;
            anyPreset |= sel;
            Mark(border, sel);
        }

        if (_customItem != null)
            Mark(_customItem, !anyPreset);
    }

    private static string Normalize(string s) => MultipleSpacesRegex().Replace((s ?? "").Trim(), " ");

    private static void Mark(Border border, bool selected)
    {
        border.Stroke = selected ? ColorSwatchBar.Accent : Color.FromArgb("#33FFFFFF");

        if (selected)
            border.SetDynamicResource(BackgroundColorProperty, "PrimaryAlpha");
        else
        {
            border.RemoveDynamicResource(BackgroundColorProperty);
            border.BackgroundColor = Colors.Transparent;
        }
    }

    private static void AddTap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(tap);
    }

    private void OnStrokeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue == null) return;
        var filtered = string.Concat(e.NewTextValue.Where(c => char.IsDigit(c) || c == ' '));
        StrokeStyle = MultipleSpacesRegex().Replace(filtered, " ");
    }
}
