namespace SnapDoc.Controls;

public partial class CustomRangeSlider : ContentView
{
    #region BindableProperties
    public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(CustomRangeSlider), 0.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(CustomRangeSlider), 100.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty LowerValueProperty = BindableProperty.Create(nameof(LowerValue), typeof(double), typeof(CustomRangeSlider), 0.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty UpperValueProperty = BindableProperty.Create(nameof(UpperValue), typeof(double), typeof(CustomRangeSlider), 100.0, BindingMode.TwoWay, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty IsRangeProperty = BindableProperty.Create(nameof(IsRange), typeof(bool), typeof(CustomRangeSlider), false, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MaximumTrackColorProperty = BindableProperty.Create(nameof(MaximumTrackColor), typeof(Color), typeof(CustomRangeSlider), Colors.Gray, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MinimumTrackColorProperty = BindableProperty.Create(nameof(MinimumTrackColor), typeof(Color), typeof(CustomRangeSlider), Colors.Green, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(CustomRangeSlider), Colors.White, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(CustomRangeSlider), null, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(CustomRangeSlider), null, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(double), typeof(CustomRangeSlider), 1.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty KnobSizeProperty = BindableProperty.Create(nameof(KnobSize), typeof(double), typeof(CustomRangeSlider), 22.0, propertyChanged: (b, o, n) => ((CustomRangeSlider)b).UpdateUI());
    public static readonly BindableProperty MinimumValueDisplayFormatProperty = BindableProperty.Create(nameof(MinimumValueDisplayFormat), typeof(string), typeof(CustomRangeSlider), "{0:0} %");
    public static readonly BindableProperty MaximumValueDisplayFormatProperty = BindableProperty.Create(nameof(MaximumValueDisplayFormat), typeof(string), typeof(CustomRangeSlider), "{0:0} %");

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
    #endregion

    private bool _isDragging = false;
    private double _touchStartPosX;
    private bool _activeThumbIsLower = true;
    private double _currentXLower;
    private double _currentXUpper;
    private readonly SliderDrawable _painter = new();
    private double _lastReportedLowerValue = double.MinValue;
    private double _lastReportedUpperValue = double.MinValue;

    public CustomRangeSlider()
    {
        InitializeComponent();

        _painter.KnobSize = KnobSize;
        _painter.BaseColor = MaximumTrackColor;
        _painter.HighlightColor = MinimumTrackColor;
        _painter.IsRange = IsRange;
        TrackCanvas.Drawable = _painter;
        TrackCanvas.HeightRequest = KnobSize + 10;

        MainContainer.SizeChanged += (s, e) => {
            if (!_isDragging) UpdateUI();
        };
    }

    private void OnPointerPressed(object sender, PointerEventArgs e)
    {
        if (MainContainer.Width <= 0) return;

        var position = e.GetPosition(TouchLayer);
        if (position == null) return;

        double touchX = position.Value.X;
        double adjustedTouchX = touchX - (KnobSize / 2);

        if (!IsRange)
        {
            _activeThumbIsLower = true;
        }
        else
        {
            double distLower = Math.Abs(adjustedTouchX - _currentXLower);
            double distUpper = Math.Abs(adjustedTouchX - _currentXUpper);
            _activeThumbIsLower = distLower < distUpper;
        }

        _touchStartPosX = touchX;
        _isDragging = true;

        UpdateThumbPosition(touchX);
    }

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (!_isDragging) return;

        switch (e.StatusType)
        {
            case GestureStatus.Running:
                double currentAbsoluteX = _touchStartPosX + e.TotalX;
                UpdateThumbPosition(currentAbsoluteX);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;

                FinalizeValue(_currentXLower, true);
                if (IsRange)
                    FinalizeValue(_currentXUpper, false);

                UpdateUI();
                break;
        }
    }

    private void UpdateThumbPosition(double absoluteX)
    {
        if (!_isDragging) return;

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

        if (_activeThumbIsLower)
        {
            if (Math.Abs(steppedVal - _lastReportedLowerValue) > 0.001)
            {
                LowerLabel.Text = string.Format(MinimumValueDisplayFormat, steppedVal);
                _lastReportedLowerValue = steppedVal;
            }
        }
        else
        {
            if (Math.Abs(steppedVal - _lastReportedUpperValue) > 0.001)
            {
                UpperLabel.Text = string.Format(MaximumValueDisplayFormat, steppedVal);
                _lastReportedUpperValue = steppedVal;
            }
        }
        UpdateVisualsDuringDrag(_currentXLower, _currentXUpper);
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
        UpperLabel.IsVisible = IsRange;

        double halfKnob = KnobSize / 2;
        double containerWidth = MainContainer.Width;
        double lWidth = LowerLabel.Width > 0 ? LowerLabel.Width : 45;
        double uWidth = UpperLabel.Width > 0 ? UpperLabel.Width : 45;
        double targetXLower = xLower + halfKnob - (lWidth / 2);
        double targetXUpper = xUpper + halfKnob - (uWidth / 2);

        targetXLower = Math.Clamp(targetXLower, 0, containerWidth - lWidth);
        targetXUpper = Math.Clamp(targetXUpper, 0, containerWidth - uWidth);

        if (IsRange)
        {
            double minGap = 5;
            if (targetXLower + lWidth + minGap > targetXUpper)
            {
                double overlap = (targetXLower + lWidth + minGap) - targetXUpper;
                targetXLower -= overlap / 2;
                targetXUpper += overlap / 2;

                targetXLower = Math.Clamp(targetXLower, 0, containerWidth - lWidth);
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
        if (LowerLabel == null || MainContainer == null || MainContainer.Width <= 0 || _painter == null)
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

            if (!_isDragging)
            {
                LowerLabel.Text = string.Format(MinimumValueDisplayFormat, LowerValue);
                UpperLabel.Text = string.Format(MaximumValueDisplayFormat, UpperValue);

                double xLower = GetXFromValue(LowerValue);
                double xUpper = IsRange ? GetXFromValue(UpperValue) : 0;

                _currentXLower = xLower;
                _currentXUpper = xUpper;

                UpdateVisualsDuringDrag(xLower, xUpper);
            }
            else
                TrackCanvas.Invalidate();
        });
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
        float radius = (float)KnobSize / 2;

        canvas.SetShadow(new SizeF(0, 3), 4, Colors.Black.WithAlpha(0.2f));
        canvas.FillColor = ThumbColor;
        canvas.FillCircle(x, y, radius);
        canvas.SetShadow(SizeF.Zero, 0, Colors.Transparent);
        canvas.StrokeColor = Color.FromArgb("#D1D1D6");
        canvas.StrokeSize = 0.5f;
        canvas.DrawCircle(x, y, radius);
    }
}