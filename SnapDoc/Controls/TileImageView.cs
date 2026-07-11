#nullable disable
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SnapDoc.Controls;

public partial class TileImageView : ContentView
{
    private readonly SKGLView _canvasView;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Grid _layoutGrid;
    private float _scale = 1.0f;
    private float _panX = 0f;
    private float _panY = 0f;
    private bool _isGenerating = false;
    private float _rotationDegrees = 0f;
    private string _computedTileFolder = string.Empty;
    private readonly LruCache<string, SKBitmap> _tileCache = new(SettingsService.Instance.MaxTileCache);
    private readonly Dictionary<long, SKPoint> _activeTouches = [];
    private float _oldFingerDistance = 0f;
    private float _oldFingerAngle = 0f;
    private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private SKPoint _touchStartPoint;
    private const float ClickThreshold = 15f;
    private MapPin _draggedPin = null;
    private string _pendingPinId = null;
    private double? _pendingZoomFactor = null;
    private bool _pendingImageFit = false;
    private SKPoint _dragOffset = SKPoint.Empty;
    private float _originalPinX;
    private float _originalPinY;
    private CancellationTokenSource _cts;
    private CancellationTokenSource _longPressCts;
    private CancellationTokenSource _tapCts;
    private readonly HashSet<string> _loadingTiles = [];
    private DateTime _lastTapTime = DateTime.MinValue;
    private SKPoint _lastTapLocation = SKPoint.Empty;
    private bool _isDoubleTapAction = false;
    private bool _isLongPressActive = false;
    private const float DoubleTapDistanceThreshold = 40f;
    private const int DoubleTapTimeoutMs = 300;
    private const int LongPressTimeoutMs = 600;
    private readonly Dictionary<string, SKBitmap> _pinIconCache = [];
    private MapPin _lastTappedPin = null;
    private List<MapPin> _sortedPins = [];

#if WINDOWS
    private bool _isRightMouseRotating = false;
    private float _lastMouseRotationAngle = 0f;
#endif

    public static readonly BindableProperty SourceImagePathProperty =
        BindableProperty.Create(nameof(SourceImagePath), typeof(string), typeof(TileImageView), default(string),
            propertyChanged: async (bindable, oldValue, newValue) =>
            {
                var control = (TileImageView)bindable;
                await control.ProcessNewImageAsync((string)newValue);
            });

    public static readonly BindableProperty TileSizeProperty =
        BindableProperty.Create(nameof(TileSize), typeof(int), typeof(TileImageView), SettingsService.Instance.TileSize,
            propertyChanged: async (bindable, o, n) =>
            {
                var control = (TileImageView)bindable;

                if ((int)o != (int)n && !string.IsNullOrEmpty(control.SourceImagePath))
                    await control.ProcessNewImageAsync(control.SourceImagePath);
                else
                    control._canvasView?.InvalidateSurface();
            });

    public static readonly BindableProperty CurrentRotationProperty =
        BindableProperty.Create(nameof(CurrentRotation), typeof(float), typeof(TileImageView), 0f, defaultBindingMode: BindingMode.TwoWay,
            propertyChanged: OnCurrentRotationChanged);

    public static readonly BindableProperty MaxZoomLevelProperty =
        BindableProperty.Create(nameof(MaxZoomLevel), typeof(int), typeof(TileImageView), SettingsService.Instance.MaxZoomLevel,
            propertyChanged: (bindable, o, n) => ((TileImageView)bindable)._canvasView.InvalidateSurface());

    public static readonly BindableProperty PinsProperty =
        BindableProperty.Create(nameof(Pins), typeof(IEnumerable<MapPin>), typeof(TileImageView), default(IEnumerable<MapPin>),
            propertyChanged: OnPinsChanged);

    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create( nameof(PlaceholderColor), typeof(Color), typeof(TileImageView), Colors.LightGray);

