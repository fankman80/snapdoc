namespace SnapDoc.Controls;

public partial class RangeSlider : ContentView
{
    #region BindableProperties
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(RangeSlider), 0.0, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(RangeSlider), 100.0, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty LowerValueProperty = BindableProperty.Create(nameof(LowerValue), typeof(double), typeof(RangeSlider), 0.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty UpperValueProperty = BindableProperty.Create(nameof(UpperValue), typeof(double), typeof(RangeSlider), 100.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty IsRangeProperty = BindableProperty.Create(nameof(IsRange), typeof(bool), typeof(RangeSlider), false, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumTrackColorProperty = BindableProperty.Create(nameof(MaximumTrackColor), typeof(Color), typeof(RangeSlider), Colors.Gray, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty MinimumTrackColorProperty = BindableProperty.Create(nameof(MinimumTrackColor), typeof(Color), typeof(RangeSlider), Colors.Green, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(RangeSlider), Colors.White, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(RangeSlider), Label.TextColorProperty.DefaultValue, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(RangeSlider), Label.FontSizeProperty.DefaultValue, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(double), typeof(RangeSlider), 1.0, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty KnobSizeProperty = BindableProperty.Create(nameof(KnobSize), typeof(double), typeof(RangeSlider), 24.0, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty MinimumValueDisplayFormatProperty = BindableProperty.Create(nameof(MinimumValueDisplayFormat), typeof(string), typeof(RangeSlider), "{0:0}");
    public static readonly BindableProperty MaximumValueDisplayFormatProperty = BindableProperty.Create(nameof(MaximumValueDisplayFormat), typeof(string), typeof(RangeSlider), "{0:0}");
    public static readonly BindableProperty ShowLabelsProperty = BindableProperty.Create(nameof(ShowLabels), typeof(bool), typeof(RangeSlider), true, propertyChanged: (b, o, n) => ((RangeSlider)b).UpdateUI());
    public static readonly BindableProperty LabelCalculationProperty = BindableProperty.Create(nameof(LabelCalculation), typeof(string), typeof(RangeSlider), string.Empty);
    public static readonly BindableProperty IsRealtimeProperty = BindableProperty.Create(nameof(IsRealtime), typeof(bool), typeof(RangeSlider), false);

    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string MinimumValueDisplayFormat { get => (string)GetValue(MinimumValueDisplayFormatProperty); set => SetValue(MinimumValueDisplayFormatProperty, value); }
    public string MaximumValueDisplayFormat { get => (string)GetValue(MaximumValueDisplayFormatProperty); set => SetValue(MaximumValueDisplayFormatProperty, value); }
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
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public string LabelCalculation { get => (string)GetValue(LabelCalculationProperty); set => SetValue(LabelCalculationProperty, value); }
    public bool IsRealtime { get => (bool)GetValue(IsRealtimeProperty); set => SetValue(IsRealtimeProperty, value); }
    #endregion

    private bool _isDragging = false;
    private double _touchStartPosX;
    private bool _activeThumbIsLower = true;
    private double _currentXLower;
    private double _currentXUpper;
    private readonly SliderDrawable _painter = new();
    private double _lastReportedLowerValue = double.MinValue;
    private double _lastReportedUpperValue = double.MinValue;
    private double _cachedLowerWidth = 0;
    private double _cachedUpperWidth = 0;
    private string _lastLowerText = "";
    private string _lastUpperText = "";
    private bool _pannedDuringTouch = false;
    private bool _isDirectionLocked;
    private bool _isScrollingTriggered;
    private const double GestureThreshold = 10.0;
    private bool _wasPressedOnThisControl;

    public RangeSlider()
    {
        InitializeComponent();

        _painter.KnobSize = KnobSize;
        _painter.BaseColor = MaximumTrackColor;
        _painter.HighlightColor = MinimumTrackColor;
        _painter.IsRange = IsRange;
        _painter.ThumbColor = ThumbColor;

        TrackCanvas.Drawable = _painter;
        TrackCanvas.HeightRequest = KnobSize + 20;
        TouchLayer.Margin = new Thickness(0, -(KnobSize / 2));

        MainContainer.SizeChanged += (s, e) =>
        {
            if (MainContainer.Width > 0 && !_isDragging)
            {
                _cachedLowerWidth = 0;
                _cachedUpperWidth = 0;
                UpdateUI();
            }
        };
    }

    private void OnPointerPressed(object sender, PointerEventArgs e)
    {
        _wasPressedOnThisControl = true;
        _pannedDuringTouch = false;
        _isDirectionLocked = false;
        _isScrollingTriggered = false;

        if (MainContainer.Width <= 0)
            return;

        var position = e.GetPosition(TouchLayer);
        if (position == null)
            return;

        _touchStartPosX = position.Value.X;

        double adjustedTouchX = _touchStartPosX - (KnobSize / 2);
        if (!IsRange)
            _activeThumbIsLower = true;
        else
        {
            double distLower = Math.Abs(adjustedTouchX - _currentXLower);
            double distUpper = Math.Abs(adjustedTouchX - _currentXUpper);
            _activeThumbIsLower = distLower < distUpper;
        }
    }

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDragging = true;
                break;

            case GestureStatus.Running:
                if (!_isDragging || _isScrollingTriggered)
                    return;

                if (!_isDirectionLocked)
                {
                    if (Math.Abs(e.TotalX) > GestureThreshold || Math.Abs(e.TotalY) > GestureThreshold)
                    {
                        if (Math.Abs(e.TotalY) > Math.Abs(e.TotalX))
                        {
                            _isScrollingTriggered = true;
                            _isDragging = false;
                            return;
                        }
                        else
                        {
                            _isDirectionLocked = true;
                            _pannedDuringTouch = true;
                            ExpandTouchLayer(true);
                        }
                    }
                    else return;
                }

                double currentFingerX = _touchStartPosX + e.TotalX;
                UpdateThumbPosition(currentFingerX);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                EndInteraction();
                break;
        }
    }

    private void OnPointerReleased(object sender, PointerEventArgs e)
    {
        if (_wasPressedOnThisControl && !_pannedDuringTouch && !_isScrollingTriggered)
        {
            var position = e.GetPosition(this);
            if (position.HasValue && position.Value.Y >= 0 && position.Value.Y <= this.Height)
            {
                _isDragging = true;
                UpdateThumbPosition(position.Value.X);
            }
        }
        EndInteraction();
    }

    private void EndInteraction()
    {
        if (!_wasPressedOnThisControl)
            return;

        _isDragging = false;
        _pannedDuringTouch = false;
        _isDirectionLocked = false;
        _isScrollingTriggered = false;
        _wasPressedOnThisControl = false;

        ExpandTouchLayer(false);

        FinalizeValue(_currentXLower, true);
        if (IsRange)
            FinalizeValue(_currentXUpper, false);

        UpdateUI();
    }

    private void UpdateThumbPosition(double absoluteX)
    {
        if (!_isDragging)
            return;

        double width = MainContainer.Width - KnobSize;
        double newX = Math.Clamp(absoluteX - (KnobSize / 2), 0, width);

        if (IsRange)
        {
            if (_activeThumbIsLower)
                newX = Math.Min(newX, _currentXUpper);
            else
                newX = Math.Max(newX, _currentXLower);
        }

        if (_activeThumbIsLower)
            _currentXLower = newX;
        else
            _currentXUpper = newX;

        double rawVal = Minimum + (newX / width) * (Maximum - Minimum);
        double steppedVal = Math.Round(rawVal / Step) * Step;
        int decimals = 0;
        string stepString = Step.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (stepString.Contains('.'))
            decimals = stepString.Split('.')[1].Length;
        steppedVal = Math.Round(steppedVal, decimals);

        if (_activeThumbIsLower)
        {
            if (Math.Abs(steppedVal - _lastReportedLowerValue) > 0.00001)
            {
                double displayVal = ApplyLabelCalculation(steppedVal);
                LowerLabel.Text = string.Format(MinimumValueDisplayFormat, displayVal);
                _lastReportedLowerValue = steppedVal;

                if (IsRealtime)
                    LowerValue = steppedVal;
            }
        }
        else
        {
            if (Math.Abs(steppedVal - _lastReportedUpperValue) > 0.00001)
            {
                double displayVal = ApplyLabelCalculation(steppedVal);
                UpperLabel.Text = string.Format(MaximumValueDisplayFormat, displayVal);
                _lastReportedUpperValue = steppedVal;

                if (IsRealtime)
                    UpperValue = steppedVal;
            }
        }
        UpdateVisualsDuringDrag(_currentXLower, _currentXUpper);
    }

    private void ExpandTouchLayer(bool expand)
    {
        if (TouchLayer == null)
            return;

        if (expand)
        {
            TouchLayer.Margin = new Thickness(-2000);
            TouchLayer.ZIndex = 9999;
#if ANDROID
            var androidView = this.Handler?.PlatformView as Android.Views.View;
            androidView?.Parent?.RequestDisallowInterceptTouchEvent(true);
#endif
        }
        else
        {
            TouchLayer.Margin = new Thickness(0, -(KnobSize / 2));
            TouchLayer.ZIndex = 100;
#if ANDROID
            var androidView = this.Handler?.PlatformView as Android.Views.View;
            androidView?.Parent?.RequestDisallowInterceptTouchEvent(false);
#endif
        }
    }

    private void UpdateVisualsDuringDrag(double xLower, double xUpper)
    {
        _painter.XLower = xLower;
        _painter.XUpper = xUpper;
        _painter.IsRange = IsRange;
        TrackCanvas.Invalidate();
        AdjustLabelPositions(xLower, xUpper);
    }

    private void AdjustLabelPositions(double xLower, double xUpper)
    {
        if (ShowLabels)
            UpperLabel.IsVisible = IsRange;

        double halfKnob = KnobSize / 2;
        double containerWidth = MainContainer.Width;

        if (LowerLabel.Text != _lastLowerText || _cachedLowerWidth <= 0)
        {
            _lastLowerText = LowerLabel.Text;
            var measure = LowerLabel.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
            _cachedLowerWidth = Math.Max(_cachedLowerWidth, measure);
        }

        if (IsRange && (UpperLabel.Text != _lastUpperText || _cachedUpperWidth <= 0))
        {
            _lastUpperText = UpperLabel.Text;
            var measure = UpperLabel.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
            _cachedUpperWidth = Math.Max(_cachedUpperWidth, measure);
        }

        double lWidth = _cachedLowerWidth > 0 ? _cachedLowerWidth : 60;
        double uWidth = _cachedUpperWidth > 0 ? _cachedUpperWidth : 60;
        double targetXLower = Math.Clamp(xLower + halfKnob - (lWidth / 2), 0, containerWidth - lWidth);
        double targetXUpper = IsRange ? Math.Clamp(xUpper + halfKnob - (uWidth / 2), 0, containerWidth - uWidth) : 0;

        if (IsRange)
        {
            double minGap = 8;
            if (targetXLower + lWidth + minGap > targetXUpper)
            {
                double overlap = (targetXLower + lWidth + minGap) - targetXUpper;
                if (targetXLower <= 0)
                    targetXUpper = lWidth + minGap;
                else if (targetXUpper >= containerWidth - uWidth)
                    targetXLower = containerWidth - uWidth - minGap - lWidth;
                else
                {
                    targetXLower -= overlap / 2;
                    targetXUpper += overlap / 2;
                }

                targetXLower = Math.Clamp(targetXLower, 0, containerWidth - lWidth);
                targetXUpper = Math.Max(targetXUpper, targetXLower + lWidth + minGap);
                targetXUpper = Math.Clamp(targetXUpper, 0, containerWidth - uWidth);
            }
            UpperLabel.TranslationX = targetXUpper;
        }
        LowerLabel.TranslationX = targetXLower;
    }

    private void FinalizeValue(double x, bool isLower)
    {
        double width = MainContainer.Width - KnobSize;
        if (width <= 0)
            return;

        double rawValue = Minimum + (x / width) * (Maximum - Minimum);
        double steppedValue = Math.Round(rawValue / Step) * Step;

        int decimals = 0;
        string stepString = Step.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (stepString.Contains('.'))
            decimals = stepString.Split('.')[1].Length;

        steppedValue = Math.Round(steppedValue, decimals);
        steppedValue = Math.Clamp(steppedValue, Minimum, Maximum);

        if (isLower)
            LowerValue = steppedValue;
        else
            UpperValue = steppedValue;

        _lastReportedLowerValue = double.MinValue;
        _lastReportedUpperValue = double.MinValue;
    }

    private double GetXFromValue(double val)
    {
        double width = MainContainer.Width - KnobSize;
        if (width <= 0 || Maximum <= Minimum)
            return 0;
        return (Math.Clamp(val, Minimum, Maximum) - Minimum) / (Maximum - Minimum) * width;
    }

    public void UpdateUI()
    {
        if (MainContainer == null || MainContainer.Width <= 0 || _painter == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _painter.BaseColor = MaximumTrackColor;
            _painter.HighlightColor = MinimumTrackColor;
            _painter.ThumbColor = ThumbColor;
            _painter.KnobSize = KnobSize;
            _painter.IsRange = IsRange;

            if (TextColor != null)
            {
                LowerLabel.TextColor = TextColor;
                UpperLabel.TextColor = TextColor;
            }
            LowerLabel.FontSize = FontSize;
            UpperLabel.FontSize = FontSize;
            _cachedLowerWidth = 0;
            _cachedUpperWidth = 0;

            if (!_isDragging)
            {
                LowerLabel.Text = string.Format(MinimumValueDisplayFormat, ApplyLabelCalculation(LowerValue));
                UpperLabel.Text = string.Format(MaximumValueDisplayFormat, ApplyLabelCalculation(UpperValue));
                _currentXLower = GetXFromValue(LowerValue);
                _currentXUpper = IsRange ? GetXFromValue(UpperValue) : 0;
            }
            UpdateVisualsDuringDrag(_currentXLower, _currentXUpper);
        });
    }
    private double ApplyLabelCalculation(double value)
    {
        if (string.IsNullOrWhiteSpace(LabelCalculation))
            return value;

        string calc = LabelCalculation.Trim();
        char op = calc[0];

        if (double.TryParse(calc[1..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double factor))
        {
            return op switch
            {
                '*' => value * factor,
                '/' => factor != 0 ? value / factor : value,
                '+' => value + factor,
                '-' => value - factor,
                _ => value
            };
        }
        return value;
    }
}

public class SliderDrawable : IDrawable
{
    public double XLower { get; set; }
    public double XUpper { get; set; }
    public double KnobSize { get; set; }
    public Color? BaseColor { get; set; }
    public Color? HighlightColor { get; set; }
    public Color? ThumbColor { get; set; }
    public bool IsRange { get; set; }
    private const float visualPadding = 2f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.Antialias = true;
        float trackHeight = 4;
        float y = dirtyRect.Height / 2;
        float halfKnob = (float)KnobSize / 2;

        canvas.StrokeColor = BaseColor;
        canvas.StrokeSize = trackHeight;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.DrawLine(halfKnob, y, dirtyRect.Width - halfKnob, y);
        
        canvas.StrokeColor = HighlightColor;

        if (IsRange)
            canvas.DrawLine((float)XLower + halfKnob, y, (float)XUpper + halfKnob, y);
        else
            canvas.DrawLine(halfKnob, y, (float)XLower + halfKnob, y);

        DrawKnob(canvas, (float)XLower + halfKnob, y);

        if (IsRange)
            DrawKnob(canvas, (float)XUpper + halfKnob, y);
    }

    private void DrawKnob(ICanvas canvas, float x, float y)
    {
        float logicalRadius = (float)KnobSize / 2;
        float visualRadius = logicalRadius - visualPadding;
        canvas.SetShadow(new SizeF(0, 2), 3, Colors.Black.WithAlpha(0.25f));
        canvas.FillColor = ThumbColor;
        canvas.FillCircle(x, y, visualRadius);
        canvas.SetShadow(SizeF.Zero, 0, Colors.Transparent);
        canvas.StrokeColor = Color.FromArgb("#D1D1D6");
        canvas.StrokeSize = 0.5f;
        canvas.DrawCircle(x, y, visualRadius);
    }
}
