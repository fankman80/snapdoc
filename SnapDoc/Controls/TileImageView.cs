#nullable disable
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
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
    private readonly LruCache<TileKey, SKBitmap> _tileCache = new(SettingsService.Instance.MaxTileCache);
    private readonly Dictionary<long, SKPoint> _activeTouches = [];
    private float _oldFingerDistance = 0f;
    private float _oldFingerAngle = 0f;
    private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private SKPoint _touchStartPoint;
    private const float ClickThreshold = 15f;
    private bool _hasDraggedPin = false;
    private DateTime _touchStartTime;
    private MapPin _draggedPin = null;
    private string _pendingPinId = null;
    private double? _pendingZoomFactor = null;
    private bool _pendingImageFit = false;
    private float _originalPinX;
    private float _originalPinY;
    private CancellationTokenSource _cts;
    private CancellationTokenSource _longPressCts;
    private CancellationTokenSource _tapCts;
    private readonly HashSet<TileKey> _loadingTiles = [];
    private DateTime _lastTapTime = DateTime.MinValue;
    private SKPoint _lastTapLocation = SKPoint.Empty;
    private bool _isDoubleTapAction = false;
    private bool _isLongPressActive = false;
    private const float DoubleTapDistanceThreshold = 40f;
    private const int DoubleTapTimeoutMs = 300;
    private const int LongPressTimeoutMs = 600;
    private readonly Dictionary<string, SKBitmap> _pinIconCache = [];
    private List<MapPin> _sortedPins = [];
    private SKPoint _lastTouchPoint;
    private readonly HashSet<string> _loadingPinPaths = [];
    private readonly SKPaint _loupeShadowPaint;
    private readonly float _loupeRadius = 150f;
    private float _cachedLoupeRadius = -1f;
    private SKPath _cachedLoupePath;
    private SKShader _cachedInnerShadowShader;
    private SKShader _cachedGlareShader;
    private static readonly SKPaint GrayscalePaint = new()
    {
        ColorFilter = SKColorFilter.CreateColorMatrix([
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0.2126f, 0.7152f, 0.0722f, 0, 0,
            0,       0,       0,       1, 0
        ])
    };

    private readonly SKPaint _loupeBorderPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        Color = SKColors.Black,
        StrokeWidth = 3f * (float)Settings.DisplayDensity,
        IsAntialias = true
    };

    private readonly SKPaint _loupeCrosshairPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        Color = SKColors.Red,
        StrokeWidth = 1.5f * (float)Settings.DisplayDensity
    };

    private readonly SKPaint _loupeInnerShadowPaint = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    private readonly SKPaint _loupeGlarePaint = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

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

    public static readonly BindableProperty IsRotationLockedProperty =
        BindableProperty.Create(nameof(IsRotationLocked), typeof(bool), typeof(TileImageView), false,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (TileImageView)bindable;
                if ((bool)newValue)
                    control.CurrentRotation = 0f;
            });

    public static readonly BindableProperty IsGrayscaleEnabledProperty =
        BindableProperty.Create(nameof(IsGrayscaleEnabled), typeof(bool), typeof(TileImageView), false,
            propertyChanged: (bindable, oldValue, newValue) =>
            {
                var control = (TileImageView)bindable;
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

    public static readonly BindableProperty PinCreationModeProperty =
        BindableProperty.Create( nameof(PinCreationMode), typeof(PinCreationMode), typeof(TileImageView), PinCreationMode.LongPress);

    private static void OnPinsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TileImageView)bindable;

        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnPinsCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
            newCollection.CollectionChanged += control.OnPinsCollectionChanged;

        control.UpdateSortedPins();

        if (newValue is IEnumerable<MapPin> newPins)
            _ = control.PreloadPinBitmapsAsync(newPins);

        control._canvasView?.InvalidateSurface();
    }

    private static readonly BindablePropertyKey OriginalImageSizePropertyKey = BindableProperty.CreateReadOnly(nameof(OriginalImageSize), typeof(SKSize), typeof(TileImageView), SKSize.Empty);
    private static readonly BindablePropertyKey CurrentScalePropertyKey = BindableProperty.CreateReadOnly(nameof(CurrentScale), typeof(float), typeof(TileImageView), 1.0f);
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
    public bool IsRotationLocked { get => (bool)GetValue(IsRotationLockedProperty); set => SetValue(IsRotationLockedProperty, value); }
    public PinCreationMode PinCreationMode { get => (PinCreationMode)GetValue(PinCreationModeProperty); set => SetValue(PinCreationModeProperty, value); }
    public bool IsGrayscaleEnabled { get => (bool)GetValue(IsGrayscaleEnabledProperty); set => SetValue(IsGrayscaleEnabledProperty, value); }

    public event EventHandler<MapPin> PinTapped;
    public event EventHandler<MapPin> PinMoved;
    public event EventHandler<MapPin> PinDoubleTapped;
    public event EventHandler<SKPoint> CanvasTapped;
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

        _loupeShadowPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(0, 0),
                _loupeRadius,
                [SKColors.Transparent, SKColors.Black.WithAlpha(100)],
                null,
                SKShaderTileMode.Clamp)
        };

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
        float minScale = GetMinScale();
        float newScale = Math.Clamp(_scale * zoomFactor, minScale, 16.0f);

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

        return GetRelativeFactorFromScreenPoint(new SKPoint(centerX, centerY), clamp: true);
    }

    public Point GetRelativeFactorFromScreenPoint(SKPoint screenPoint, bool clamp = false)
    {
        if (OriginalImageSize == SKSize.Empty)
            return new Point(0, 0);

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix))
            return new Point(0, 0);

        SKPoint imagePixel = inverseMatrix.MapPoint(screenPoint);

        double factorX = imagePixel.X / OriginalImageSize.Width;
        double factorY = imagePixel.Y / OriginalImageSize.Height;

        if (clamp)
        {
            factorX = Math.Clamp(factorX, 0.0, 1.0);
            factorY = Math.Clamp(factorY, 0.0, 1.0);
        }

        return new Point(factorX, factorY);
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
        if (IsRotationLocked || !_isRightMouseRotating || OriginalImageSize == SKSize.Empty || _isGenerating) return;

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

    private float GetMinScale()
    {
        if (OriginalImageSize.IsEmpty || _canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
            return 0.001f;

        // Skalierung, bei der das gesamte Bild komplett in den Canvas passt
        float fitScale = Math.Min(
            _canvasView.CanvasSize.Width / OriginalImageSize.Width,
            _canvasView.CanvasSize.Height / OriginalImageSize.Height
        );

        // Erlaube z. B. bis zu 50% von fitScale herauszuzoomen (maximal aber 0.1f)
        return Math.Min(0.1f, fitScale * 0.5f);
    }

    private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(PlaceholderColor.ToSKColor());

        DrawMapAndPins(canvas);

        // Wenn ein Pin gezogen wird, Lupe darüber zeichnen
        if (_draggedPin != null && SettingsService.Instance.IsLoupeEnabled)
            DrawMagnifyingGlass(canvas);
    }

    private void DrawMapAndPins(SKCanvas canvas)
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

        if (_isGenerating || string.IsNullOrEmpty(_computedTileFolder) || OriginalImageSize.IsEmpty) return;

        int currentZoom = MaxZoomLevel + (int)Math.Floor(Math.Log2(_scale));
        currentZoom = Math.Clamp(currentZoom, 0, MaxZoomLevel);

        canvas.Save();
        canvas.Translate(_panX, _panY);
        canvas.RotateDegrees(_rotationDegrees);
        canvas.Scale(_scale);

        float tileScaleFactor = (float)Math.Pow(2, MaxZoomLevel - currentZoom);
        float currentTileSizeInCanvasSpace = TileSize * tileScaleFactor;
        double zoomScale = Math.Pow(0.5, MaxZoomLevel - currentZoom);
        float levelWidth = OriginalImageSize.Width * (float)zoomScale;
        float levelHeight = OriginalImageSize.Height * (float)zoomScale;
        int maxTilesX = (int)Math.Ceiling(levelWidth / TileSize);
        int maxTilesY = (int)Math.Ceiling(levelHeight / TileSize);
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

        minX = Math.Clamp(minX, 0, Math.Max(0, maxTilesX - 1));
        minY = Math.Clamp(minY, 0, Math.Max(0, maxTilesY - 1));
        maxX = Math.Clamp(maxX, 0, Math.Max(0, maxTilesX - 1));
        maxY = Math.Clamp(maxY, 0, Math.Max(0, maxTilesY - 1));

        string zoomFolder = Path.Combine(_computedTileFolder, currentZoom.ToString());
        var paint = IsGrayscaleEnabled ? GrayscalePaint : null;

        if (Directory.Exists(zoomFolder))
        {
            for (int x = minX; x <= maxX; x++)
            {
                string xFolder = Path.Combine(zoomFolder, x.ToString());
                if (!Directory.Exists(xFolder)) continue;

                for (int y = minY; y <= maxY; y++)
                {
                    var cacheKey = new TileKey(currentZoom, x, y);
                    float posX = x * currentTileSizeInCanvasSpace;
                    float posY = y * currentTileSizeInCanvasSpace;
                    var destRect = new SKRect(posX, posY, posX + currentTileSizeInCanvasSpace, posY + currentTileSizeInCanvasSpace);

                    if (!_tileCache.TryGetValue(cacheKey, out var bitmap))
                    {
                        if (_loadingTiles.Add(cacheKey))
                        {
                            string tilePath = Path.Combine(xFolder, $"{y}.jpg");

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

                        // Fallback-Logik fuer noch nicht geladene Kacheln
                        int fallbackZoom = currentZoom - 1;
                        int fallbackX = x / 2;
                        int fallbackY = y / 2;
                        int deltaZoom = 1;

                        while (fallbackZoom >= 0)
                        {
                            var fallbackKey = new TileKey(fallbackZoom, fallbackX, fallbackY);

                            if (_tileCache.TryGetValue(fallbackKey, out var fallbackBitmap))
                            {
                                int factor = 1 << deltaZoom;

                                float srcWidth = (float)TileSize / factor;
                                float srcHeight = (float)TileSize / factor;

                                float srcX = (x % factor) * srcWidth;
                                float srcY = (y % factor) * srcHeight;

                                var srcRect = new SKRect(srcX, srcY, srcX + srcWidth, srcY + srcHeight);
                                canvas.DrawBitmap(fallbackBitmap, srcRect, destRect, LinearSampling, paint);
                                break;
                            }

                            fallbackZoom--;
                            fallbackX /= 2;
                            fallbackY /= 2;
                            deltaZoom++;
                        }
                    }
                    else
                        canvas.DrawBitmap(bitmap, destRect, LinearSampling, paint);
                }
            }
        }

        // Pins zeichnenLinearSampling
        if (Pins != null && OriginalImageSize != SKSize.Empty)
        {
            float padding = 50f;
            float l = viewLeft - padding;
            float r = viewRight + padding;
            float t = viewTop - padding;
            float b = viewBottom + padding;

            foreach (var pin in _sortedPins)
            {
                SKBitmap pinBitmap = pin.Icon ??= GetOrLoadPinBitmap(pin);
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

                canvas.DrawBitmap(pinBitmap, left, top, LinearSampling);
                canvas.Restore();
            }
        }
        canvas.Restore();
    }

    private void DrawMagnifyingGlass(SKCanvas canvas)
    {
        if (_draggedPin == null) return;

        float currentLoupeRadius = SettingsService.Instance.LoupeRadius * (float)Settings.DisplayDensity;

        // Shader und Pfade nur neu berechnen, wenn sich der Radius geändert hat
        if (Math.Abs(_cachedLoupeRadius - currentLoupeRadius) > 0.1f)
            UpdateLoupeCache(currentLoupeRadius);

        SKMatrix mapMatrix = SKMatrix.CreateTranslation(_panX, _panY);
        mapMatrix = mapMatrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        mapMatrix = mapMatrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        float pinAbsX = _draggedPin.RelativeX * OriginalImageSize.Width;
        float pinAbsY = _draggedPin.RelativeY * OriginalImageSize.Height;
        SKPoint pinScreenPos = mapMatrix.MapPoint(pinAbsX, pinAbsY);

        float zoomFactor = SettingsService.Instance.LoupeZoomFactor;
        float margin = 30f;
        float loupeCenterX = _cachedLoupeRadius + margin;
        float loupeCenterY = _cachedLoupeRadius + margin;

        canvas.Save();
        canvas.Translate(loupeCenterX, loupeCenterY);
        canvas.ClipPath(_cachedLoupePath, SKClipOperation.Intersect, true);
        canvas.Save();
        canvas.Scale(zoomFactor);
        canvas.Translate(-pinScreenPos.X, -pinScreenPos.Y);
        DrawMapAndPins(canvas);
        canvas.Restore();
        canvas.DrawCircle(0, 0, _cachedLoupeRadius, _loupeInnerShadowPaint);
        canvas.Save();

        float glareRadiusX = _cachedLoupeRadius * 0.85f;
        float glareRadiusY = _cachedLoupeRadius * 0.45f;
        float glareOffsetX = -(_cachedLoupeRadius * 0.15f);
        float glareOffsetY = -(_cachedLoupeRadius * 0.35f);

        canvas.RotateDegrees(-25f, glareOffsetX, glareOffsetY);
        canvas.DrawOval(glareOffsetX, glareOffsetY, glareRadiusX, glareRadiusY, _loupeGlarePaint);
        canvas.Restore();
        canvas.DrawCircle(0, 0, _cachedLoupeRadius, _loupeBorderPaint);

        float crosshairHalfSize = 15 * (float)Settings.DisplayDensity;
        canvas.DrawLine(-crosshairHalfSize, 0, crosshairHalfSize, 0, _loupeCrosshairPaint);
        canvas.DrawLine(0, -crosshairHalfSize, 0, crosshairHalfSize, _loupeCrosshairPaint);
        canvas.Restore();
    }

    private void UpdateLoupeCache(float newRadius)
    {
        _cachedLoupeRadius = newRadius;
        _cachedLoupePath?.Dispose();
        _cachedInnerShadowShader?.Dispose();
        _cachedGlareShader?.Dispose();

        var pathBuilder = new SKPathBuilder();
        pathBuilder.AddCircle(0, 0, newRadius);
        _cachedLoupePath = pathBuilder.Detach();

        var shadowColors = new SKColor[] { SKColors.Transparent, SKColors.Transparent, new(0, 0, 0, 130) };
        var shadowPositions = new float[] { 0f, 0.6f, 1f };
        _cachedInnerShadowShader = SKShader.CreateRadialGradient(
            new SKPoint(0, 0), // Zentriert auf 0,0
            newRadius,
            shadowColors,
            shadowPositions,
            SKShaderTileMode.Clamp);

        _loupeInnerShadowPaint.Shader = _cachedInnerShadowShader;

        float glareRadiusY = newRadius * 0.45f;
        float glareOffsetX = -(newRadius * 0.15f);
        float glareOffsetY = -(newRadius * 0.35f);

        _cachedGlareShader = SKShader.CreateLinearGradient(
            new SKPoint(glareOffsetX, glareOffsetY - glareRadiusY),
            new SKPoint(glareOffsetX, glareOffsetY + glareRadiusY),
            [new SKColor(255, 255, 255, 140), new SKColor(255, 255, 255, 0)],
            [0f, 1f],
            SKShaderTileMode.Clamp);

        _loupeGlarePaint.Shader = _cachedGlareShader;
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
                    _touchStartTime = DateTime.UtcNow;
                    _hasDraggedPin = false;
                    _isDoubleTapAction = false;

                    _draggedPin = GetPinAtPosition(e.Location);

                    _lastTouchPoint = e.Location;

                    if (_draggedPin != null && _draggedPin.IsLockPosition)
                        _draggedPin = null;

                    if (_draggedPin != null)
                    {
                        _originalPinX = _draggedPin.RelativeX;
                        _originalPinY = _draggedPin.RelativeY;

                        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
                        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
                        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));
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
                    var keys = _activeTouches.Keys.OrderBy(k => k).ToArray();
                    var p0 = _activeTouches[keys[0]];
                    var p1 = _activeTouches[keys[1]];
                    _oldFingerDistance = SKPoint.Distance(p0, p1);
                    _oldFingerAngle = (float)Math.Atan2(p1.Y - p0.Y, p1.X - p0.X);
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
                    if (SKPoint.Distance(_touchStartPoint, e.Location) > ClickThreshold)
                        _hasDraggedPin = true;
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
                    var keys = _activeTouches.Keys.OrderBy(k => k).ToArray();
                    var oldP0 = _activeTouches[keys[0]];
                    var oldP1 = _activeTouches[keys[1]];
                    float oldCenterX = (oldP0.X + oldP1.X) / 2f;
                    float oldCenterY = (oldP0.Y + oldP1.Y) / 2f;

                    _activeTouches[e.Id] = e.Location;

                    var newP0 = _activeTouches[keys[0]];
                    var newP1 = _activeTouches[keys[1]];
                    float newCenterX = (newP0.X + newP1.X) / 2f;
                    float newCenterY = (newP0.Y + newP1.Y) / 2f;

                    _panX += (newCenterX - oldCenterX);
                    _panY += (newCenterY - oldCenterY);

                    float newDistance = SKPoint.Distance(newP0, newP1);
                    if (_oldFingerDistance > 0)
                    {
                        float scaleFactor = newDistance / _oldFingerDistance;
                        float minScale = GetMinScale();
                        float newScale = Math.Clamp(_scale * scaleFactor, minScale, 16.0f);
                        float scaleRatio = newScale / _scale;
                        _panX = newCenterX - (newCenterX - _panX) * scaleRatio;
                        _panY = newCenterY - (newCenterY - _panY) * scaleRatio;
                        _scale = newScale;
                    }
                    _oldFingerDistance = newDistance;

                    float newAngle = (float)Math.Atan2(newP1.Y - newP0.Y, newP1.X - newP0.X);
                    if (!IsRotationLocked && _oldFingerAngle != 0f)
                    {
                        float angleDiff = newAngle - _oldFingerAngle;

                        if (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
                        if (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

                        float rotationDiffDegrees = angleDiff * (180f / (float)Math.PI);
                        _rotationDegrees += rotationDiffDegrees;

                        double rad = angleDiff;
                        float cos = (float)Math.Cos(rad);
                        float sin = (float)Math.Sin(rad);
                        float dx = _panX - newCenterX;
                        float dy = _panY - newCenterY;

                        _panX = newCenterX + (dx * cos - dy * sin);
                        _panY = newCenterY + (dx * sin + dy * cos);
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

                bool isInsideThreshold = SKPoint.Distance(_touchStartPoint, e.Location) < ClickThreshold;
                bool isQuickTap = (DateTime.UtcNow - _touchStartTime).TotalMilliseconds < 300;
                bool isTap = isInsideThreshold && !_hasDraggedPin && isQuickTap;

                if (_activeTouches.Count == 1 && isTap)
                {
                    var now = DateTime.UtcNow;
                    double elapsed = (now - _lastTapTime).TotalMilliseconds;
                    float distance = SKPoint.Distance(_lastTapLocation, e.Location);

                    var currentPin = GetPinAtPosition(e.Location);

                    if (elapsed < DoubleTapTimeoutMs && distance < DoubleTapDistanceThreshold)
                    {
                        // Doppelklick-Handling zurücksetzen
                        _isDoubleTapAction = true;
                        _tapCts?.Cancel();
                        _tapCts = null;
                        _lastTapTime = DateTime.MinValue;

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
                        _tapCts?.Cancel();
                        _tapCts = new CancellationTokenSource();
                        var token = _tapCts.Token;
                        var tapLocation = e.Location;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(250, token);

                                if (!token.IsCancellationRequested)
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        if (currentPin != null)
                                            PinTapped?.Invoke(this, currentPin);
                                        else
                                        {
                                            CanvasTapped?.Invoke(this, tapLocation);
                                            if (PinCreationMode == PinCreationMode.SingleTap)
                                                CanvasLongPressed?.Invoke(this, tapLocation);
                                        }
                                    });
                                }
                            }
                            catch (OperationCanceledException) { }
                        });
                    }
                }
                else if (_draggedPin != null)
                {
                    PinMoved?.Invoke(this, _draggedPin);
                }

                if (_draggedPin != null)
                {
                    if (isTap)
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
        if (e.NewItems != null)
            _ = PreloadPinBitmapsAsync(e.NewItems.OfType<MapPin>());

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

            bool tilesExist = Directory.Exists(_computedTileFolder) &&
                              Directory.GetFiles(_computedTileFolder, "*.jpg", SearchOption.AllDirectories).Length > 0;

            if (!tilesExist)
            {
                _loadingIndicator.IsVisible = true;
                _loadingIndicator.IsRunning = true;

                await Task.Run(() => GenerateTilePyramidInternal(
                    imagePath,
                    _computedTileFolder,
                    MaxZoomLevel,
                    TileSize,
                    PlaceholderColor.ToSKColor(),
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

    private static void GenerateTilePyramidInternal(
        string sourceImagePath,
        string outputFolder,
        int maxZoomLevels,
        int tileSize,
        SKColor tileBackgroundColor,
        CancellationToken token,
        Action onLevelGenerated = null)
    {
        using var codec = SKCodec.Create(sourceImagePath);
        if (codec == null) return;

        using var originalBitmap = SKBitmap.Decode(codec);
        if (originalBitmap == null) return;

        int origWidth = originalBitmap.Width;
        int origHeight = originalBitmap.Height;

        // Parallelitaet drosseln, um Windows-Dateisystem-Engpaesse zu vermeiden
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        // Von grob (0) bis detailliert (maxZoomLevels)
        for (int zoom = 0; zoom <= maxZoomLevels; zoom++)
        {
            token.ThrowIfCancellationRequested();

            double scale = Math.Pow(0.5, maxZoomLevels - zoom);
            int levelWidth = Math.Max(1, (int)(origWidth * scale));
            int levelHeight = Math.Max(1, (int)(origHeight * scale));

            using var scaledBitmap = (zoom == maxZoomLevels)
                ? null
                : originalBitmap.Resize(new SKImageInfo(levelWidth, levelHeight), LinearSampling);

            SKBitmap currentLevelBitmap = scaledBitmap ?? originalBitmap;

            int tilesX = (int)Math.Ceiling((double)currentLevelBitmap.Width / tileSize);
            int tilesY = (int)Math.Ceiling((double)currentLevelBitmap.Height / tileSize);

            string zoomFolder = Path.Combine(outputFolder, zoom.ToString());

            for (int x = 0; x < tilesX; x++)
                Directory.CreateDirectory(Path.Combine(zoomFolder, x.ToString()));

            Parallel.For(0, tilesX, parallelOptions, x =>
            {
                string xFolder = Path.Combine(zoomFolder, x.ToString());

                for (int y = 0; y < tilesY; y++)
                {
                    token.ThrowIfCancellationRequested();

                    string tilePath = Path.Combine(xFolder, $"{y}.jpg");
                    if (File.Exists(tilePath)) continue;

                    int srcX = x * tileSize;
                    int srcY = y * tileSize;
                    int width = Math.Min(tileSize, currentLevelBitmap.Width - srcX);
                    int height = Math.Min(tileSize, currentLevelBitmap.Height - srcY);

                    if (width <= 0 || height <= 0) continue;

                    var srcRectI = new SKRectI(srcX, srcY, srcX + width, srcY + height);

                    using var subsetBitmap = new SKBitmap();
                    if (currentLevelBitmap.ExtractSubset(subsetBitmap, srcRectI))
                    {
                        SKBitmap tileToSave = subsetBitmap;
                        bool needsDispose = false;

                        if (width < tileSize || height < tileSize)
                        {
                            tileToSave = new SKBitmap(tileSize, tileSize);
                            using (var canvas = new SKCanvas(tileToSave))
                            {
                                canvas.Clear(tileBackgroundColor);
                                canvas.DrawBitmap(subsetBitmap, 0, 0, LinearSampling);
                            }
                            needsDispose = true;
                        }

                        try
                        {
                            using var image = SKImage.FromBitmap(tileToSave);
                            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
                            using var stream = File.Create(tilePath);
                            data.SaveTo(stream);
                        }
                        finally
                        {
                            if (needsDispose)
                                tileToSave.Dispose();
                        }
                    }
                }
            });

            // Event ausloesen, damit die UI die neuen Kacheln sofort anzeigen kann
            onLevelGenerated?.Invoke();
        }
    }

    private SKBitmap GetOrLoadPinBitmap(MapPin pin)
    {
        if (string.IsNullOrEmpty(pin.IconPath)) return null;

        // Fast-Path: Liegt es im RAM-Cache?
        if (_pinIconCache.TryGetValue(pin.IconPath, out var cachedBitmap))
            return cachedBitmap;

        // Noch nicht da? Asynchrones Laden für diesen Pin anstoßen
        _ = PreloadPinBitmapsAsync([pin]);

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
        double maxLimit = SettingsService.Instance.PinMaxScaleLimit / 100.0;
        double minLimit = SettingsService.Instance.PinMinScaleLimit / 100.0;

        if (dynamicScale > maxLimit) dynamicScale = maxLimit;
        if (dynamicScale < minLimit) dynamicScale = minLimit;

        return (float)(SettingsService.Instance.OsBaseScale * dynamicScale * pin.PinScale);
    }

    private void UpdateDraggedPinPosition(SKPoint touchPoint)
    {
        if (_draggedPin == null || OriginalImageSize == SKSize.Empty) return;

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix)) return;

        // Berechne die Verschiebung des Fingers im Koordinatensystem des Bildes
        SKPoint currentPlanPoint = inverseMatrix.MapPoint(touchPoint);
        SKPoint previousPlanPoint = inverseMatrix.MapPoint(_lastTouchPoint);

        float deltaX = currentPlanPoint.X - previousPlanPoint.X;
        float deltaY = currentPlanPoint.Y - previousPlanPoint.Y;

        // Das Delta direkt auf die aktuellen relativen Koordinaten aufaddieren
        float newRelX = _draggedPin.RelativeX + (deltaX / OriginalImageSize.Width);
        float newRelY = _draggedPin.RelativeY + (deltaY / OriginalImageSize.Height);

        _draggedPin.RelativeX = Math.Clamp(newRelX, 0f, 1f);
        _draggedPin.RelativeY = Math.Clamp(newRelY, 0f, 1f);

        // Aktuellen Punkt für das nächste Moved-Event merken
        _lastTouchPoint = touchPoint;
    }

    private async Task PreloadPinBitmapsAsync(IEnumerable<MapPin> pins)
    {
        if (pins == null) return;

        // Fehlende Pfade ermitteln
        var missingPaths = pins
            .Select(p => p.IconPath)
            .Where(path => !string.IsNullOrEmpty(path)
                           && !_pinIconCache.ContainsKey(path)
                           && !_loadingPinPaths.Contains(path))
            .Distinct()
            .ToList();

        if (missingPaths.Count == 0) return;

        // Als "wird geladen" markieren (UI-Thread)
        foreach (var path in missingPaths)
        {
            _loadingPinPaths.Add(path);
        }

        try
        {
            // E/A-Zugriff und Dekodierung im Hintergrund-Thread ausführen
            await Task.Run(() =>
            {
                foreach (var path in missingPaths)
                {
                    SKBitmap bitmap = null;
                    try
                    {
                        bitmap = LoadPinBitmapInternal(path);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Fehler beim Laden des Pin-Icons '{path}': {ex.Message}");
                    }

                    // Ergebnisse thread-sicher auf dem UI-Thread anwenden & aufräumen
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _loadingPinPaths.Remove(path);

                        if (bitmap != null)
                        {
                            _pinIconCache[path] = bitmap;
                            _canvasView?.InvalidateSurface();
                        }
                    });
                }
            });
        }
        catch
        {
            // Sicherheitsnetz: Falls Task.Run fehlschlägt, UI-Thread wieder freigeben
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var path in missingPaths)
                {
                    _loadingPinPaths.Remove(path);
                }
            });
        }
    }

    private static SKBitmap LoadPinBitmapInternal(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        if (File.Exists(iconPath))
        {
            try
            {
                using var stream = File.OpenRead(iconPath);
                return SKBitmap.Decode(stream);
            }
            catch { /* Ignorieren */ }
        }

        string cacheFolder = Settings.CacheDirectory;
        if (!Directory.Exists(cacheFolder))
            Directory.CreateDirectory(cacheFolder);

        string fileName = Path.GetFileName(iconPath);
        string targetCachePath = Path.Combine(cacheFolder, fileName);

        if (File.Exists(targetCachePath))
        {
            try
            {
                using var stream = File.OpenRead(targetCachePath);
                return SKBitmap.Decode(stream);
            }
            catch { /* Ignorieren */ }
        }

        try
        {
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
                return SKBitmap.Decode(readStream);
            }
#elif IOS
        string imageName = Path.GetFileNameWithoutExtension(fileName);
        using var uiImage = UIKit.UIImage.FromBundle(imageName);
        if (uiImage != null)
        {
            using var nsData = uiImage.AsPNG();
            if (nsData != null)
            {
                using var stream = nsData.AsStream();
                using var targetStream = File.Create(targetCachePath);
                stream.CopyTo(targetStream);
                targetStream.Close();

                using var readStream = File.OpenRead(targetCachePath);
                return SKBitmap.Decode(readStream);
            }
        }
#elif WINDOWS
        string fileNameOnly = Path.GetFileName(iconPath);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileNameOnly);
        string ext = Path.GetExtension(fileNameOnly);
        string baseDir = AppContext.BaseDirectory;

            string[] searchDirs = [
                Path.Combine(baseDir, "Assets", "pins"),
                Path.Combine(baseDir, "Assets"),
                baseDir
                ];

            foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            // 1. Suche nach exaktem Dateinamen
            string targetPath = Path.Combine(dir, fileNameOnly);

            // 2. Direkter Scale-100 Check (ohne Directory.GetFiles Scan)
            if (!File.Exists(targetPath))
            {
                targetPath = Path.Combine(dir, $"{nameWithoutExt}.scale-100{ext}");
            }

            // 3. Fallback: Nur falls scale-100 nicht existiert, das Verzeichnis durchsuchen
            if (!File.Exists(targetPath))
            {
                var matches = Directory.GetFiles(dir, $"{nameWithoutExt}.scale-*{ext}");
                targetPath = matches.FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                try
                {
                    using var stream = File.OpenRead(targetPath);
                    return SKBitmap.Decode(stream);
                }
                catch { /* Ignorieren */ }
            }
        }