    private static void OnPinsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TileImageView)bindable;

        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnPinsCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnPinsCollectionChanged;

        control.UpdateSortedPins();
        control._canvasView?.InvalidateSurface();
    }

    private static readonly BindablePropertyKey OriginalImageSizePropertyKey = BindableProperty.CreateReadOnly(nameof(OriginalImageSize), typeof(SKSize), typeof(TileImageView), SKSize.Empty);
    private static readonly BindablePropertyKey CurrentScalePropertyKey = BindableProperty.CreateReadOnly(nameof(CurrentScale), typeof(float), typeof(TileImageView), 1.0f);
    private static readonly BindablePropertyKey CurrentRotationPropertyKey = BindableProperty.CreateReadOnly(nameof(CurrentRotation), typeof(float), typeof(TileImageView), 0f);
    private static readonly BindablePropertyKey CurrentPanPropertyKey = BindableProperty.CreateReadOnly(nameof(CurrentPan), typeof(SKPoint), typeof(TileImageView), SKPoint.Empty);
    public static readonly BindableProperty OriginalImageSizeProperty = OriginalImageSizePropertyKey.BindableProperty;
    public static readonly BindableProperty CurrentScaleProperty = CurrentScalePropertyKey.BindableProperty;
    public static readonly BindableProperty CurrentPanProperty = CurrentPanPropertyKey.BindableProperty;

    public string SourceImagePath { get => (string)GetValue(SourceImagePathProperty); set => SetValue(SourceImagePathProperty, value); }
    public int TileSize { get => (int)GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }
    public int MaxZoomLevel { get => (int)GetValue(MaxZoomLevelProperty); set => SetValue(MaxZoomLevelProperty, value); }
    public IEnumerable<MapPin> Pins { get => (IEnumerable<MapPin>)GetValue(PinsProperty); set => SetValue(PinsProperty, value); }
    public SKSize OriginalImageSize { get => (SKSize)GetValue(OriginalImageSizeProperty); private set => SetValue(OriginalImageSizePropertyKey, value); }
    public float CurrentScale { get => (float)GetValue(CurrentScaleProperty); private set => SetValue(CurrentScalePropertyKey, value); }
    public float CurrentRotation { get => (float)GetValue(CurrentRotationProperty); set => SetValue(CurrentRotationProperty, value); }
    public SKPoint CurrentPan { get => (SKPoint)GetValue(CurrentPanProperty); private set => SetValue(CurrentPanPropertyKey, value); }
    public Color PlaceholderColor { get => (Color)GetValue(PlaceholderColorProperty); set => SetValue(PlaceholderColorProperty, value); }
    public ObservableCollection<MapPin> MyPins { get; set; } = [];

    public event EventHandler<MapPin> PinTapped;
    public event EventHandler<MapPin> PinMoved;
    public event EventHandler<MapPin> PinDoubleTapped;
    public event EventHandler<SKPoint> CanvasDoubleTapped;
    public event EventHandler<SKPoint> CanvasLongPressed;

    public TileImageView()
    {
        BackgroundColor = Colors.White;
        _layoutGrid = [];
        _canvasView = new SKGLView
        {
            EnableTouchEvents = true,
            InputTransparent = false
        };
        _canvasView.PaintSurface += OnPaintSurface;
        _canvasView.Touch += OnCanvasTouch;

#if WINDOWS
        this.Loaded += (s, e) =>
        {
            if (_canvasView.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement winView)
            {
                winView.PointerWheelChanged += OnWinViewPointerWheelChanged;
                winView.PointerPressed += OnWinViewPointerPressed;
                winView.PointerMoved += OnWinViewPointerMoved;
                winView.PointerReleased += OnWinViewPointerReleased;
            }
        };
#endif

        _loadingIndicator = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(16),
            Opacity = 0.6,
            Color = Colors.Black
        };

        _layoutGrid.Children.Add(_canvasView);
        _layoutGrid.Children.Add(_loadingIndicator);
        Content = _layoutGrid;
    }

    public void ZoomToPin(string pinId, double? factor = null)
    {
        if (Pins == null || OriginalImageSize == SKSize.Empty) return;

        if (_canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
        {
            _pendingPinId = pinId;
            _pendingZoomFactor = (float?)factor;
            _pendingImageFit = false;
            return;
        }

        var pin = Pins.FirstOrDefault(p => p.Id == pinId);
        if (pin == null) return;

        _rotationDegrees = 0f;
        _scale = factor.HasValue ? (float)factor.Value : 1.0f;

        float pinAbsX = pin.RelativeX * OriginalImageSize.Width;
        float pinAbsY = pin.RelativeY * OriginalImageSize.Height;

        float scaledX = pinAbsX * _scale;
        float scaledY = pinAbsY * _scale;

        float canvasWidth = _canvasView.CanvasSize.Width;
        float canvasHeight = _canvasView.CanvasSize.Height;

        _panX = (canvasWidth / 2f) - scaledX;
        _panY = (canvasHeight / 2f) - scaledY;

        CurrentScale = _scale;
        CurrentPan = new SKPoint(_panX, _panY);
        CurrentRotation = _rotationDegrees;
        _canvasView.InvalidateSurface();
    }

    public void HandleMouseZoom(SKPoint mouseLocation, float delta)
    {
        if (OriginalImageSize == SKSize.Empty || _isGenerating) return;

        float zoomFactor = delta > 0 ? 1.1f : 0.9f;
        float oldScale = _scale;
        float newScale = Math.Clamp(_scale * zoomFactor, 0.1f, 16.0f);

        if (Math.Abs(newScale - oldScale) < 0.001f) return;

        Point relativePoint = GetRelativeFactorFromScreenPoint(mouseLocation);

        _scale = newScale;

        float imagePixelX = (float)relativePoint.X * OriginalImageSize.Width;
        float imagePixelY = (float)relativePoint.Y * OriginalImageSize.Height;

        SKMatrix matrix = SKMatrix.CreateRotationDegrees(_rotationDegrees);
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        SKPoint transformedPoint = matrix.MapPoint(imagePixelX, imagePixelY);

        _panX = mouseLocation.X - transformedPoint.X;
        _panY = mouseLocation.Y - transformedPoint.Y;

        CurrentScale = _scale;
        CurrentPan = new SKPoint(_panX, _panY);

        _canvasView.InvalidateSurface();
    }

    public Point GetPlanFactorAtControlCenter()
    {
        if (OriginalImageSize == SKSize.Empty || _canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
            return new Point(0, 0);

        float centerX = (float)_canvasView.CanvasSize.Width / 2f;
        float centerY = (float)_canvasView.CanvasSize.Height / 2f;

        float dx = centerX - _panX;
        float dy = centerY - _panY;
        float negRad = -_rotationDegrees * (float)(Math.PI / 180.0);
        float cosNeg = (float)Math.Cos(negRad);
        float sinNeg = (float)Math.Sin(negRad);
        float pixelX = (dx * cosNeg - dy * sinNeg) / _scale;
        float pixelY = (dx * sinNeg + dy * cosNeg) / _scale;
        double factorX = Math.Clamp(pixelX / OriginalImageSize.Width, 0.0, 1.0);
        double factorY = Math.Clamp(pixelY / OriginalImageSize.Height, 0.0, 1.0);
        return new Point(factorX, factorY);
    }

    public Point GetRelativeFactorFromScreenPoint(SKPoint screenPoint)
    {
        if (OriginalImageSize == SKSize.Empty) return new Point(0, 0);

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix))
            return new Point(0, 0);

        SKPoint imagePixel = inverseMatrix.MapPoint(screenPoint);

        double factorX = imagePixel.X / OriginalImageSize.Width;
        double factorY = imagePixel.Y / OriginalImageSize.Height;

        return new Point(factorX, factorY);
    }

    public Point ConvertScreenToRelativePoint(SKPoint screenPoint)
    {
        if (OriginalImageSize == SKSize.Empty)
            return new Point(0, 0);

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (matrix.TryInvert(out SKMatrix inverseMatrix))
        {
            SKPoint planPoint = inverseMatrix.MapPoint(screenPoint);

            double factorX = Math.Clamp(planPoint.X / OriginalImageSize.Width, 0.0, 1.0);
            double factorY = Math.Clamp(planPoint.Y / OriginalImageSize.Height, 0.0, 1.0);

            return new Point(factorX, factorY);
        }

        return new Point(0, 0);
    }

    public void ImageFit()
    {
        if (_canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
        {
            _pendingImageFit = true;
            _pendingPinId = null;
            return;
        }

        float canvasWidth = _canvasView.CanvasSize.Width;
        float canvasHeight = _canvasView.CanvasSize.Height;

        _scale = Math.Min(canvasWidth / OriginalImageSize.Width, canvasHeight / OriginalImageSize.Height);
        _rotationDegrees = 0f;

        _panX = (canvasWidth / 2f) - (_scale * OriginalImageSize.Width / 2f);
        _panY = (canvasHeight / 2f) - (_scale * OriginalImageSize.Height / 2f);

        CurrentScale = _scale;
        CurrentPan = new SKPoint(_panX, _panY);
        CurrentRotation = _rotationDegrees;
        _canvasView.InvalidateSurface();
    }

    public void InvalidateSurface()
    {
        _canvasView?.InvalidateSurface();
    }

#if WINDOWS
    private void OnWinViewPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (OriginalImageSize == SKSize.Empty || _isGenerating) return;

        var pointerPoint = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
        var position = pointerPoint.Position;
        int delta = pointerPoint.Properties.MouseWheelDelta;

        float density = (float)Settings.DisplayDensity;
        SKPoint mousePos = new ((float)position.X * density, (float)position.Y * density);

        HandleMouseZoom(mousePos, delta);
        e.Handled = true;
    }

    private void OnWinViewPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var winView = (Microsoft.UI.Xaml.UIElement)sender;
        var pointerPoint = e.GetCurrentPoint(winView);
    
        if (pointerPoint.Properties.IsRightButtonPressed)
        {
            if (OriginalImageSize == SKSize.Empty || _isGenerating) return;

            _isRightMouseRotating = true;
        
            float density = (float)Settings.DisplayDensity;
            SKPoint mousePos = new((float)pointerPoint.Position.X * density, (float)pointerPoint.Position.Y * density);
        
            float centerX = (float)_canvasView.CanvasSize.Width / 2f;
            float centerY = (float)_canvasView.CanvasSize.Height / 2f;
        
            _lastMouseRotationAngle = (float)Math.Atan2(mousePos.Y - centerY, mousePos.X - centerX);
        
            winView.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnWinViewPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isRightMouseRotating || OriginalImageSize == SKSize.Empty || _isGenerating) return;

        var winView = (Microsoft.UI.Xaml.UIElement)sender;
        var pointerPoint = e.GetCurrentPoint(winView);
    
            float density = (float)Settings.DisplayDensity;
        SKPoint mousePos = new((float)pointerPoint.Position.X * density, (float)pointerPoint.Position.Y * density);

        float centerX = (float)_canvasView.CanvasSize.Width / 2f;
        float centerY = (float)_canvasView.CanvasSize.Height / 2f;
   
        float currentAngle = (float)Math.Atan2(mousePos.Y - centerY, mousePos.X - centerX);
        float angleDiff = currentAngle - _lastMouseRotationAngle;
    
        if (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
        if (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

        if (Math.Abs(angleDiff) > 0.001f)
        {
            float rotationDiffDegrees = angleDiff * (180f / (float)Math.PI);
            _rotationDegrees += rotationDiffDegrees;

            float cos = (float)Math.Cos(angleDiff);
            float sin = (float)Math.Sin(angleDiff);
            float dx = _panX - centerX;
            float dy = _panY - centerY;

            _panX = centerX + (dx * cos - dy * sin);
            _panY = centerY + (dx * sin + dy * cos);

            _lastMouseRotationAngle = currentAngle;

            CurrentRotation = _rotationDegrees;
            CurrentPan = new SKPoint(_panX, _panY);

            _canvasView.InvalidateSurface();
        }
        e.Handled = true;
    }

    private void OnWinViewPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isRightMouseRotating)
        {
            _isRightMouseRotating = false;
            var winView = (Microsoft.UI.Xaml.UIElement)sender;
            winView.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }
