
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

    private bool _isDragging = false;
    private Border? _activeThumb;
    private double _startTranslationX;

    public CustomRangeSlider()
    {
        InitializeComponent();

        HighlightTrack.AnchorX = 0;

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
        double width = MainContainer.Width - KnobSize;

        if (!IsRange)
            _activeThumb = LowerThumb;
        else
        {
            double distLower = Math.Abs(adjustedTouchX - LowerThumb.TranslationX);
            double distUpper = Math.Abs(adjustedTouchX - UpperThumb.TranslationX);

            if (distLower < distUpper)
                _activeThumb = LowerThumb;
            else if (distUpper < distLower)
                _activeThumb = UpperThumb;
            else
                _activeThumb = adjustedTouchX > LowerThumb.TranslationX ? UpperThumb : LowerThumb;
        }

        double newX = Math.Clamp(adjustedTouchX, 0, width);
        if (IsRange)
        {
            if (_activeThumb == LowerThumb)
                newX = Math.Min(newX, UpperThumb.TranslationX);
            else
                newX = Math.Max(newX, LowerThumb.TranslationX);
        }

        _activeThumb.TranslationX = newX;
        _startTranslationX = newX;
        _isDragging = true;

        UpdateVisualsDuringDrag(LowerThumb.TranslationX, UpperThumb.TranslationX);
    }

    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (_activeThumb == null) return;
        double width = MainContainer.Width - KnobSize;

        switch (e.StatusType)
        {
            case GestureStatus.Running:
                double translationX = Math.Clamp(_startTranslationX + e.TotalX, 0, width);

                if (IsRange)
                {
                    if (_activeThumb == LowerThumb)
                        translationX = Math.Min(translationX, UpperThumb.TranslationX);
                    else
                        translationX = Math.Max(translationX, LowerThumb.TranslationX);
                }

                _activeThumb.TranslationX = translationX;

                double rawVal = Minimum + (translationX / width) * (Maximum - Minimum);
                if (_activeThumb == LowerThumb)
                    LowerLabel.Text = string.Format(ValueDisplayFormat, rawVal);
                else
                    UpperLabel.Text = string.Format(ValueDisplayFormat, rawVal);

                UpdateVisualsDuringDrag(LowerThumb.TranslationX, UpperThumb.TranslationX);
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

    private void UpdateVisualsDuringDrag(double xLower, double xUpper)
    {
        if (IsRange)
        {
            HighlightTrack.TranslationX = xLower + (KnobSize / 2);
            double newWidth = Math.Max(0, xUpper - xLower);
            if (Math.Abs(HighlightTrack.WidthRequest - newWidth) > 0.5)
                HighlightTrack.WidthRequest = newWidth;
        }
        else
        {
            HighlightTrack.TranslationX = 0;
            double newWidth = xLower + (KnobSize / 2);
            if (Math.Abs(HighlightTrack.WidthRequest - newWidth) > 0.5)
                HighlightTrack.WidthRequest = newWidth;
        }

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
        if (LowerLabel == null || MainContainer == null || MainContainer.Width <= 0) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isDragging)
            {
                LowerLabel.Text = string.Format(ValueDisplayFormat, LowerValue);
                UpperLabel.Text = string.Format(ValueDisplayFormat, UpperValue);

                double xLower = GetXFromValue(LowerValue);
                double xUpper = GetXFromValue(UpperValue);

                LowerThumb.TranslationX = xLower;
                UpperThumb.TranslationX = xUpper;
                UpdateVisualsDuringDrag(xLower, xUpper);
            }
        });
    }
}