#else
        using var stream = FileSystem.OpenAppPackageFileAsync(iconPath).GetAwaiter().GetResult();
        using var targetStream = File.Create(targetCachePath);
        stream.CopyTo(targetStream);
        targetStream.Close();

        using var readStream = File.OpenRead(targetCachePath);
        return SKBitmap.Decode(readStream);
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Extrahieren des Pins: {ex.Message}");
        }

        return null;
    }

    private void ClearCache()
    {
        _tileCache.Clear();
        _loadingTiles.Clear();

        foreach (var bitmap in _pinIconCache.Values)
            bitmap?.Dispose();

        _pinIconCache.Clear();
    }

    public void ResetTouchState()
    {
        _activeTouches.Clear();
        _draggedPin = null;
        _isLongPressActive = false;
        _isDoubleTapAction = false;
        _oldFingerDistance = 0f;
        _oldFingerAngle = 0f;
        _longPressCts?.Cancel();
        _tapCts?.Cancel();
        _canvasView?.InvalidateSurface();
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

public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache = [];
    private readonly LinkedList<CacheEntry> _list = [];
    private readonly int _capacity;

    private readonly record struct CacheEntry(TKey Key, TValue Value);

    public LruCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"Der Schluessel '{key}' wurde nicht gefunden.");
        set => Add(key, value);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                _list.Remove(node);
                _list.AddFirst(node);
                return true;
            }

            value = default!;
            return false;
        }
    }

    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _list.Remove(existingNode);
                _cache.Remove(key);
            }
            else if (_cache.Count >= _capacity && _list.Last is not null)
            {
                var lastNode = _list.Last;
                _cache.Remove(lastNode.Value.Key);
                _list.RemoveLast();
            }

            var entry = new CacheEntry(key, value);
            var newNode = _list.AddFirst(entry);
            _cache[key] = newNode;
        }
    }

    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (!_cache.Remove(key, out var node))
            {
                return false;
            }

            _list.Remove(node);
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _list.Clear();
        }
    }
}

public enum PinCreationMode
{
    LongPress,
    SingleTap
}

public readonly record struct TileKey(int Zoom, int X, int Y);
