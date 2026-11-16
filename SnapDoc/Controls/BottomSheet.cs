#nullable disable
namespace SnapDoc.Controls;

/// <summary>
/// Nicht-modales Bottom Sheet im Stil der Editor-Leisten:
/// volle Breite, eckig, dunkel, feine Trennlinie oben.
/// Wirkt wie eine nach oben aufgeklappte Erweiterung der Bottombar.
/// </summary>
[ContentProperty(nameof(SheetContent))]
public partial class BottomSheet : ContentView
{
    #region Bindable Properties

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), typeof(View), typeof(BottomSheet),
        propertyChanged: (b, _, n) => ((BottomSheet)b)._scroll.Content = (View)n);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(BottomSheet), string.Empty,
        propertyChanged: (b, _, n) =>
        {
            var s = (BottomSheet)b;
            s._title.Text = (string)n;
            s._title.IsVisible = !string.IsNullOrEmpty((string)n);
        });

    public static readonly BindableProperty SheetColorProperty = BindableProperty.Create(
        nameof(SheetColor), typeof(Color), typeof(BottomSheet), Color.FromArgb("#2B2B2B"),
        propertyChanged: (b, _, n) => ((BottomSheet)b)._sheet.BackgroundColor = (Color)n);

    public static readonly BindableProperty DividerColorProperty = BindableProperty.Create(
        nameof(DividerColor), typeof(Color), typeof(BottomSheet), Color.FromArgb("#3D3D3D"),
        propertyChanged: (b, _, n) => ((BottomSheet)b)._divider.Color = (Color)n);

    public View SheetContent
    {
        get => (View)GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Hintergrund – gleiche Farbe wie die Bottombar verwenden.</summary>
    public Color SheetColor
    {
        get => (Color)GetValue(SheetColorProperty);
        set => SetValue(SheetColorProperty, value);
    }

    /// <summary>Farbe der Trennlinie oben (wie zwischen den Leisten).</summary>
    public Color DividerColor
    {
        get => (Color)GetValue(DividerColorProperty);
        set => SetValue(DividerColorProperty, value);
    }

    #endregion

    /// <summary>Maximale Höhe relativ zur Höhe des Elternelements.</summary>
    public double MaxHeightRatio { get; set; } = 0.6;

    /// <summary>Breite des Inhalts auf grossen Bildschirmen (Hintergrund bleibt volle Breite).</summary>
    public double MaxContentWidth { get; set; } = 640;

    public bool IsOpen { get; private set; }

    public event EventHandler Closed;

    private readonly Grid _sheet;
    private readonly BoxView _divider;
    private readonly Label _title;
    private readonly ScrollView _scroll;
    private VisualElement _parent;
    private double _dragY;

    // Kurzer Weg + Einblenden: das Sheet gleitet nicht über die Leisten
    private const double SlideDistance = 48;

    public BottomSheet()
    {
        IsVisible = false;
        Opacity = 0;
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Fill;

        _divider = new BoxView
        {
            HeightRequest = 1,
            Color = DividerColor,
            VerticalOptions = LayoutOptions.Start
        };

        var handle = new BoxView
        {
            WidthRequest = 32,
            HeightRequest = 4,
            CornerRadius = 2,
            Color = Color.FromArgb("#666666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _title = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#9E9E9E"),
            Margin = new Thickness(0, 4, 0, 0),
            IsVisible = false
        };

        var header = new VerticalStackLayout
        {
            BackgroundColor = Colors.Transparent, // Ziehen im ganzen Kopf
            Padding = new Thickness(20, 0, 20, 0),
            Children = { handle, _title }
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        header.GestureRecognizers.Add(pan);

        _scroll = new ScrollView { Padding = new Thickness(20, 8, 20, 12) };

        // Inhalt auf Tablet/Windows mittig begrenzen, Hintergrund bleibt volle Breite
        var content = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        content.Add(header, 0, 0);
        content.Add(_scroll, 0, 1);
        _content = content;

        _sheet = new Grid
        {
            BackgroundColor = SheetColor,
            Children = { content, _divider }
        };

        Content = _sheet;
    }

    private readonly Grid _content;

    #region Öffnen / Schliessen

    public async Task OpenAsync()
    {
        if (IsOpen) return;
        IsOpen = true;

        UpdateSize();
        TranslationY = SlideDistance;
        Opacity = 0;
        IsVisible = true;

        await Task.WhenAll(
            this.TranslateToAsync(0, 0, 200, Easing.CubicOut),
            this.FadeToAsync(1, 150, Easing.CubicOut));
    }

    public async Task CloseAsync()
    {
        if (!IsOpen) return;
        IsOpen = false;

        await Task.WhenAll(
            this.TranslateToAsync(0, SlideDistance, 150, Easing.CubicIn),
            this.FadeToAsync(0, 150, Easing.CubicIn));

        IsVisible = false;
        TranslationY = 0;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public Task ToggleAsync() => IsOpen ? CloseAsync() : OpenAsync();

    private async void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                _dragY = Math.Max(0, e.TotalY);
                TranslationY = _dragY;
                Opacity = Math.Max(0.3, 1 - _dragY / Math.Max(Height, 1));
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_dragY > Math.Min(80, Height * 0.25))
                {
                    await CloseAsync();
                }
                else
                {
                    await Task.WhenAll(
                        this.TranslateToAsync(0, 0, 120, Easing.CubicOut),
                        this.FadeToAsync(1, 120));
                }
                _dragY = 0;
                break;
        }
    }

    #endregion

    #region Grösse

    protected override void OnParentSet()
    {
        base.OnParentSet();

        _parent?.SizeChanged -= OnParentSizeChanged;

        _parent = Parent as VisualElement;

        _parent?.SizeChanged += OnParentSizeChanged;
    }

    private void OnParentSizeChanged(object sender, EventArgs e) => UpdateSize();

    private void UpdateSize()
    {
        if (_parent == null || _parent.Height <= 0) return;

        MaximumHeightRequest = _parent.Height * MaxHeightRatio;

        // Hintergrund volle Breite, Inhalt auf breiten Bildschirmen begrenzt
        _content.MaximumWidthRequest = MaxContentWidth;
        _content.HorizontalOptions = _parent.Width > MaxContentWidth
            ? LayoutOptions.Center
            : LayoutOptions.Fill;
    }

    #endregion
}
