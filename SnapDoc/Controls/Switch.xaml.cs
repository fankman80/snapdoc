using Microsoft.Maui.Controls.Shapes;

namespace SnapDoc.Controls;

public partial class Switch : ContentView
{
    public Switch()
    {
        InitializeComponent();
        UpdateUI();
    }

    // ===== Bindable Properties =====

    public static readonly BindableProperty LabelTextProperty =
        BindableProperty.Create(nameof(LabelText), typeof(string), typeof(Switch), string.Empty, propertyChanged: OnAnyPropertyChanged);

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(Switch), false, BindingMode.TwoWay, propertyChanged: OnAnyPropertyChanged);

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public static readonly BindableProperty SwitchWidthProperty =
        BindableProperty.Create(nameof(SwitchWidth), typeof(double), typeof(Switch), 44.0, propertyChanged: OnAnyPropertyChanged);

    public double SwitchWidth
    {
        get => (double)GetValue(SwitchWidthProperty);
        set => SetValue(SwitchWidthProperty, value);
    }

    public static readonly BindableProperty SwitchHeightProperty =
        BindableProperty.Create(nameof(SwitchHeight), typeof(double), typeof(Switch), 24.0, propertyChanged: OnAnyPropertyChanged);

    public double SwitchHeight
    {
        get => (double)GetValue(SwitchHeightProperty);
        set => SetValue(SwitchHeightProperty, value);
    }

    public static readonly BindableProperty KnobMarginProperty =
        BindableProperty.Create(nameof(KnobMargin), typeof(double), typeof(Switch), 3.0, propertyChanged: OnAnyPropertyChanged);

    public double KnobMargin
    {
        get => (double)GetValue(KnobMarginProperty);
        set => SetValue(KnobMarginProperty, value);
    }

    public static readonly BindableProperty BorderColorOnProperty =
        BindableProperty.Create(nameof(BorderColorOn), typeof(Color), typeof(Switch), Colors.Transparent, propertyChanged: OnAnyPropertyChanged);

    public Color BorderColorOn
    {
        get => (Color)GetValue(BorderColorOnProperty);
        set => SetValue(BorderColorOnProperty, value);
    }

    public static readonly BindableProperty BorderColorOffProperty =
    BindableProperty.Create(nameof(BorderColorOff), typeof(Color), typeof(Switch), Colors.Black, propertyChanged: OnAnyPropertyChanged);

    public Color BorderColorOff
    {
        get => (Color)GetValue(BorderColorOffProperty);
        set => SetValue(BorderColorOffProperty, value);
    }

    public static readonly BindableProperty KnobColorOnProperty =
        BindableProperty.Create(nameof(KnobColorOn), typeof(Color), typeof(Switch), Colors.White, propertyChanged: OnAnyPropertyChanged);

    public Color KnobColorOn
    {
        get => (Color)GetValue(KnobColorOnProperty);
        set => SetValue(KnobColorOnProperty, value);
    }

    public static readonly BindableProperty KnobColorOffProperty =
        BindableProperty.Create(nameof(KnobColorOff), typeof(Color), typeof(Switch), Colors.Gray, propertyChanged: OnAnyPropertyChanged);

    public Color KnobColorOff
    {
        get => (Color)GetValue(KnobColorOffProperty);
        set => SetValue(KnobColorOffProperty, value);
    }

    public static readonly BindableProperty SwitchColorOnProperty =
        BindableProperty.Create(nameof(SwitchColorOn), typeof(Color), typeof(Switch), Colors.LimeGreen, propertyChanged: OnAnyPropertyChanged);

    public Color SwitchColorOn
    {
        get => (Color)GetValue(SwitchColorOnProperty);
        set => SetValue(SwitchColorOnProperty, value);
    }

    public static readonly BindableProperty SwitchColorOffProperty =
        BindableProperty.Create(nameof(SwitchColorOff), typeof(Color), typeof(Switch), Colors.DarkGray, propertyChanged: OnAnyPropertyChanged);

    public Color SwitchColorOff
    {
        get => (Color)GetValue(SwitchColorOffProperty);
        set => SetValue(SwitchColorOffProperty, value);
    }

    // ===== Dynamisch berechnete Werte =====

    public float KnobSize => (float)(SwitchHeight - (2 * KnobMargin));
    public float CalculatedCornerRadius => (float)(SwitchHeight / 2.0);
    public float CalculatedKnobRadius => KnobSize / 2;
    public Color CurrentKnobColor => IsToggled ? KnobColorOn : KnobColorOff;
    public Color CurrentSwitchColor => IsToggled ? SwitchColorOn : SwitchColorOff;

    public double SwitchWidthMinus2 => SwitchWidth - 2;
    public double SwitchHeightMinus2 => SwitchHeight - 2;

    // ===== Event-Handler =====

    private static void OnAnyPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Switch customSwitch)
        {
            customSwitch.UpdateUI();
        }
    }

    private void OnTapped(object sender, TappedEventArgs e)
    {
        IsToggled = !IsToggled;
        // UpdateUI wird sowieso über PropertyChanged aufgerufen
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        UpdateUI();
    }

    // ===== UI-Update =====

    private void UpdateUI()
    {
        if (OuterContainer == null || Knob == null)
            return;

        OuterContainer.WidthRequest = SwitchWidth;
        OuterContainer.HeightRequest = SwitchHeight;
        OuterContainer.StrokeThickness = 0;
        OuterContainer.BackgroundColor = CurrentSwitchColor;
        OuterContainer.StrokeShape = new RoundRectangle { CornerRadius = CalculatedCornerRadius };

        Knob.WidthRequest = KnobSize;
        Knob.HeightRequest = KnobSize;
        Knob.BackgroundColor = CurrentKnobColor;
        Knob.Stroke = Colors.Transparent;
        Knob.StrokeThickness = 0;
        Knob.StrokeShape = new RoundRectangle { CornerRadius = CalculatedKnobRadius };
        Knob.Margin = new Thickness(KnobMargin);

        // Position des Knobs animiert verschieben
        double maxTranslate = SwitchWidth - KnobSize - (2 * KnobMargin);
        Knob.TranslateTo(IsToggled ? maxTranslate : 0, 0, 100, Easing.SinInOut);

        CustomPath.Stroke = IsToggled ? BorderColorOn : BorderColorOff;

        UpdatePath();
    }

    private void UpdatePath()
    {
        double width = SwitchWidth - 1;
        double height = SwitchHeight - 1;
        double radius = height / 2;

        // Sicherheitshalber Radius begrenzen
        radius = Math.Min(radius, width / 2);

        var segments = new PathSegmentCollection

    {
        // Obere Linie
        new LineSegment { Point = new Point(width - radius, 0.5) },

        // Obere rechte Ecke (Bogen)
        new ArcSegment
        {
            Point = new Point(width + 0.5, radius),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
            IsLargeArc = false
        },

        // Rechte Seite
        new LineSegment { Point = new Point(width + 0.5, height - radius) },

        // Untere rechte Ecke (Bogen)
        new ArcSegment
        {
            Point = new Point(width - radius, height + 0.5),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
            IsLargeArc = false
        },

        // Untere Linie
        new LineSegment { Point = new Point(radius, height + 0.5) },

        // Untere linke Ecke (Bogen)
        new ArcSegment
        {
            Point = new Point(0.5, height - radius),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
            IsLargeArc = false
        },

        // Linke Seite
        new LineSegment { Point = new Point(0.5, radius) },

        // Obere linke Ecke (Bogen)
        new ArcSegment
        {
            Point = new Point(radius, 0.5),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0,
            IsLargeArc = false
        }
    };

        var figure = new PathFigure
        {
            StartPoint = new Point(radius, 0.5),
            Segments = segments,
            IsClosed = true
        };

        var geometry = new PathGeometry
        {
            Figures = [figure]
        };

        CustomPath.Data = geometry;
    }
}
