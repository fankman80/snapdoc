
namespace SnapDoc.Controls;

public partial class CustomRangeSlider : ContentView
{
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(CustomRangeSlider), 0.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(CustomRangeSlider), 100.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty LowerValueProperty = BindableProperty.Create(nameof(LowerValue), typeof(double), typeof(CustomRangeSlider), 0.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty UpperValueProperty = BindableProperty.Create(nameof(UpperValue), typeof(double), typeof(CustomRangeSlider), 100.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty IsRangeProperty = BindableProperty.Create(nameof(IsRange), typeof(bool), typeof(CustomRangeSlider), false, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumTrackColorProperty = BindableProperty.Create(nameof(MaximumTrackColor), typeof(Color), typeof(CustomRangeSlider), Colors.Gray);
    public static readonly BindableProperty MinimumTrackColorProperty = BindableProperty.Create(nameof(MinimumTrackColor), typeof(Color), typeof(CustomRangeSlider), Colors.Green);
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(CustomRangeSlider), Colors.White);
    public static readonly BindableProperty KnobSizeProperty = BindableProperty.Create(nameof(KnobSize), typeof(double), typeof(CustomRangeSlider), 22.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(CustomRangeSlider), null);
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(CustomRangeSlider), Label.FontSizeProperty.DefaultValue);
    public static readonly BindableProperty ValueDisplayFormatProperty = BindableProperty.Create(nameof(ValueDisplayFormat), typeof(string), typeof(CustomRangeSlider), "{0:0} %");
    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(double), typeof(CustomRangeSlider), 1.0);

    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string ValueDisplayFormat { get => (string)GetValue(ValueDisplayFormatProperty); set => SetValue(ValueDisplayFormatProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double LowerValue { get => (double)GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public double UpperValue { get => (double)GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public bool IsRange { get => (bool)GetValue(IsRangeProperty); set => SetValue(IsRangeProperty, value); }
    public Color MaximumTrackColor { get => (Color)GetValue(MaximumTrackColorProperty); set => SetValue(MaximumTrackColorProperty, value); }
    public Color MinimumTrackColor { get => (Color)GetValue(MinimumTrackColorProperty); set => SetValue(MinimumTrackColorProperty, value); }
    public Color ThumbColor { get => (Color)GetValue(ThumbColorProperty); set => SetValue(ThumbColorProperty, value); }
    public double KnobSize { get => (double)GetValue(KnobSizeProperty); set => SetValue(KnobSizeProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    private double _startLower, _startUpper;
    private bool _isDragging = false;

    public CustomRangeSlider()
    {
        InitializeComponent();
        this.Loaded += (s, e) => UpdateUI();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateUI();
    }

    private void OnPanUpdatedLower(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDragging = true;
                _startLower = GetXFromValue(LowerValue);
                break;

            case GestureStatus.Running:
                double newX = Math.Clamp(_startLower + e.TotalX, 0, MainContainer.Width - KnobSize);
                LowerThumb.TranslationX = newX;
                UpdateValue(newX, true);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;
                UpdateUI();
                break;
        }
    }

    private void OnPanUpdatedUpper(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDragging = true;
                _startUpper = GetXFromValue(UpperValue);
                break;

            case GestureStatus.Running:
                double newX = Math.Clamp(_startUpper + e.TotalX, 0, MainContainer.Width - KnobSize);
                UpperThumb.TranslationX = newX;
                UpdateValue(newX, false);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;
                UpdateUI();
                break;
        }
    }

    private void UpdateValue(double x, bool isLower)
    {
        double width = MainContainer.Width - KnobSize;
        if (width <= 0) return;

        x = Math.Clamp(x, 0, width);

        if (isLower) LowerThumb.TranslationX = x;
        else UpperThumb.TranslationX = x;

        double rawValue = Minimum + (x / width) * (Maximum - Minimum);
        double steppedValue = Math.Round(rawValue / Step) * Step;

        if (isLower) LowerValue = Math.Clamp(steppedValue, Minimum, UpperValue - (IsRange ? Step : 0));
        else UpperValue = Math.Clamp(steppedValue, LowerValue + Step, Maximum);
    }

    private double GetXFromValue(double val)
    {
        double width = MainContainer.Width - KnobSize;
        if (width <= 0 || Maximum <= Minimum) return 0;

        return (Math.Clamp(val, Minimum, Maximum) - Minimum) / (Maximum - Minimum) * width;
    }

    public void UpdateUI()
    {
        if (LowerLabel == null || UpperLabel == null || MainContainer == null) return;
        if (MainContainer.Width <= 0) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LowerLabel.Text = string.Format(ValueDisplayFormat, LowerValue);
            UpperLabel.Text = string.Format(ValueDisplayFormat, UpperValue);

            Size lowerSize = LowerLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);
            Size upperSize = UpperLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);

            double lWidth = lowerSize.Width;
            double uWidth = upperSize.Width;
            double xLower = GetXFromValue(LowerValue);
            double xUpper = GetXFromValue(UpperValue);
            double halfKnob = KnobSize / 2;
            double containerWidth = MainContainer.Width;
            double targetXLower = xLower + halfKnob - (lWidth / 2);
            double targetXUpper = xUpper + halfKnob - (uWidth / 2);

            targetXLower = Math.Clamp(targetXLower, 0, containerWidth - lWidth);
            targetXUpper = Math.Clamp(targetXUpper, 0, containerWidth - uWidth);

            if (IsRange)
            {
                double minGap = 10;
                if (targetXLower + lWidth + minGap > targetXUpper)
                {
                    double overlap = (targetXLower + lWidth + minGap) - targetXUpper;
                    targetXLower -= overlap / 2;
                    targetXUpper += overlap / 2;
                    targetXLower = Math.Clamp(targetXLower, 0, containerWidth - lWidth);
                    targetXUpper = Math.Clamp(targetXUpper, 0, containerWidth - uWidth);
                }
            }

            LowerLabel.TranslationX = targetXLower;
            UpperLabel.TranslationX = targetXUpper;

            if (!_isDragging)
            {
                LowerThumb.TranslationX = xLower;
                UpperThumb.TranslationX = xUpper;
            }

            if (IsRange)
            {
                HighlightTrack.TranslationX = xLower + halfKnob;
                HighlightTrack.WidthRequest = Math.Max(0, xUpper - xLower);
            }
            else
            {
                HighlightTrack.TranslationX = 0;
                HighlightTrack.WidthRequest = xLower + halfKnob;
            }
        });
    }
}