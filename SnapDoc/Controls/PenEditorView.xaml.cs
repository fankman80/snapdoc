#nullable disable
using Microsoft.Maui.Controls.Shapes;
using System.Text.RegularExpressions;

namespace SnapDoc.Controls;

public partial class PenEditorView : ContentView
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpacesRegex();

    private static readonly int[] Widths = [1, 2, 3, 5, 8, 12, 16];

    private static readonly (string Name, string Pattern)[] Patterns =
    [
        ("Durchgezogen", ""),
        ("Gestrichelt", "12 6"),
        ("Gepunktet", "2 5"),
        ("Strichpunkt", "12 4 2 4")
    ];

    public event EventHandler StyleChanged;

    private bool _loading;
    private readonly List<(Border Border, int Width)> _widthItems = [];
    private readonly List<(Border Border, string Pattern)> _patternItems = [];

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
                Raise();
            }
        }
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
    }

    private void Raise()
    {
        if (!_loading)
            StyleChanged?.Invoke(this, EventArgs.Empty);
    }

    #region Presets

    private void BuildWidthPresets()
    {
        foreach (var w in Widths)
        {
            double dot = Math.Clamp(w + 3, 5, 22);
            var item = new Border
            {
                WidthRequest = 40,
                HeightRequest = 40,
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                StrokeThickness = 1.5,
                Content = new Ellipse
                {
                    Fill = Colors.White,
                    WidthRequest = dot,
                    HeightRequest = dot,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            ToolTipProperties.SetText(item, $"{w} px");
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
        foreach (var (border, p) in _patternItems)
            Mark(border, Normalize(p) == current);
    }

    private static string Normalize(string s) => MultipleSpacesRegex().Replace((s ?? "").Trim(), " ");

    private static void Mark(Border border, bool selected)
    {
        border.Stroke = selected ? ColorSwatchBar.Accent : Color.FromArgb("#33FFFFFF");
        border.BackgroundColor = selected ? Color.FromArgb("{DynamicResource PrimaryAlpha}") : Colors.Transparent;
    }

    private static void AddTap(View view, Action action)
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => action();
        view.GestureRecognizers.Add(tap);
    }

    #endregion

    private void OnStrokeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.NewTextValue == null) return;
        var filtered = string.Concat(e.NewTextValue.Where(c => char.IsDigit(c) || c == ' '));
        StrokeStyle = MultipleSpacesRegex().Replace(filtered, " ");
    }
}
