using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Diagnostics;

namespace SnapDoc.Controls
{
    public partial class VerticalZoomSlider : GraphicsView
    {
        private readonly VerticalZoomSliderDrawable _drawable;
        private double _panStartValue;

        #region Bindable Properties

        // Basis-Werte
        public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(double), typeof(VerticalZoomSlider), 1.0, BindingMode.TwoWay, propertyChanged: OnValueChanged);
        public static readonly BindableProperty MinimumProperty = BindableProperty.Create(nameof(Minimum), typeof(double), typeof(VerticalZoomSlider), 1.0, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty MaximumProperty = BindableProperty.Create(nameof(Maximum), typeof(double), typeof(VerticalZoomSlider), 10.0, propertyChanged: OnStyleChanged);

        // Farben
        public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(VerticalZoomSlider), Colors.White, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty ActiveTrackColorProperty = BindableProperty.Create(nameof(ActiveTrackColor), typeof(Color), typeof(VerticalZoomSlider), Colors.Yellow, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty ThumbColorProperty = BindableProperty.Create(nameof(ThumbColor), typeof(Color), typeof(VerticalZoomSlider), Colors.Yellow, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(VerticalZoomSlider), Colors.Yellow, propertyChanged: OnStyleChanged);

        // Größen & Font
        public static readonly BindableProperty TrackWidthProperty = BindableProperty.Create(nameof(TrackWidth), typeof(float), typeof(VerticalZoomSlider), 4f, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty ThumbRadiusProperty = BindableProperty.Create(nameof(ThumbRadius), typeof(float), typeof(VerticalZoomSlider), 10f, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(float), typeof(VerticalZoomSlider), 14f, propertyChanged: OnStyleChanged);
        public static readonly BindableProperty IsFontBoldProperty = BindableProperty.Create(nameof(IsFontBold), typeof(bool), typeof(VerticalZoomSlider), true, propertyChanged: OnStyleChanged);

        #endregion

        #region Properties Wrapper

        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }
        public Color ActiveTrackColor { get => (Color)GetValue(ActiveTrackColorProperty); set => SetValue(ActiveTrackColorProperty, value); }
        public Color ThumbColor { get => (Color)GetValue(ThumbColorProperty); set => SetValue(ThumbColorProperty, value); }
        public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
        public float TrackWidth { get => (float)GetValue(TrackWidthProperty); set => SetValue(TrackWidthProperty, value); }
        public float ThumbRadius { get => (float)GetValue(ThumbRadiusProperty); set => SetValue(ThumbRadiusProperty, value); }
        public float FontSize { get => (float)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public bool IsFontBold { get => (bool)GetValue(IsFontBoldProperty); set => SetValue(IsFontBoldProperty, value); }

        #endregion

        public event EventHandler<ValueChangedEventArgs>? ValueChanged;

        public VerticalZoomSlider()
        {
            _drawable = new VerticalZoomSliderDrawable();
            this.Drawable = _drawable;

            // Gesten registrieren
            var panGesture = new PanGestureRecognizer();
            panGesture.PanUpdated += OnPanUpdated;
            this.GestureRecognizers.Add(panGesture);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnTapped;
            this.GestureRecognizers.Add(tapGesture);

            this.HeightRequest = 300;
            this.WidthRequest = 100;

            UpdateDrawable();
        }

        private static void OnValueChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is VerticalZoomSlider slider)
            {
                slider._drawable.CurrentValue = (double)newValue;
                slider.Invalidate();
                slider.ValueChanged?.Invoke(slider, new ValueChangedEventArgs((double)oldValue, (double)newValue));
            }
        }

        private static void OnStyleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is VerticalZoomSlider slider)
            {
                slider.UpdateDrawable();
                slider.Invalidate();
            }
        }

        private void UpdateDrawable()
        {
            if (_drawable == null)
                return;
            _drawable.Minimum = this.Minimum;
            _drawable.Maximum = this.Maximum;
            _drawable.CurrentValue = this.Value;
            _drawable.TrackColor = this.TrackColor;
            _drawable.ActiveTrackColor = this.ActiveTrackColor;
            _drawable.ThumbColor = this.ThumbColor;
            _drawable.TextColor = this.TextColor;
            _drawable.TrackWidth = this.TrackWidth;
            _drawable.ThumbRadius = this.ThumbRadius;
            _drawable.FontSize = this.FontSize;
            _drawable.FontStyle = this.IsFontBold ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
        }

        private void OnTapped(object? sender, TappedEventArgs e)
        {
            Point? touchPoint = e.GetPosition(this);
            if (touchPoint.HasValue) UpdateValueFromY(touchPoint.Value.Y);
        }

        private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (e.StatusType == GestureStatus.Started)
            {
                _panStartValue = Value;
            }
            else if (e.StatusType == GestureStatus.Running)
            {
                float padding = ThumbRadius + 5;
                float trackHeight = (float)this.Height - (2 * padding);
                if (trackHeight <= 0) return;

                double percentageMoved = e.TotalY / trackHeight;
                double valueRange = Maximum - Minimum;
                double valueChange = percentageMoved * valueRange;
                double targetValue = _panStartValue - valueChange;
                Value = Math.Clamp(Math.Round(targetValue, 1), Minimum, Maximum);
            }
        }

        private void UpdateValueFromY(double relativeY)
        {
            float padding = ThumbRadius + 5;
            float trackTop = padding;
            float trackBottom = (float)this.Height - padding;
            float trackHeight = trackBottom - trackTop;
            double clampedY = Math.Clamp(relativeY, trackTop, trackBottom);
            float sliderPercent = 1 - (float)((clampedY - trackTop) / trackHeight);
            double newValue = Minimum + (sliderPercent * (Maximum - Minimum));
            Value = Math.Round(newValue, 1);
        }
    }
}