#endif
    private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        if (_pendingImageFit && _canvasView.CanvasSize.Width > 0 && _canvasView.CanvasSize.Height > 0)
        {
            _pendingImageFit = false;
            ImageFit();
            return;
        }
        else if (_pendingPinId != null && _canvasView.CanvasSize.Width > 0 && _canvasView.CanvasSize.Height > 0)
        {
            string id = _pendingPinId;
            double? factor = _pendingZoomFactor;
            _pendingPinId = null;
            ZoomToPin(id, factor);
            return;
        }

        var canvas = e.Surface.Canvas;

        canvas.Clear(PlaceholderColor.ToSKColor());

        if (_isGenerating || string.IsNullOrEmpty(_computedTileFolder)) return;

        int currentZoom = MaxZoomLevel + (int)Math.Floor(Math.Log2(_scale));
        currentZoom = Math.Clamp(currentZoom, 2, MaxZoomLevel);

        canvas.Save();
        canvas.Translate(_panX, _panY);
        canvas.RotateDegrees(_rotationDegrees);
        canvas.Scale(_scale);

        float tileScaleFactor = (float)Math.Pow(2, MaxZoomLevel - currentZoom);
        float currentTileSizeInCanvasSpace = TileSize * tileScaleFactor;

        int maxTiles = (int)Math.Pow(2, currentZoom);

        float canvasWidth = _canvasView.CanvasSize.Width;
        float canvasHeight = _canvasView.CanvasSize.Height;

        float viewRadius = (float)Math.Sqrt(canvasWidth * canvasWidth + canvasHeight * canvasHeight) / (2f * _scale);

        float canvasCenterX = canvasWidth / 2f;
        float canvasCenterY = canvasHeight / 2f;

        float dx = canvasCenterX - _panX;
        float dy = canvasCenterY - _panY;

        float negRad = -_rotationDegrees * (float)(Math.PI / 180.0);
        float cosNeg = (float)Math.Cos(negRad);
        float sinNeg = (float)Math.Sin(negRad);

        float tileCenterX = (dx * cosNeg - dy * sinNeg) / _scale;
        float tileCenterY = (dx * sinNeg + dy * cosNeg) / _scale;

        float viewLeft = tileCenterX - viewRadius;
        float viewTop = tileCenterY - viewRadius;
        float viewRight = tileCenterX + viewRadius;
        float viewBottom = tileCenterY + viewRadius;

        int minX = (int)Math.Floor(viewLeft / currentTileSizeInCanvasSpace);
        int minY = (int)Math.Floor(viewTop / currentTileSizeInCanvasSpace);
        int maxX = (int)Math.Ceiling(viewRight / currentTileSizeInCanvasSpace);
        int maxY = (int)Math.Ceiling(viewBottom / currentTileSizeInCanvasSpace);

        minX = Math.Clamp(minX, 0, maxTiles - 1);
        minY = Math.Clamp(minY, 0, maxTiles - 1);
        maxX = Math.Clamp(maxX, 0, maxTiles - 1);
        maxY = Math.Clamp(maxY, 0, maxTiles - 1);

        string zoomFolder = Path.Combine(_computedTileFolder, currentZoom.ToString());

        if (!Directory.Exists(zoomFolder)) return;

        for (int x = minX; x <= maxX; x++)
        {
            string xFolder = Path.Combine(zoomFolder, x.ToString());
            if (!Directory.Exists(xFolder)) continue;

            for (int y = minY; y <= maxY; y++)
            {
                string cacheKey = $"{currentZoom}_{x}_{y}";

                if (!_tileCache.TryGetValue(cacheKey, out var bitmap))
                {
                    float posX = x * currentTileSizeInCanvasSpace;
                    float posY = y * currentTileSizeInCanvasSpace;
                    var destRect = new SKRect(posX, posY, posX + currentTileSizeInCanvasSpace, posY + currentTileSizeInCanvasSpace);

                    if (_loadingTiles.Add(cacheKey))
                    {
                        string tilePath = Path.Combine(xFolder, $"{y}.webp");

                        _ = Task.Run(() =>
                        {
                            try
                            {
                                if (File.Exists(tilePath))
                                {
                                    using var stream = File.OpenRead(tilePath);
                                    var decodedBitmap = SKBitmap.Decode(stream);
                                    if (decodedBitmap != null)
                                    {
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            _tileCache[cacheKey] = decodedBitmap;
                                            _canvasView.InvalidateSurface();
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Fehler beim Kachelladen: {ex.Message}");
                            }
                            finally
                            {
                                MainThread.BeginInvokeOnMainThread(() => _loadingTiles.Remove(cacheKey));
                            }
                        });
                    }

                    int fallbackZoom = currentZoom - 1;
                    int fallbackX = x / 2;
                    int fallbackY = y / 2;
                    int deltaZoom = 1;

                    while (fallbackZoom >= 0)
                    {
                        string fallbackKey = $"{fallbackZoom}_{fallbackX}_{fallbackY}";

                        if (_tileCache.TryGetValue(fallbackKey, out var fallbackBitmap))
                        {
                            int factor = 1 << deltaZoom;

                            float srcWidth = (float)TileSize / factor;
                            float srcHeight = (float)TileSize / factor;

                            float srcX = (x % factor) * srcWidth;
                            float srcY = (y % factor) * srcHeight;

                            var srcRect = new SKRect(srcX, srcY, srcX + srcWidth, srcY + srcHeight);

                            canvas.DrawBitmap(fallbackBitmap, srcRect, destRect, LinearSampling, null);

                            break;
                        }

                        fallbackZoom--;
                        fallbackX /= 2;
                        fallbackY /= 2;
                        deltaZoom++;
                    }
                }
                else
                {
                    float posX = x * currentTileSizeInCanvasSpace;
                    float posY = y * currentTileSizeInCanvasSpace;
                    var destRect = new SKRect(posX, posY, posX + currentTileSizeInCanvasSpace, posY + currentTileSizeInCanvasSpace);

                    canvas.DrawBitmap(bitmap, destRect, LinearSampling, null);
                }
            }
        }

        if (Pins != null && OriginalImageSize != SKSize.Empty)
        {
            float padding = 50f;
            float l = viewLeft - padding;
            float r = viewRight + padding;
            float t = viewTop - padding;
            float b = viewBottom + padding;

            foreach (var pin in _sortedPins)
            {
                SKBitmap pinBitmap = pin.Icon ?? GetOrLoadPinBitmap(pin);
                if (pinBitmap == null) continue;

                float absoluteX = pin.RelativeX * OriginalImageSize.Width;
                float absoluteY = pin.RelativeY * OriginalImageSize.Height;

                if (absoluteX < l || absoluteX > r || absoluteY < t || absoluteY > b)
                    continue;

                canvas.Save();
                canvas.Translate(absoluteX, absoluteY);

                if (!pin.IsLockRotate)
                    canvas.RotateDegrees(-_rotationDegrees);
                else
                    canvas.RotateDegrees(pin.Rotation);

                float pinScale = GetPinScale(pin);
                canvas.Scale(pinScale, pinScale);

                float left = -(float)(pin.Anchor.X * pinBitmap.Width);
                float top = -(float)(pin.Anchor.Y * pinBitmap.Height);

                canvas.DrawBitmap(pinBitmap, left, top, LinearSampling, null);
                canvas.Restore();
            }
        }

        canvas.Restore();
    }

    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
#if WINDOWS
        if (_isRightMouseRotating)
        {
            _activeTouches.Clear();
            _draggedPin = null;
            _longPressCts?.Cancel();
            e.Handled = true;
            return;
        }
#endif
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _isLongPressActive = false;

                _activeTouches[e.Id] = e.Location;
                if (_activeTouches.Count == 1)
                {
                    _touchStartPoint = e.Location;
                    _isDoubleTapAction = false;

                    _draggedPin = GetPinAtPosition(e.Location);

                    if (_draggedPin != null && _draggedPin.IsLockPosition)
                        _draggedPin = null;

                    if (_draggedPin != null)
                    {
                        _originalPinX = _draggedPin.RelativeX;
                        _originalPinY = _draggedPin.RelativeY;

                        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
                        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
                        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

                        if (matrix.TryInvert(out SKMatrix inverseMatrix))
                        {
                            SKPoint planPoint = inverseMatrix.MapPoint(e.Location);
                            float pinAbsX = _draggedPin.RelativeX * OriginalImageSize.Width;
                            float pinAbsY = _draggedPin.RelativeY * OriginalImageSize.Height;
                            _dragOffset = new SKPoint(planPoint.X - pinAbsX, planPoint.Y - pinAbsY);
                        }
                    }
                    else
                    {
                        _longPressCts?.Cancel();
                        _longPressCts = new CancellationTokenSource();
                        var token = _longPressCts.Token;
                        var lpLocation = e.Location;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(LongPressTimeoutMs, token);
                                if (!token.IsCancellationRequested)
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        _isLongPressActive = true;
                                        CanvasLongPressed?.Invoke(this, lpLocation);
                                    });
                                }
                            }
                            catch (OperationCanceledException) { }
                        });
                    }
                }

                if (_activeTouches.Count == 2)
                {
                    _draggedPin = null;
                    _longPressCts?.Cancel();
                    var points = _activeTouches.Values.ToArray();
                    _oldFingerDistance = SKPoint.Distance(points[0], points[1]);
                    _oldFingerAngle = (float)Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
                }
                break;

            case SKTouchAction.Moved:
                if (_isGenerating) break;
                if (_isDoubleTapAction) break;
                if (_isLongPressActive) break;

                if (_activeTouches.Count == 1 && SKPoint.Distance(_touchStartPoint, e.Location) > ClickThreshold)
                    _longPressCts?.Cancel();

                bool shouldInvalidate = false;

                if (_draggedPin != null && _activeTouches.Count == 1)
                {
                    UpdateDraggedPinPosition(e.Location);
                    shouldInvalidate = true;
                }
                else if (_activeTouches.Count == 1 && _activeTouches.TryGetValue(e.Id, out SKPoint oldPt))
                {
                    _panX += e.Location.X - oldPt.X;
                    _panY += e.Location.Y - oldPt.Y;
                    _activeTouches[e.Id] = e.Location;

                    CurrentPan = new SKPoint(_panX, _panY);
                    shouldInvalidate = true;
                }
                else if (_activeTouches.Count == 2 && _activeTouches.ContainsKey(e.Id))
                {
                    _activeTouches[e.Id] = e.Location;
                    var points = _activeTouches.Values.ToArray();
                    float centerX = (points[0].X + points[1].X) / 2f;
                    float centerY = (points[0].Y + points[1].Y) / 2f;
                    float newDistance = SKPoint.Distance(points[0], points[1]);

                    if (_oldFingerDistance > 0)
                    {
                        float scaleFactor = newDistance / _oldFingerDistance;
                        float newScale = Math.Clamp(_scale * scaleFactor, 0.1f, 16.0f);

                        float scaleRatio = newScale / _scale;
                        _panX = centerX - (centerX - _panX) * scaleRatio;
                        _panY = centerY - (centerY - _panY) * scaleRatio;
                        _scale = newScale;
                    }
                    _oldFingerDistance = newDistance;

                    float newAngle = (float)Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);

                    if (_oldFingerAngle != 0f)
                    {
                        float angleDiff = newAngle - _oldFingerAngle;
                        float rotationDiffDegrees = angleDiff * (180f / (float)Math.PI);
                        _rotationDegrees += rotationDiffDegrees;

                        double rad = angleDiff;
                        float cos = (float)Math.Cos(rad);
                        float sin = (float)Math.Sin(rad);
                        float dx = _panX - centerX;
                        float dy = _panY - centerY;

                        _panX = centerX + (dx * cos - dy * sin);
                        _panY = centerY + (dx * sin + dy * cos);
                    }
                    _oldFingerAngle = newAngle;
                    shouldInvalidate = true;
                }

                if (shouldInvalidate)
                {
                    CurrentScale = _scale;
                    CurrentPan = new SKPoint(_panX, _panY);
                    CurrentRotation = _rotationDegrees;

                    _canvasView.InvalidateSurface();
                }
                break;

            case SKTouchAction.Released:
                _longPressCts?.Cancel();

                if (_isLongPressActive)
                {
                    _isLongPressActive = false;
                    _draggedPin = null;
                    _activeTouches.Remove(e.Id);
                    break;
                }

                if (_activeTouches.Count == 1 && SKPoint.Distance(_touchStartPoint, e.Location) < ClickThreshold)
                {
                    var now = DateTime.UtcNow;
                    double elapsed = (now - _lastTapTime).TotalMilliseconds;
                    float distance = SKPoint.Distance(_lastTapLocation, e.Location);

                    var currentPin = GetPinAtPosition(e.Location);

                    if (elapsed < DoubleTapTimeoutMs && distance < DoubleTapDistanceThreshold)
                    {
                        _isDoubleTapAction = true;
                        _tapCts?.Cancel();
                        _tapCts = null;
                        _lastTapTime = DateTime.MinValue;
                        _lastTappedPin = null;

                        if (currentPin != null)
                            PinDoubleTapped?.Invoke(this, currentPin);
                        else
                            CanvasDoubleTapped?.Invoke(this, e.Location);
                    }
                    else
                    {
                        _isDoubleTapAction = false;
                        _lastTapTime = now;
                        _lastTapLocation = e.Location;
                        _lastTappedPin = currentPin;

                        if (currentPin != null)
                        {
                            _tapCts?.Cancel();
                            _tapCts = new CancellationTokenSource();
                            var token = _tapCts.Token;

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(250, token);

                                    if (!token.IsCancellationRequested)
                                    {
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            PinTapped?.Invoke(this, currentPin);
                                        });
                                    }
                                }
                                catch (OperationCanceledException) { }
                            });
                        }
                    }
                }
                else if (_draggedPin != null && SKPoint.Distance(_touchStartPoint, e.Location) >= ClickThreshold)
                    PinMoved?.Invoke(this, _draggedPin);

                if (_draggedPin != null)
                {
                    if (SKPoint.Distance(_touchStartPoint, e.Location) < ClickThreshold)
                    {
                        _draggedPin.RelativeX = _originalPinX;
                        _draggedPin.RelativeY = _originalPinY;
                    }
                    _draggedPin = null;
                }

                _activeTouches.Remove(e.Id);
                if (_activeTouches.Count < 2)
                {
                    _oldFingerDistance = 0f;
                    _oldFingerAngle = 0f;
                }
                _canvasView.InvalidateSurface();
                break;

            case SKTouchAction.Cancelled:
                _longPressCts?.Cancel();
                _isDoubleTapAction = false;
                _isLongPressActive = false;
                if (_draggedPin != null)
                {
                    _draggedPin.RelativeX = _originalPinX;
                    _draggedPin.RelativeY = _originalPinY;
                    _draggedPin = null;
                }
                _activeTouches.Remove(e.Id);
                _canvasView.InvalidateSurface();
                break;
        }

        e.Handled = true;
    }

    private static void OnCurrentRotationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TileImageView)bindable;
        float newRotation = (float)newValue;

        if (Math.Abs(control._rotationDegrees - newRotation) > 0.01f)
        {
            if (control._canvasView.CanvasSize.Width > 0 && control._canvasView.CanvasSize.Height > 0)
            {
                float centerX = (float)control._canvasView.CanvasSize.Width / 2f;
                float centerY = (float)control._canvasView.CanvasSize.Height / 2f;
                float angleDiffDegrees = newRotation - control._rotationDegrees;
                double rad = angleDiffDegrees * (Math.PI / 180.0);

                float cos = (float)Math.Cos(rad);
                float sin = (float)Math.Sin(rad);
                float dx = control._panX - centerX;
                float dy = control._panY - centerY;

                control._panX = centerX + (dx * cos - dy * sin);
                control._panY = centerY + (dx * sin + dy * cos);
                control.CurrentPan = new SKPoint(control._panX, control._panY);
            }

            control._rotationDegrees = newRotation;
            control._canvasView?.InvalidateSurface();
        }
    }

    private void OnPinsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateSortedPins();
        _canvasView?.InvalidateSurface();
    }

    private async Task ProcessNewImageAsync(string imagePath)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            _computedTileFolder = string.Empty;
            ClearCache();
            _canvasView.InvalidateSurface();
            return;
        }

        ClearCache();

        try
        {
            using (var codec = SKCodec.Create(imagePath))
            {
                if (codec != null)
                    OriginalImageSize = new SKSize(codec.Info.Width, codec.Info.Height);
            }

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
            CleanupOldTileFolders(imagePath, TileSize);

            _computedTileFolder = Path.Combine(FileSystem.AppDataDirectory, "Tiles", $"{fileNameWithoutExt}_{TileSize}");
            _scale = 1.0f;
            _panX = 0f;
            _panY = 0f;
            _rotationDegrees = 0f;
            CurrentScale = _scale;
            CurrentPan = new SKPoint(_panX, _panY);
            CurrentRotation = _rotationDegrees;
            _canvasView.IsVisible = true;
            _isGenerating = false;

            bool tilesExist = Directory.Exists(_computedTileFolder) &&
                              Directory.GetFiles(_computedTileFolder, "*.webp", SearchOption.AllDirectories).Length > 0;

            if (!tilesExist)
            {
                _loadingIndicator.IsVisible = true;
                _loadingIndicator.IsRunning = true;

                await Task.Run(() => GenerateTilePyramidInternal(
                    imagePath,
                    _computedTileFolder,
                    MaxZoomLevel,
                    TileSize,
                    token,
                    () => MainThread.BeginInvokeOnMainThread(() => _canvasView.InvalidateSurface())
                ), token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Laden des Hintergrundbildes: {ex.Message}");
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
            _canvasView.IsVisible = true;
            _isGenerating = false;
            _canvasView.InvalidateSurface();
        }
    }

    private static void CleanupOldTileFolders(string imagePath, int currentTileSize)
    {
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
        string baseFolder = Path.Combine(FileSystem.AppDataDirectory, "Tiles");

        if (!Directory.Exists(baseFolder)) return;

        var allDirs = Directory.GetDirectories(baseFolder, $"{fileNameWithoutExt}_*");

        foreach (var dir in allDirs)
        {
            if (!dir.EndsWith($"_{currentTileSize}"))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Fehler beim Loeschen alter Kacheln: {ex.Message}");
                }
            }
        }
    }

    private void UpdateSortedPins()
    {
        if (Pins == null)
        {
            _sortedPins.Clear();
            return;
        }

        _sortedPins = [.. Pins
        .OrderByDescending(p => p.IsCustomPin)
        .ThenByDescending(p => p.PinScale)
        ];
    }

    private static void GenerateTilePyramidInternal(string sourceImagePath, string outputFolder, int maxZoomLevels, int tileSize, CancellationToken token, Action onLevelGenerated = null)
    {
        using var codec = SKCodec.Create(sourceImagePath);
        if (codec == null) return;
        using var originalBitmap = SKBitmap.Decode(codec);
        int origWidth = originalBitmap.Width;
        int origHeight = originalBitmap.Height;

        for (int zoom = 0; zoom <= maxZoomLevels; zoom++)
        {
            token.ThrowIfCancellationRequested();

            double scale = Math.Pow(0.5, maxZoomLevels - zoom);
            int levelWidth = (int)(origWidth * scale);
            int levelHeight = (int)(origHeight * scale);

            using var scaledBitmap = originalBitmap.Resize(new SKImageInfo(levelWidth, levelHeight), LinearSampling);
            if (scaledBitmap == null) continue;

            int tilesX = (int)Math.Ceiling((double)levelWidth / tileSize);
            int tilesY = (int)Math.Ceiling((double)levelHeight / tileSize);

            for (int x = 0; x < tilesX; x++)
            {
                token.ThrowIfCancellationRequested();

                for (int y = 0; y < tilesY; y++)
                {
                    token.ThrowIfCancellationRequested();

                    string tileDirectory = Path.Combine(outputFolder, zoom.ToString(), x.ToString());
                    Directory.CreateDirectory(tileDirectory);

                    string tilePath = Path.Combine(tileDirectory, $"{y}.webp");

                    int srcX = x * tileSize;
                    int srcY = y * tileSize;
                    int width = Math.Min(tileSize, levelWidth - srcX);
                    int height = Math.Min(tileSize, levelHeight - srcY);

                    using var tileBitmap = new SKBitmap(tileSize, tileSize);
                    using (var canvas = new SKCanvas(tileBitmap))
                    {
                        canvas.Clear(SKColors.Transparent);
                        var srcRect = new SKRect(srcX, srcY, srcX + width, srcY + height);
                        var destRect = new SKRect(0, 0, width, height);
                        canvas.DrawBitmap(scaledBitmap, srcRect, destRect, LinearSampling, null);
                    }

                    using var image = SKImage.FromBitmap(tileBitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Webp, 90);
                    using var stream = File.OpenWrite(tilePath);
                    data.SaveTo(stream);
                }
            }
            onLevelGenerated?.Invoke();
        }
    }

    private SKBitmap GetOrLoadPinBitmap(MapPin pin)
    {
        if (string.IsNullOrEmpty(pin.IconPath)) return null;

        if (_pinIconCache.TryGetValue(pin.IconPath, out var cachedBitmap))
            return cachedBitmap;

        if (File.Exists(pin.IconPath))
        {
            try
            {
                using var stream = File.OpenRead(pin.IconPath);
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    _pinIconCache[pin.IconPath] = bitmap;
                    return bitmap;
                }
            }
            catch { /* Laden fehlgeschlagen */ }
        }

        string cacheFolder = Settings.CacheDirectory;
        if (!Directory.Exists(cacheFolder))
            Directory.CreateDirectory(cacheFolder);

        string fileName = Path.GetFileName(pin.IconPath);
        string targetCachePath = Path.Combine(cacheFolder, fileName);

        if (File.Exists(targetCachePath))
        {
            try
            {
                using var stream = File.OpenRead(targetCachePath);
                var bitmap = SKBitmap.Decode(stream);
                if (bitmap != null)
                {
                    _pinIconCache[pin.IconPath] = bitmap;
                    return bitmap;
                }
            }
            catch { /* Laden fehlgeschlagen */ }
        }

        try
        {
            SKBitmap extractedBitmap = null;

#if ANDROID
            var context = Android.App.Application.Context;
            string imageName = Path.GetFileNameWithoutExtension(fileName).ToLower();
            int resId = context.Resources.GetIdentifier(imageName, "drawable", context.PackageName);

            if (resId != 0)
            {
                using var resourceStream = context.Resources.OpenRawResource(resId);
                using var targetStream = File.Create(targetCachePath);
                resourceStream.CopyTo(targetStream);
                targetStream.Close();

                using var readStream = File.OpenRead(targetCachePath);
                extractedBitmap = SKBitmap.Decode(readStream);
            }
#elif IOS
        string imageName = Path.GetFileNameWithoutExtension(fileName);
        using var uiImage = UIKit.UIImage.FromBundle(imageName);
        if (uiImage != null)
        {
            // In ein PNG-Datenobjekt umwandeln
            using var nsData = uiImage.AsPNG();
            if (nsData != null)
            {
                using var stream = nsData.AsStream();
                using var targetStream = File.Create(targetCachePath);
                stream.CopyTo(targetStream);
                targetStream.Close();

                using var readStream = File.OpenRead(targetCachePath);
                extractedBitmap = SKBitmap.Decode(readStream);
            }
        }
#else
        using var stream = Task.Run(() => FileSystem.OpenAppPackageFileAsync(pin.IconPath)).Result;
        using var targetStream = File.Create(targetCachePath);
        stream.CopyTo(targetStream);
        targetStream.Close();

        using var readStream = File.OpenRead(targetCachePath);
        extractedBitmap = SKBitmap.Decode(readStream);
#endif

            if (extractedBitmap != null)
            {
                _pinIconCache[pin.IconPath] = extractedBitmap;
                return extractedBitmap;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim automatischen Extrahieren des Pins: {ex.Message}");
        }

        return null;
    }

    private MapPin GetPinAtPosition(SKPoint touchPoint)
    {
        if (Pins == null || OriginalImageSize == SKSize.Empty) return null;

        for (int i = _sortedPins.Count - 1; i >= 0; i--)
        {
            var pin = _sortedPins[i];

            SKBitmap pinBitmap = pin.Icon ?? GetOrLoadPinBitmap(pin);
            if (pinBitmap == null) continue;

            SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
            matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
            matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

            float absoluteX = pin.RelativeX * OriginalImageSize.Width;
            float absoluteY = pin.RelativeY * OriginalImageSize.Height;
            matrix = matrix.PreConcat(SKMatrix.CreateTranslation(absoluteX, absoluteY));

            if (!pin.IsLockRotate)
                matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(-_rotationDegrees));
            else
                matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(pin.Rotation));

            float pinScale = GetPinScale(pin);
            matrix = matrix.PreConcat(SKMatrix.CreateScale(pinScale, pinScale));

            if (!matrix.TryInvert(out SKMatrix inverseMatrix)) continue;

            SKPoint localPoint = inverseMatrix.MapPoint(touchPoint);

            float left = -(float)(pin.Anchor.X * pinBitmap.Width);
            float top = -(float)(pin.Anchor.Y * pinBitmap.Height);
            float right = left + pinBitmap.Width;
            float bottom = top + pinBitmap.Height;

            var localBounds = new SKRect(left, top, right, bottom);

            if (localBounds.Contains(localPoint.X, localPoint.Y))
                return pin;
        }
        return null;
    }

    private float GetPinScale(MapPin pin)
    {
        if (pin.IsCustomPin || pin.IsLockAutoScale)
            return pin.PinScale;

        double currentScale = _scale > 0 ? _scale : 1.0;
        double dynamicScale = 1.0 / currentScale;
        double maxLimit = Services.SettingsService.Instance.PinMaxScaleLimit / 100.0;
        double minLimit = Services.SettingsService.Instance.PinMinScaleLimit / 100.0;

        if (dynamicScale > maxLimit) dynamicScale = maxLimit;
        if (dynamicScale < minLimit) dynamicScale = minLimit;

        return (float)(Services.SettingsService.Instance.OsBaseScale * dynamicScale * pin.PinScale);
    }

    private void UpdateDraggedPinPosition(SKPoint touchPoint)
    {
        if (_draggedPin == null || OriginalImageSize == SKSize.Empty) return;

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix)) return;
        SKPoint planPoint = inverseMatrix.MapPoint(touchPoint);

        float newRelX = (planPoint.X - _dragOffset.X) / OriginalImageSize.Width;
        float newRelY = (planPoint.Y - _dragOffset.Y) / OriginalImageSize.Height;

        _draggedPin.RelativeX = Math.Clamp(newRelX, 0f, 1f);
        _draggedPin.RelativeY = Math.Clamp(newRelY, 0f, 1f);
    }

    private void ClearCache()
    {
        _tileCache.Clear();
        _loadingTiles.Clear();

        foreach (var bitmap in _pinIconCache.Values)
            bitmap?.Dispose();

        _pinIconCache.Clear();
    }
}

