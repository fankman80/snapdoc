#nullable disable
namespace SnapDoc.Controls;

[ContentProperty(nameof(SheetContent))]
public partial class BottomSheet : ContentView
{
    private readonly VerticalStackLayout _header;
    private readonly Grid _sheet;
    private readonly BoxView _divider;
    private readonly ScrollView _scroll;
    private VisualElement _parent;
    private double _dragY;
    private const double SlideDistance = 48;
    private readonly Grid _content;

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), typeof(View), typeof(BottomSheet), propertyChanged: (b, o, n) =>
        {
            var s = (BottomSheet)b;
            if (o is View oldView) oldView.MeasureInvalidated -= s.OnContentMeasureInvalidated;
            s._scroll.Content = (View)n;
            if (n is View newView) newView.MeasureInvalidated += s.OnContentMeasureInvalidated;
            s.UpdateSize();
        });

    private void OnContentMeasureInvalidated(object sender, EventArgs e)
    {
        if (IsOpen)
            Dispatcher.Dispatch(UpdateSize);
    }

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

    public Color SheetColor
    {
        get => (Color)GetValue(SheetColorProperty);
        set => SetValue(SheetColorProperty, value);
    }

    public Color DividerColor
    {
        get => (Color)GetValue(DividerColorProperty);
        set => SetValue(DividerColorProperty, value);
    }

    public double MaxHeightRatio { get; set; } = 0.6;
    public double MaxContentWidth { get; set; } = 640;
    public bool IsOpen { get; private set; }
    public event EventHandler Closed;

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
            Margin = new Thickness(0, 8, 0, 4)
        };

        _header = new VerticalStackLayout
        {
            BackgroundColor = Colors.Transparent, // Ziehen im ganzen Kopf
            Padding = new Thickness(20, 0, 20, 0),
            Children = { handle }
        };

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _header.GestureRecognizers.Add(pan);

        _scroll = new ScrollView { Padding = new Thickness(12, 8, 20, 12) };

        // Inhalt auf Tablet/Windows mittig begrenzen, Hintergrund bleibt volle Breite
        var content = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        content.Add(_header, 0, 0);
        content.Add(_scroll, 0, 1);
        _content = content;

        _sheet = new Grid
        {
            BackgroundColor = SheetColor,
            Children = { content, _divider }
        };

        Content = _sheet;
    }

    public async Task OpenAsync()
    {
        if (IsOpen) return;
        IsOpen = true;

        UpdateSize();
        TranslationY = SlideDistance;
        Opacity = 0;
        IsVisible = true;

        await Task.WhenAll(this.TranslateToAsync(0, 0, 200, Easing.CubicOut), this.FadeToAsync(1, 150, Easing.CubicOut));
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
        if (_parent == null || _content == null || _header == null || _scroll == null) return;
        if (_parent.Height <= 0 || _parent.Width <= 0) return;

        double maxSheet = _parent.Height * MaxHeightRatio;
        MaximumHeightRequest = maxSheet;

        // Hintergrund volle Breite, Inhalt auf breiten Bildschirmen begrenzt
        _content.MaximumWidthRequest = MaxContentWidth;
        _content.HorizontalOptions = _parent.Width > MaxContentWidth
            ? LayoutOptions.Center
            : LayoutOptions.Fill;

        if (SheetContent == null) return;

        // Höhe selbst bestimmen – ScrollView meldet unter Android sonst die volle Höhe
        double width = Math.Min(_parent.Width, MaxContentWidth);
        double innerWidth = width - _scroll.Padding.HorizontalThickness;

        double headerH = _header.Measure(width, double.PositiveInfinity).Height;
        double contentH = SheetContent.Measure(innerWidth, double.PositiveInfinity).Height
                          + _scroll.Padding.VerticalThickness;

        _scroll.HeightRequest = Math.Max(0, Math.Min(contentH, maxSheet - headerH));
    }
}
