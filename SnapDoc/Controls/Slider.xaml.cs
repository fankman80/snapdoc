namespace SnapDoc.Controls;

public partial class CustomRangeSlider : ContentView
{
    #region BindableProperties
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
    public static readonly BindableProperty MinimumValueDisplayFormatProperty = BindableProperty.Create(nameof(MinimumValueDisplayFormat), typeof(string), typeof(CustomRangeSlider), "{0:0} %");
    public static readonly BindableProperty MaximumValueDisplayFormatProperty = BindableProperty.Create(nameof(MaximumValueDisplayFormat), typeof(string), typeof(CustomRangeSlider), "{0:0} %");
    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(double), typeof(CustomRangeSlider), 1.0);

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
    private Border? _activeThumb;
    private double _touchStartPosX;
    private double _thumbStartPosX;
    private readonly SliderDrawable _painter = new();

    // Throttling Variablen für butterweiche Text-Updates
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
        double adjustedX = touchX - (KnobSize / 2);

        if (!IsRange)
            _activeThumb = LowerThumb;
        else
        {
            double distLower = Math.Abs(adjustedX - LowerThumb.TranslationX);
            double distUpper = Math.Abs(adjustedX - UpperThumb.TranslationX);
            _activeThumb = distLower < distUpper ? LowerThumb : UpperThumb;
        }

        _touchStartPosX = touchX;
        _thumbStartPosX = _activeThumb.TranslationX;
        _isDragging = true;

        UpdateThumbPosition(touchX);
    }

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (!_isDragging || _activeThumb == null) return;

        switch (e.StatusType)
        {
            case GestureStatus.Running:
                double currentAbsoluteX = _touchStartPosX + e.TotalX;
                UpdateThumbPosition(currentAbsoluteX);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;
                FinalizeValue(LowerThumb.TranslationX, true);
                if (IsRange) FinalizeValue(UpperThumb.TranslationX, false);
                _activeThumb = null;
                UpdateUI();
                break;
        }
    }

    private void UpdateThumbPosition(double absoluteX)
    {
        if (_activeThumb == null) return;

        double width = MainContainer.Width - KnobSize;
        double newX = Math.Clamp(absoluteX - (KnobSize / 2), 0, width);

        if (IsRange)
        {
            if (_activeThumb == LowerThumb)
                newX = Math.Min(newX, UpperThumb.TranslationX);
            else
                newX = Math.Max(newX, LowerThumb.TranslationX);
        }

        _activeThumb.TranslationX = newX;

        double rawVal = Minimum + (newX / width) * (Maximum - Minimum);
        double steppedVal = Math.Round(rawVal / Step) * Step;

        if (_activeThumb == LowerThumb)
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

        UpdateVisualsDuringDrag(LowerThumb.TranslationX, UpperThumb.TranslationX);
    }

    private void UpdateVisualsDuringDrag(double xLower, double xUpper)
    {
        // Werte an den Painter übergeben
        _painter.XLower = xLower;
        _painter.XUpper = xUpper;
        _painter.IsRange = IsRange; // Falls es sich dynamisch ändert

        // Den Befehl zum Neuzeichnen geben (passiert fast instantan auf der GPU)
        TrackCanvas.Invalidate();

        AdjustLabelPositions(xLower, xUpper);
    }

    private void AdjustLabelPositions(double xLower, double xUpper)
    {
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
        }

        LowerLabel.TranslationX = targetXLower;
        UpperLabel.TranslationX = targetXUpper;
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
        if (LowerLabel == null || MainContainer == null || MainContainer.Width <= 0)
            return;

        _painter.BaseColor = MaximumTrackColor;
        _painter.HighlightColor = MinimumTrackColor;
        _painter.KnobSize = KnobSize;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isDragging)
            {
                LowerLabel.Text = string.Format(MinimumValueDisplayFormat, LowerValue);
                UpperLabel.Text = string.Format(MaximumValueDisplayFormat, UpperValue);

                double xLower = GetXFromValue(LowerValue);
                double xUpper = GetXFromValue(UpperValue);

                LowerThumb.TranslationX = xLower;
                UpperThumb.TranslationX = xUpper;
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
    public bool IsRange { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
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
    }
}