public class MapPin
{
    public string Id { get; set; }
    public float RelativeX { get; set; }
    public float RelativeY { get; set; }
    public float Rotation { get; set; }
    public SKBitmap Icon { get; set; }
    public string IconPath { get; set; }
    public bool IsLockRotate { get; set; } = false;
    public bool IsLockPosition { get; set; } = false;
    public bool IsCustomPin { get; set; }
    public bool IsLockAutoScale { get; set; }
    public float PinScale { get; set; } = 1.0f;
    public Point Anchor { get; set; } = new Point(0.5, 0.5);
}

public class LruCache<TKey, TValue>(int capacity) where TValue : IDisposable
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _cache = [];
    private readonly LinkedList<(TKey Key, TValue Value)> _list = new();

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (_cache.TryGetValue(key, out var node))
        {
            _list.Remove(node);
            _list.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        set
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _list.Remove(existingNode);
                existingNode.Value.Value?.Dispose();
                _cache.Remove(key);
            }

            if (_cache.Count >= capacity)
            {
                var last = _list.Last;
                if (last != null)
                {
                    last.Value.Value?.Dispose();
                    _cache.Remove(last.Value.Key);
                    _list.RemoveLast();
                }
            }

            var newNode = new LinkedListNode<(TKey, TValue)>((key, value));
            _list.AddFirst(newNode);
            _cache[key] = newNode;
        }
    }

    public void Clear()
    {
        foreach (var (_, value) in _list)
            value?.Dispose();

        _list.Clear();
        _cache.Clear();
    }
}