#nullable disable
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using System.Collections.Specialized;
using System.Diagnostics;

namespace SnapDoc.Controls;

public partial class TileImageView : ContentView, IDisposable
{
    // ---------------------------------------------------------------------------------
    //  Konstanten
    // ---------------------------------------------------------------------------------
    private const float ClickThreshold = 15f;
    private const float DoubleTapDistanceThreshold = 40f;
    private const int DoubleTapTimeoutMs = 300;
    private const int LongPressTimeoutMs = 600;
    private const int MaxConcurrentTileLoads = 4;
    private const int MaxQueuedTileRequests = 96;
    private const int PermanentTileBudget = 64;
    private const int RenderCoalesceMs = 8;
    private const string PyramidCompleteMarker = ".complete";

    // ---------------------------------------------------------------------------------
    //  Felder - Ansicht
    // ---------------------------------------------------------------------------------
    private readonly SKGLView _canvasView;
    private readonly ActivityIndicator _loadingIndicator;
    private readonly Grid _layoutGrid;
    private float _scale = 1.0f;
    private float _panX = 0f;
    private float _panY = 0f;
    private float _rotationDegrees = 0f;
    private bool _disposed = false;
    private string _computedTileFolder = string.Empty;
    private bool _showFps = false;
    private readonly double[] _frameTimes = new double[60];
    private int _frameTimeIndex = 0;
    private long _lastFrameTicks = 0;
    private float _fpsValue = 0f;
    private SKFont _fpsFont;
    private SKPaint _fpsTextPaint;
    private SKPaint _fpsBackPaint;
    private int _baseFallbackLevel = 0;

    // ---------------------------------------------------------------------------------
    //  Felder - Kachel-Cache und Loader
    // ---------------------------------------------------------------------------------
    private readonly LruCache<TileKey, SKBitmap> _tileCache = new(SettingsService.Instance.MaxTileCache);
    private readonly Dictionary<TileKey, SKBitmap> _permanentTiles = [];
    private readonly HashSet<TileKey> _pendingTiles = [];
    private readonly List<TileRequest> _tileQueue = [];
    private readonly SemaphoreSlim _tileLoadSemaphore = new(MaxConcurrentTileLoads, MaxConcurrentTileLoads);
    private int _activeTileLoads = 0;
    private int _renderGeneration = 0;
    private int _maxGeneratedLevel = -1;
    private int _displayZoom = -1;
    private bool[] _isPermanentLevel = [];

    // ---------------------------------------------------------------------------------
    //  Felder - Rendering-Steuerung
    // ---------------------------------------------------------------------------------
    private bool _renderPending = false;
    private SKColor _placeholderSKColor = Colors.LightGray.ToSKColor();
    private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    // ---------------------------------------------------------------------------------
    //  Felder - Eingabe
    // ---------------------------------------------------------------------------------
    private readonly Dictionary<long, SKPoint> _activeTouches = [];
    private float _oldFingerDistance = 0f;
    private float _oldFingerAngle = 0f;
    private SKPoint _touchStartPoint;
    private SKPoint _lastTouchPoint;
    private DateTime _touchStartTime;
    private bool _hasDraggedPin = false;
    private MapPin _draggedPin = null;
    private float _originalPinX;
    private float _originalPinY;
    private string _pendingPinId = null;
    private double? _pendingZoomFactor = null;
    private bool _pendingImageFit = false;
    private CancellationTokenSource _cts;
    private CancellationTokenSource _longPressCts;
    private CancellationTokenSource _tapCts;
    private DateTime _lastTapTime = DateTime.MinValue;
    private SKPoint _lastTapLocation = SKPoint.Empty;
    private bool _isDoubleTapAction = false;
    private bool _isLongPressActive = false;

    // ---------------------------------------------------------------------------------
    //  Felder - Pins
    // ---------------------------------------------------------------------------------
    private readonly Dictionary<string, SKBitmap> _pinIconCache = [];
    private readonly HashSet<string> _loadingPinPaths = [];
    private List<MapPin> _sortedPins = [];
    private bool _pinsNeedSort = false;
    private INotifyCollectionChanged _observedPins = null;

    // ---------------------------------------------------------------------------------
    //  Felder - Lupe
    // ---------------------------------------------------------------------------------
    private readonly SKPaint _loupeShadowPaint;
    private readonly float _loupeRadius = 150f;
    private float _cachedLoupeRadius = -1f;
    private SKPath _cachedLoupePath;
    private SKShader _cachedInnerShadowShader;
    private SKShader _cachedGlareShader;

    private static readonly SKPaint GrayscalePaint = new()
    {
        ColorFilter = SKColorFilter.CreateColorMatrix(
        [
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
    private Microsoft.UI.Xaml.UIElement _winView = null;
    private bool _isRightMouseRotating = false;
    private float _lastMouseRotationAngle = 0f;
#endif

    // ---------------------------------------------------------------------------------
    //  BindableProperties
    // ---------------------------------------------------------------------------------
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
                    control.InvalidateView();
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
            propertyChanged: (bindable, oldValue, newValue) => ((TileImageView)bindable).RequestRender());

    public static readonly BindableProperty CurrentRotationProperty =
        BindableProperty.Create(nameof(CurrentRotation), typeof(float), typeof(TileImageView), 0f,
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnCurrentRotationChanged);

    public static readonly BindableProperty MaxZoomLevelProperty =
        BindableProperty.Create(nameof(MaxZoomLevel), typeof(int), typeof(TileImageView), SettingsService.Instance.MaxZoomLevel,
            propertyChanged: (bindable, o, n) =>
            {
                var control = (TileImageView)bindable;
                control.RebuildLevelMetadata();
                control.InvalidateView();
            });

    public static readonly BindableProperty PinsProperty =
        BindableProperty.Create(nameof(Pins), typeof(IEnumerable<MapPin>), typeof(TileImageView), default(IEnumerable<MapPin>),
            propertyChanged: OnPinsChanged);

    public static readonly BindableProperty PlaceholderColorProperty =
        BindableProperty.Create(nameof(PlaceholderColor), typeof(Color), typeof(TileImageView), Colors.LightGray,
            propertyChanged: (bindable, o, n) =>
            {
                var control = (TileImageView)bindable;
                control._placeholderSKColor = ((Color)n ?? Colors.LightGray).ToSKColor();
                control.RequestRender();
            });

    public static readonly BindableProperty ShowFpsCounterProperty =
        BindableProperty.Create(nameof(ShowFpsCounter), typeof(bool), typeof(TileImageView), false,
        propertyChanged: (bindable, o, n) =>
        {
            var control = (TileImageView)bindable;
            control._showFps = (bool)n;
            control._lastFrameTicks = 0;
            Array.Clear(control._frameTimes);
            control.InvalidateView();
        });

    public bool ShowFpsCounter
    {
        get => (bool)GetValue(ShowFpsCounterProperty);
        set => SetValue(ShowFpsCounterProperty, value);
    }

    public static readonly BindableProperty PinCreationModeProperty =
        BindableProperty.Create(nameof(PinCreationMode), typeof(PinCreationMode), typeof(TileImageView), PinCreationMode.LongPress);

    private static readonly BindablePropertyKey OriginalImageSizePropertyKey =
        BindableProperty.CreateReadOnly(nameof(OriginalImageSize), typeof(SKSize), typeof(TileImageView), SKSize.Empty);

    private static readonly BindablePropertyKey CurrentScalePropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentScale), typeof(float), typeof(TileImageView), 1.0f);

    private static readonly BindablePropertyKey CurrentPanPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentPan), typeof(SKPoint), typeof(TileImageView), SKPoint.Empty);

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

    // ---------------------------------------------------------------------------------
    //  Konstruktor
    // ---------------------------------------------------------------------------------
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
        Loaded += OnLoadedWindows;
        Unloaded += OnUnloadedWindows;
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

        RebuildLevelMetadata();
    }

    // ---------------------------------------------------------------------------------
    //  Render-Anforderungen zusammenfassen
    // ---------------------------------------------------------------------------------
    private void RequestRender()
    {
        if (_disposed || _renderPending || _canvasView == null) return;

        _renderPending = true;
        _canvasView.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(RenderCoalesceMs), () =>
        {
            _renderPending = false;
            if (!_disposed)
                _canvasView?.InvalidateSurface();
        });
    }

    private void InvalidateView()
    {
        unchecked { _renderGeneration++; }
        RequestRender();
    }

    public void InvalidateSurface() => _canvasView?.InvalidateSurface();

    // ---------------------------------------------------------------------------------
    //  Pyramiden-Metadaten
    // ---------------------------------------------------------------------------------
    private void RebuildLevelMetadata()
    {
        int maxZoom = MaxZoomLevel;
        _isPermanentLevel = new bool[Math.Max(1, maxZoom + 1)];

        var size = OriginalImageSize;
        if (size.IsEmpty) return;

        int tileSize = Math.Max(1, TileSize);

        for (int zoom = 0; zoom <= maxZoom; zoom++)
        {
            GetLevelTileCounts(zoom, maxZoom, tileSize, size, out int tilesX, out int tilesY);
            _isPermanentLevel[zoom] = (long)tilesX * tilesY <= PermanentTileBudget;
        }

        _baseFallbackLevel = 0;
        for (int z = 0; z < _isPermanentLevel.Length; z++)
            if (_isPermanentLevel[z]) _baseFallbackLevel = z;
    }

    private static void GetLevelTileCounts(int zoom, int maxZoom, int tileSize, SKSize imageSize, out int tilesX, out int tilesY)
    {
        double zoomScale = 1.0 / (1 << (maxZoom - zoom));

        int levelWidth = Math.Max(1, (int)(imageSize.Width * zoomScale));
        int levelHeight = Math.Max(1, (int)(imageSize.Height * zoomScale));

        tilesX = Math.Max(1, (int)Math.Ceiling(levelWidth / (double)tileSize));
        tilesY = Math.Max(1, (int)Math.Ceiling(levelHeight / (double)tileSize));
    }

    private bool IsPermanentLevel(int zoom)
        => zoom >= 0 && zoom < _isPermanentLevel.Length && _isPermanentLevel[zoom];

    private bool HasValidViewport =>
        !OriginalImageSize.IsEmpty &&
        _canvasView.CanvasSize.Width > 0 &&
        _canvasView.CanvasSize.Height > 0;

    private bool TryGetTile(TileKey key, out SKBitmap bitmap)
    {
        if (_permanentTiles.TryGetValue(key, out bitmap))
            return true;

        return _tileCache.TryGetValue(key, out bitmap);
    }

    // ---------------------------------------------------------------------------------
    //  Tile-Loader mit Semaphore, LIFO und Generationszaehler
    // ---------------------------------------------------------------------------------
    private readonly record struct TileRequest(TileKey Key, string Path, int Generation);

    private void RequestTile(TileKey key, string path)
    {
        if (_disposed) return;
        if (!_pendingTiles.Add(key)) return;

        _tileQueue.Add(new TileRequest(key, path, _renderGeneration));

        while (_tileQueue.Count > MaxQueuedTileRequests)
        {
            var dropped = _tileQueue[0];
            _tileQueue.RemoveAt(0);
            _pendingTiles.Remove(dropped.Key);
        }

        PumpTileQueue();
    }

    private void PumpTileQueue()
    {
        while (!_disposed && _activeTileLoads < MaxConcurrentTileLoads && _tileQueue.Count > 0)
        {
            int last = _tileQueue.Count - 1;
            var request = _tileQueue[last];
            _tileQueue.RemoveAt(last);

            if (unchecked(_renderGeneration - request.Generation) > 8)
            {
                _pendingTiles.Remove(request.Key);
                continue;
            }

            _activeTileLoads++;
            StartTileLoad(request);
        }
    }

    private void StartTileLoad(TileRequest request)
    {
        _ = Task.Run(async () =>
        {
            SKBitmap decoded = null;

            try
            {
                await _tileLoadSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    using var stream = File.OpenRead(request.Path);
                    using var codec = SKCodec.Create(stream);

                    if (codec != null)
                    {
                        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                        SKColorType.Bgra8888, SKAlphaType.Opaque);
                        var bmp = new SKBitmap(info);

                        if (codec.GetPixels(info, bmp.GetPixels()) == SKCodecResult.Success)
                            decoded = bmp;
                        else
                            bmp.Dispose();
                    }
                }
                catch (FileNotFoundException) { /* Kachel (noch) nicht erzeugt */ }
                catch (DirectoryNotFoundException) { /* Level (noch) nicht erzeugt */ }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fehler beim Kachelladen: {ex.Message}");
                }
                finally
                {
                    _tileLoadSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler im Tile-Loader: {ex.Message}");
            }

            MainThread.BeginInvokeOnMainThread(() => OnTileLoaded(request, decoded));
        });
    }

    private void OnTileLoaded(TileRequest request, SKBitmap decoded)
    {
        _activeTileLoads = Math.Max(0, _activeTileLoads - 1);
        _pendingTiles.Remove(request.Key);

        if (_disposed)
        {
            decoded?.Dispose();
            return;
        }

        if (decoded != null)
        {
            if (IsPermanentLevel(request.Key.Zoom))
            {
                if (!_permanentTiles.TryAdd(request.Key, decoded))
                    decoded.Dispose();
            }
            else if (_tileCache.ContainsKey(request.Key))
                decoded.Dispose();
            else
                _tileCache[request.Key] = decoded;

            RequestRender();
        }

        PumpTileQueue();
    }

    // ---------------------------------------------------------------------------------
    //  Navigation
    // ---------------------------------------------------------------------------------
    public void ZoomToPin(string pinId, double? factor = null)
    {
        if (string.IsNullOrEmpty(pinId)) return;

        _pendingPinId = pinId;
        _pendingZoomFactor = factor;
        _pendingImageFit = false;

        if (!HasValidViewport || Pins == null) return;

        var pin = Pins.FirstOrDefault(p => p.Id == pinId);
        if (pin == null) return;

        _rotationDegrees = 0f;
        _scale = factor.HasValue
            ? Math.Clamp((float)factor.Value, GetMinScale(), 16.0f)
            : 1.0f;

        float pinAbsX = pin.RelativeX * OriginalImageSize.Width;
        float pinAbsY = pin.RelativeY * OriginalImageSize.Height;
        float canvasWidth = _canvasView.CanvasSize.Width;
        float canvasHeight = _canvasView.CanvasSize.Height;

        _panX = canvasWidth * 0.5f - pinAbsX * _scale;
        _panY = canvasHeight * 0.5f - pinAbsY * _scale;

        CurrentScale = _scale;
        CurrentPan = new SKPoint(_panX, _panY);
        CurrentRotation = _rotationDegrees;

        _pendingPinId = null;
        _pendingZoomFactor = null;

        InvalidateView();
    }

    public void HandleMouseZoom(SKPoint mouseLocation, float delta)
    {
        if (!HasValidViewport) return;

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

        InvalidateView();
    }

    public Point GetPlanFactorAtControlCenter()
    {
        if (OriginalImageSize == SKSize.Empty || _canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
            return new Point(0, 0);

        float centerX = _canvasView.CanvasSize.Width / 2f;
        float centerY = _canvasView.CanvasSize.Height / 2f;

        return GetRelativeFactorFromScreenPoint(new SKPoint(centerX, centerY), clamp: true);
    }

    public Point GetRelativeFactorFromScreenPoint(SKPoint screenPoint, bool clamp = false)
    {
        if (OriginalImageSize == SKSize.Empty)
            return new Point(0, 0);

        SKMatrix matrix = BuildMapMatrix();

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

        InvalidateView();
    }

    private SKMatrix BuildMapMatrix()
    {
        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));
        return matrix;
    }

    private float GetMinScale()
    {
        if (OriginalImageSize.IsEmpty || _canvasView.CanvasSize.Width <= 0 || _canvasView.CanvasSize.Height <= 0)
            return 0.001f;

        float fitScale = Math.Min(
            _canvasView.CanvasSize.Width / OriginalImageSize.Width,
            _canvasView.CanvasSize.Height / OriginalImageSize.Height
        );

        return Math.Min(0.1f, fitScale * 0.5f);
    }

    // ---------------------------------------------------------------------------------
    //  Rendering
    // ---------------------------------------------------------------------------------
    private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(_placeholderSKColor);

        if (_pinsNeedSort)
        {
            _pinsNeedSort = false;
            UpdateSortedPins();
        }

        if (HandlePendingViewportActions()) return;

        float canvasWidth = _canvasView.CanvasSize.Width;
        float canvasHeight = _canvasView.CanvasSize.Height;
        var fullClip = new SKRect(0, 0, canvasWidth, canvasHeight);

        DrawMapAndPins(canvas, fullClip, isPrimaryPass: true);

        if (_draggedPin != null && SettingsService.Instance.IsLoupeEnabled)
            DrawMagnifyingGlass(canvas);

        if (_showFps)
            DrawFpsCounter(canvas, canvasWidth);
    }

    private bool HandlePendingViewportActions()
    {
        if (!HasValidViewport) return false;

        if (_pendingImageFit)
        {
            _pendingImageFit = false;
            ImageFit();
            return true;
        }

        if (_pendingPinId != null)
        {
            string pinId = _pendingPinId;
            double? factor = _pendingZoomFactor;

            ZoomToPin(pinId, factor);
            return _pendingPinId == null;
        }

        return false;
    }

    private void DrawMapAndPins(SKCanvas canvas, SKRect deviceClip, bool isPrimaryPass)
    {
        if (string.IsNullOrEmpty(_computedTileFolder)) return;

        SKSize imageSize = OriginalImageSize;
        if (imageSize.IsEmpty) return;

        int maxZoom = MaxZoomLevel;
        int tileSize = Math.Max(1, TileSize);
        var paint = IsGrayscaleEnabled ? GrayscalePaint : null;

        canvas.Save();
        canvas.Translate(_panX, _panY);
        canvas.RotateDegrees(_rotationDegrees);
        canvas.Scale(_scale);

        SKMatrix total = canvas.TotalMatrix;
        if (!total.TryInvert(out SKMatrix inverse))
        {
            canvas.Restore();
            return;
        }

        SKRect view = MapRectBounds(inverse, deviceClip);

        float effScale = MathF.Sqrt(MathF.Abs(total.ScaleX * total.ScaleY - total.SkewX * total.SkewY));
        if (effScale <= 0f || float.IsNaN(effScale))
        {
            canvas.Restore();
            return;
        }

        int maxAvailableZoom = _maxGeneratedLevel < 0 ? -1 : Math.Min(maxZoom, _maxGeneratedLevel);
        if (maxAvailableZoom < 0)
        {
            canvas.Restore();
            return;
        }

        int desiredZoom = Math.Clamp(maxZoom + (int)Math.Ceiling(Math.Log2(effScale)), 0, maxAvailableZoom); //.Floor ist schneller aber unscharf
        int baseZoom;

        if (isPrimaryPass)
        {
            if (_displayZoom < 0 || desiredZoom <= _displayZoom || _displayZoom > maxAvailableZoom)
                _displayZoom = desiredZoom;

            baseZoom = Math.Clamp(_displayZoom, 0, maxAvailableZoom);
        }
        else
        {
            baseZoom = desiredZoom;
        }

        if (isPrimaryPass)
            EnsureTileCacheCapacity(baseZoom, desiredZoom, maxZoom, tileSize, imageSize, view);

        int guaranteed = Math.Min(_baseFallbackLevel, Math.Min(baseZoom, maxAvailableZoom));
        if (guaranteed < baseZoom)
            DrawTileLayer(canvas, guaranteed, maxZoom, tileSize, imageSize, view, paint, effScale, allowFallback: false);

        DrawTileLayer(canvas, baseZoom, maxZoom, tileSize, imageSize, view, paint, effScale, allowFallback: true);

        if (desiredZoom != baseZoom)
        {
            bool targetComplete = IsLayerComplete(desiredZoom, maxZoom, tileSize, imageSize, view);

            if (isPrimaryPass && targetComplete)
            {
                _displayZoom = desiredZoom;
                RequestRender();
            }
        }

        DrawPins(canvas, imageSize, view);

        canvas.Restore();
    }

    private bool IsLayerComplete(int zoom, int maxZoom, int tileSize, SKSize imageSize, SKRect view)
    {
        float tileSpan = tileSize * (1 << (maxZoom - zoom));
        GetLevelTileCounts(zoom, maxZoom, tileSize, imageSize, out int tilesX, out int tilesY);

        int minX = Math.Clamp((int)Math.Floor(view.Left / tileSpan), 0, tilesX - 1);
        int maxX = Math.Clamp((int)Math.Ceiling(view.Right / tileSpan), 0, tilesX - 1);
        int minY = Math.Clamp((int)Math.Floor(view.Top / tileSpan), 0, tilesY - 1);
        int maxY = Math.Clamp((int)Math.Ceiling(view.Bottom / tileSpan), 0, tilesY - 1);

        bool complete = true;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                var key = new TileKey(zoom, x, y);
                if (TryGetTile(key, out _)) continue;

                complete = false;

                if (!_pendingTiles.Contains(key))
                    RequestTile(key, Path.Combine(_computedTileFolder, zoom.ToString(), x.ToString(), $"{y}.jpg"));
            }
        }

        return complete;
    }

    private static SKRect MapRectBounds(SKMatrix matrix, SKRect rect)
    {
        SKPoint c0 = matrix.MapPoint(rect.Left, rect.Top);
        SKPoint c1 = matrix.MapPoint(rect.Right, rect.Top);
        SKPoint c2 = matrix.MapPoint(rect.Left, rect.Bottom);
        SKPoint c3 = matrix.MapPoint(rect.Right, rect.Bottom);

        float left = MathF.Min(MathF.Min(c0.X, c1.X), MathF.Min(c2.X, c3.X));
        float right = MathF.Max(MathF.Max(c0.X, c1.X), MathF.Max(c2.X, c3.X));
        float top = MathF.Min(MathF.Min(c0.Y, c1.Y), MathF.Min(c2.Y, c3.Y));
        float bottom = MathF.Max(MathF.Max(c0.Y, c1.Y), MathF.Max(c2.Y, c3.Y));

        return new SKRect(left, top, right, bottom);
    }

    private void EnsureTileCacheCapacity(int baseZoom, int desiredZoom, int maxZoom, int tileSize, SKSize imageSize, SKRect view)
    {
        int required = CountVisibleTiles(baseZoom, maxZoom, tileSize, imageSize, view);

        if (desiredZoom != baseZoom)
            required += CountVisibleTiles(desiredZoom, maxZoom, tileSize, imageSize, view);

        int capacity = Math.Max(SettingsService.Instance.MaxTileCache, required * 2 + 16);
        _tileCache.EnsureCapacity(capacity);
    }

    private static int CountVisibleTiles(int zoom, int maxZoom, int tileSize, SKSize imageSize, SKRect view)
    {
        float tileSpan = tileSize * (1 << (maxZoom - zoom));
        GetLevelTileCounts(zoom, maxZoom, tileSize, imageSize, out int tilesX, out int tilesY);

        int minX = Math.Clamp((int)Math.Floor(view.Left / tileSpan), 0, tilesX - 1);
        int maxX = Math.Clamp((int)Math.Ceiling(view.Right / tileSpan), 0, tilesX - 1);
        int minY = Math.Clamp((int)Math.Floor(view.Top / tileSpan), 0, tilesY - 1);
        int maxY = Math.Clamp((int)Math.Ceiling(view.Bottom / tileSpan), 0, tilesY - 1);

        return (maxX - minX + 1) * (maxY - minY + 1);
    }

    private bool DrawTileLayer(
        SKCanvas canvas,
        int zoom,
        int maxZoom,
        int tileSize,
        SKSize imageSize,
        SKRect view,
        SKPaint paint,
        float effScale,
        bool allowFallback)
    {
        float tileSpan = tileSize * (1 << (maxZoom - zoom));
        GetLevelTileCounts(zoom, maxZoom, tileSize, imageSize, out int tilesX, out int tilesY);

        int minX = Math.Clamp((int)Math.Floor(view.Left / tileSpan), 0, tilesX - 1);
        int maxX = Math.Clamp((int)Math.Ceiling(view.Right / tileSpan), 0, tilesX - 1);
        int minY = Math.Clamp((int)Math.Floor(view.Top / tileSpan), 0, tilesY - 1);
        int maxY = Math.Clamp((int)Math.Ceiling(view.Bottom / tileSpan), 0, tilesY - 1);

        float seam = 0.5f / effScale;

        bool complete = true;

        for (int x = minX; x <= maxX; x++)
        {
            float posX = x * tileSpan;

            for (int y = minY; y <= maxY; y++)
            {
                float posY = y * tileSpan;
                var destRect = new SKRect(posX, posY, posX + tileSpan + seam, posY + tileSpan + seam);
                var key = new TileKey(zoom, x, y);

                if (TryGetTile(key, out var bitmap))
                {
                    canvas.DrawBitmap(bitmap, destRect, LinearSampling, paint);
                    continue;
                }

                complete = false;

                if (!_pendingTiles.Contains(key))
                {
                    string tilePath = Path.Combine(
                        _computedTileFolder,
                        zoom.ToString(),
                        x.ToString(),
                        $"{y}.jpg");

                    RequestTile(key, tilePath);
                }

                if (!allowFallback) continue;

                DrawParentFallback(canvas, zoom, x, y, tileSize, destRect, paint);
            }
        }

        return complete;
    }

    private void DrawParentFallback(SKCanvas canvas, int zoom, int x, int y, int tileSize, SKRect destRect, SKPaint paint)
    {
        int fallbackZoom = zoom - 1;
        int fallbackX = x / 2;
        int fallbackY = y / 2;
        int deltaZoom = 1;

        while (fallbackZoom >= 0)
        {
            var fallbackKey = new TileKey(fallbackZoom, fallbackX, fallbackY);

            if (TryGetTile(fallbackKey, out var fallbackBitmap))
            {
                int factor = 1 << deltaZoom;
                float srcSize = (float)tileSize / factor;
                float srcX = (x % factor) * srcSize;
                float srcY = (y % factor) * srcSize;

                var srcRect = new SKRect(srcX, srcY, srcX + srcSize, srcY + srcSize);
                canvas.DrawBitmap(fallbackBitmap, srcRect, destRect, LinearSampling, paint);
                return;
            }

            fallbackZoom--;
            fallbackX /= 2;
            fallbackY /= 2;
            deltaZoom++;
        }
    }

    private void DrawPins(SKCanvas canvas, SKSize imageSize, SKRect view)
    {
        if (_sortedPins.Count == 0) return;

        var settings = SettingsService.Instance;
        double osBaseScale = settings.OsBaseScale;
        double maxLimit = settings.PinMaxScaleLimit / 100.0;
        double minLimit = settings.PinMinScaleLimit / 100.0;
        float imgWidth = imageSize.Width;
        float imgHeight = imageSize.Height;
        float mapScale = _scale > 0 ? _scale : 1f;

        foreach (var pin in _sortedPins)
        {
            SKBitmap pinBitmap = pin.Icon ?? GetOrLoadPinBitmap(pin);
            if (pinBitmap == null) continue;

            float absoluteX = pin.RelativeX * imgWidth;
            float absoluteY = pin.RelativeY * imgHeight;
            float pinScale = GetPinScale(pin, mapScale, osBaseScale, maxLimit, minLimit);
            float halfW = MathF.Max((float)pin.Anchor.X, 1f - (float)pin.Anchor.X) * pinBitmap.Width * pinScale;
            float halfH = MathF.Max((float)pin.Anchor.Y, 1f - (float)pin.Anchor.Y) * pinBitmap.Height * pinScale;
            float radius = MathF.Sqrt(halfW * halfW + halfH * halfH);

            if (absoluteX < view.Left - radius || absoluteX > view.Right + radius || absoluteY < view.Top - radius || absoluteY > view.Bottom + radius)
                continue;

            canvas.Save();
            canvas.Translate(absoluteX, absoluteY);

            if (!pin.IsLockRotate)
                canvas.RotateDegrees(-_rotationDegrees);
            else
                canvas.RotateDegrees(pin.Rotation);

            canvas.Scale(pinScale, pinScale);

            float left = -(float)(pin.Anchor.X * pinBitmap.Width);
            float top = -(float)(pin.Anchor.Y * pinBitmap.Height);

            canvas.DrawBitmap(pinBitmap, left, top, LinearSampling);
            canvas.Restore();
        }
    }

    // ---------------------------------------------------------------------------------
    //  Lupe
    // ---------------------------------------------------------------------------------
    private void DrawMagnifyingGlass(SKCanvas canvas)
    {
        if (_draggedPin == null) return;

        float currentLoupeRadius = SettingsService.Instance.LoupeRadius * (float)Settings.DisplayDensity;

        if (Math.Abs(_cachedLoupeRadius - currentLoupeRadius) > 0.1f)
            UpdateLoupeCache(currentLoupeRadius);

        SKMatrix mapMatrix = BuildMapMatrix();

        float pinAbsX = _draggedPin.RelativeX * OriginalImageSize.Width;
        float pinAbsY = _draggedPin.RelativeY * OriginalImageSize.Height;
        SKPoint pinScreenPos = mapMatrix.MapPoint(pinAbsX, pinAbsY);

        float zoomFactor = SettingsService.Instance.LoupeZoomFactor;
        float margin = 30f;
        float loupeCenterX = _cachedLoupeRadius + margin;
        float loupeCenterY = _cachedLoupeRadius + margin;

        var loupeDeviceClip = new SKRect(
            loupeCenterX - _cachedLoupeRadius,
            loupeCenterY - _cachedLoupeRadius,
            loupeCenterX + _cachedLoupeRadius,
            loupeCenterY + _cachedLoupeRadius);

        canvas.Save();
        canvas.Translate(loupeCenterX, loupeCenterY);
        canvas.ClipPath(_cachedLoupePath, SKClipOperation.Intersect, true);

        canvas.Save();
        canvas.Scale(zoomFactor);
        canvas.Translate(-pinScreenPos.X, -pinScreenPos.Y);

        DrawMapAndPins(canvas, loupeDeviceClip, isPrimaryPass: false);

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
            new SKPoint(0, 0),
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

    // ---------------------------------------------------------------------------------
    //  Eingabe
    // ---------------------------------------------------------------------------------
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
                HandleTouchPressed(e);
                break;

            case SKTouchAction.Moved:
                HandleTouchMoved(e);
                break;

            case SKTouchAction.Released:
                HandleTouchReleased(e);
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
                RequestRender();
                break;
        }

        e.Handled = true;
    }

    private void HandleTouchPressed(SKTouchEventArgs e)
    {
        _isLongPressActive = false;
        _activeTouches[e.Id] = e.Location;

        if (_activeTouches.Count == 1)
        {
            _touchStartPoint = e.Location;
            _touchStartTime = DateTime.UtcNow;
            _hasDraggedPin = false;
            _isDoubleTapAction = false;
            _lastTouchPoint = e.Location;

            _draggedPin = GetPinAtPosition(e.Location);

            if (_draggedPin != null && _draggedPin.IsLockPosition)
                _draggedPin = null;

            if (_draggedPin != null)
            {
                _originalPinX = _draggedPin.RelativeX;
                _originalPinY = _draggedPin.RelativeY;
            }
            else
            {
                StartLongPressDetection(e.Location);
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
    }

    private void StartLongPressDetection(SKPoint location)
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = new CancellationTokenSource();

        var token = _longPressCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LongPressTimeoutMs, token);

                if (!token.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_disposed) return;
                        _isLongPressActive = true;
                        CanvasLongPressed?.Invoke(this, location);
                    });
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void HandleTouchMoved(SKTouchEventArgs e)
    {
        if (!HasValidViewport) return;
        if (_isDoubleTapAction) return;
        if (_isLongPressActive) return;

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

            _panX += newCenterX - oldCenterX;
            _panY += newCenterY - oldCenterY;

            float newDistance = SKPoint.Distance(newP0, newP1);

            if (_oldFingerDistance > 0)
            {
                float scaleFactor = newDistance / _oldFingerDistance;
                float minScale = GetMinScale();
                float newScale = Math.Clamp(_scale * scaleFactor, minScale, 16.0f);
                float scaleRatio = newScale / _scale;

                _panX = newCenterX - (newCenterX - _panX) * scaleRatio;
                _panY = newCenterY - (newCenterY - _panY) * scaleRatio;
                _scale = float.IsFinite(newScale) ? MathF.Max(newScale, 1e-6f) : _scale;
            }

            _oldFingerDistance = newDistance;

            float newAngle = (float)Math.Atan2(newP1.Y - newP0.Y, newP1.X - newP0.X);

            if (!IsRotationLocked && _oldFingerAngle != 0f)
            {
                float angleDiff = newAngle - _oldFingerAngle;
                if (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
                if (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

                _rotationDegrees += angleDiff * (180f / (float)Math.PI);

                float cos = (float)Math.Cos(angleDiff);
                float sin = (float)Math.Sin(angleDiff);
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

            InvalidateView();
        }
    }

    private void HandleTouchReleased(SKTouchEventArgs e)
    {
        _longPressCts?.Cancel();

        if (_isLongPressActive)
        {
            _isLongPressActive = false;
            _draggedPin = null;
            _activeTouches.Remove(e.Id);
            return;
        }

        bool isInsideThreshold = SKPoint.Distance(_touchStartPoint, e.Location) < ClickThreshold;
        bool isQuickTap = (DateTime.UtcNow - _touchStartTime).TotalMilliseconds < 300;
        bool isTap = isInsideThreshold && !_hasDraggedPin && isQuickTap;

        if (_activeTouches.Count == 1 && isTap)
            HandleTap(e.Location);
        else if (_draggedPin != null)
            PinMoved?.Invoke(this, _draggedPin);

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

        RequestRender();
    }

    private void HandleTap(SKPoint location)
    {
        var now = DateTime.UtcNow;
        double elapsed = (now - _lastTapTime).TotalMilliseconds;
        float distance = SKPoint.Distance(_lastTapLocation, location);

        var currentPin = GetPinAtPosition(location);

        if (elapsed < DoubleTapTimeoutMs && distance < DoubleTapDistanceThreshold)
        {
            _isDoubleTapAction = true;
            _tapCts?.Cancel();
            _tapCts?.Dispose();
            _tapCts = null;
            _lastTapTime = DateTime.MinValue;

            if (currentPin != null)
                PinDoubleTapped?.Invoke(this, currentPin);
            else
                CanvasDoubleTapped?.Invoke(this, location);

            return;
        }

        _isDoubleTapAction = false;
        _lastTapTime = now;
        _lastTapLocation = location;

        _tapCts?.Cancel();
        _tapCts?.Dispose();
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
                        if (_disposed) return;

                        if (currentPin != null)
                        {
                            PinTapped?.Invoke(this, currentPin);
                        }
                        else
                        {
                            CanvasTapped?.Invoke(this, location);

                            if (PinCreationMode == PinCreationMode.SingleTap)
                                CanvasLongPressed?.Invoke(this, location);
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private static void OnCurrentRotationChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TileImageView)bindable;
        float newRotation = (float)newValue;

        if (Math.Abs(control._rotationDegrees - newRotation) <= 0.01f) return;

        if (control._canvasView.CanvasSize.Width > 0 && control._canvasView.CanvasSize.Height > 0)
        {
            float centerX = control._canvasView.CanvasSize.Width / 2f;
            float centerY = control._canvasView.CanvasSize.Height / 2f;

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
        control.InvalidateView();
    }

    // ---------------------------------------------------------------------------------
    //  Windows-Maussteuerung
    // ---------------------------------------------------------------------------------
#if WINDOWS
    private void OnLoadedWindows(object sender, EventArgs e)
    {
        if (_canvasView.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement winView)
        {
            // C2: Referenz merken, damit spaeter sauber abgemeldet werden kann.
            _winView = winView;
            winView.PointerWheelChanged += OnWinViewPointerWheelChanged;
            winView.PointerPressed += OnWinViewPointerPressed;
            winView.PointerMoved += OnWinViewPointerMoved;
            winView.PointerReleased += OnWinViewPointerReleased;
        }
    }

    private void OnUnloadedWindows(object sender, EventArgs e) => DetachWindowsHandlers();

    private void DetachWindowsHandlers()
    {
        if (_winView == null) return;

        _winView.PointerWheelChanged -= OnWinViewPointerWheelChanged;
        _winView.PointerPressed -= OnWinViewPointerPressed;
        _winView.PointerMoved -= OnWinViewPointerMoved;
        _winView.PointerReleased -= OnWinViewPointerReleased;
        _winView = null;
    }

    private void OnWinViewPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!HasValidViewport) return;

        var pointerPoint = e.GetCurrentPoint((Microsoft.UI.Xaml.UIElement)sender);
        var position = pointerPoint.Position;
        int delta = pointerPoint.Properties.MouseWheelDelta;

        float density = (float)Settings.DisplayDensity;
        SKPoint mousePos = new((float)position.X * density, (float)position.Y * density);

        HandleMouseZoom(mousePos, delta);
        e.Handled = true;
    }

    private void OnWinViewPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var winView = (Microsoft.UI.Xaml.UIElement)sender;
        var pointerPoint = e.GetCurrentPoint(winView);

        if (!pointerPoint.Properties.IsRightButtonPressed) return;
        if (!HasValidViewport) return;

        _isRightMouseRotating = true;

        float density = (float)Settings.DisplayDensity;
        SKPoint mousePos = new((float)pointerPoint.Position.X * density, (float)pointerPoint.Position.Y * density);

        float centerX = _canvasView.CanvasSize.Width / 2f;
        float centerY = _canvasView.CanvasSize.Height / 2f;

        _lastMouseRotationAngle = (float)Math.Atan2(mousePos.Y - centerY, mousePos.X - centerX);

        winView.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnWinViewPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (IsRotationLocked || !_isRightMouseRotating || !HasValidViewport) return;

        var winView = (Microsoft.UI.Xaml.UIElement)sender;
        var pointerPoint = e.GetCurrentPoint(winView);

        float density = (float)Settings.DisplayDensity;
        SKPoint mousePos = new((float)pointerPoint.Position.X * density, (float)pointerPoint.Position.Y * density);

        float centerX = _canvasView.CanvasSize.Width / 2f;
        float centerY = _canvasView.CanvasSize.Height / 2f;

        float currentAngle = (float)Math.Atan2(mousePos.Y - centerY, mousePos.X - centerX);
        float angleDiff = currentAngle - _lastMouseRotationAngle;

        if (angleDiff > Math.PI) angleDiff -= (float)(2 * Math.PI);
        if (angleDiff < -Math.PI) angleDiff += (float)(2 * Math.PI);

        if (Math.Abs(angleDiff) > 0.001f)
        {
            _rotationDegrees += angleDiff * (180f / (float)Math.PI);

            float cos = (float)Math.Cos(angleDiff);
            float sin = (float)Math.Sin(angleDiff);
            float dx = _panX - centerX;
            float dy = _panY - centerY;

            _panX = centerX + (dx * cos - dy * sin);
            _panY = centerY + (dx * sin + dy * cos);

            _lastMouseRotationAngle = currentAngle;

            CurrentRotation = _rotationDegrees;
            CurrentPan = new SKPoint(_panX, _panY);

            InvalidateView();
        }

        e.Handled = true;
    }

    private void OnWinViewPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isRightMouseRotating) return;

        _isRightMouseRotating = false;
        var winView = (Microsoft.UI.Xaml.UIElement)sender;
        winView.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
#endif

    // ---------------------------------------------------------------------------------
    //  Pin-Verwaltung
    // ---------------------------------------------------------------------------------

    private static void OnPinsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (TileImageView)bindable;

        control._observedPins?.CollectionChanged -= control.OnPinsCollectionChanged;
        control._observedPins = null;

        if (oldValue is INotifyCollectionChanged oldCollection)
            oldCollection.CollectionChanged -= control.OnPinsCollectionChanged;

        if (newValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnPinsCollectionChanged;
            control._observedPins = newCollection;
        }

        control.UpdateSortedPins();

        if (newValue is IEnumerable<MapPin> newPins)
            _ = control.PreloadPinBitmapsAsync(newPins);

        if (control._pendingPinId != null && control.HasValidViewport)
            control.ZoomToPin(control._pendingPinId, control._pendingZoomFactor);
        else
            control.RequestRender();
    }

    private void OnPinsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _pinsNeedSort = true;

        if (e.NewItems != null)
            _ = PreloadPinBitmapsAsync(e.NewItems.OfType<MapPin>());

        if (_pendingPinId != null && HasValidViewport)
        {
            ZoomToPin(_pendingPinId, _pendingZoomFactor);
            return;
        }

        RequestRender();
    }

    private void UpdateSortedPins()
    {
        if (Pins == null)
        {
            _sortedPins.Clear();
            return;
        }

        _sortedPins =
        [
            .. Pins
                .OrderByDescending(p => p.IsCustomPin)
                .ThenByDescending(p => p.PinScale)
        ];
    }

    private SKBitmap GetOrLoadPinBitmap(MapPin pin)
    {
        if (string.IsNullOrEmpty(pin.IconPath)) return null;

        if (_pinIconCache.TryGetValue(pin.IconPath, out var cachedBitmap))
            return cachedBitmap;

        _ = PreloadPinBitmapsAsync([pin]);
        return null;
    }

    private MapPin GetPinAtPosition(SKPoint touchPoint)
    {
        if (Pins == null || OriginalImageSize == SKSize.Empty) return null;

        var settings = SettingsService.Instance;
        double osBaseScale = settings.OsBaseScale;
        double maxLimit = settings.PinMaxScaleLimit / 100.0;
        double minLimit = settings.PinMinScaleLimit / 100.0;
        float mapScale = _scale > 0 ? _scale : 1f;

        SKSize imageSize = OriginalImageSize;
        SKMatrix baseMatrix = BuildMapMatrix();

        for (int i = _sortedPins.Count - 1; i >= 0; i--)
        {
            var pin = _sortedPins[i];

            SKBitmap pinBitmap = pin.Icon ?? GetOrLoadPinBitmap(pin);
            if (pinBitmap == null) continue;

            float absoluteX = pin.RelativeX * imageSize.Width;
            float absoluteY = pin.RelativeY * imageSize.Height;

            SKMatrix matrix = baseMatrix.PreConcat(SKMatrix.CreateTranslation(absoluteX, absoluteY));

            matrix = pin.IsLockRotate
                ? matrix.PreConcat(SKMatrix.CreateRotationDegrees(pin.Rotation))
                : matrix.PreConcat(SKMatrix.CreateRotationDegrees(-_rotationDegrees));

            float pinScale = GetPinScale(pin, mapScale, osBaseScale, maxLimit, minLimit);
            matrix = matrix.PreConcat(SKMatrix.CreateScale(pinScale, pinScale));

            if (!matrix.TryInvert(out SKMatrix inverseMatrix)) continue;

            SKPoint localPoint = inverseMatrix.MapPoint(touchPoint);

            float left = -(float)(pin.Anchor.X * pinBitmap.Width);
            float top = -(float)(pin.Anchor.Y * pinBitmap.Height);
            var localBounds = new SKRect(left, top, left + pinBitmap.Width, top + pinBitmap.Height);

            if (localBounds.Contains(localPoint.X, localPoint.Y))
                return pin;
        }

        return null;
    }

    private static float GetPinScale(MapPin pin, float mapScale, double osBaseScale, double maxLimit, double minLimit)
    {
        if (pin.IsCustomPin || pin.IsLockAutoScale)
            return pin.PinScale;

        double dynamicScale = 1.0 / (mapScale > 0 ? mapScale : 1.0);

        if (dynamicScale > maxLimit) dynamicScale = maxLimit;
        if (dynamicScale < minLimit) dynamicScale = minLimit;

        return (float)(osBaseScale * dynamicScale * pin.PinScale);
    }

    private void UpdateDraggedPinPosition(SKPoint touchPoint)
    {
        if (_draggedPin == null || OriginalImageSize == SKSize.Empty) return;

        SKMatrix matrix = BuildMapMatrix();
        if (!matrix.TryInvert(out SKMatrix inverseMatrix)) return;

        SKPoint currentPlanPoint = inverseMatrix.MapPoint(touchPoint);
        SKPoint previousPlanPoint = inverseMatrix.MapPoint(_lastTouchPoint);

        float deltaX = currentPlanPoint.X - previousPlanPoint.X;
        float deltaY = currentPlanPoint.Y - previousPlanPoint.Y;

        float newRelX = _draggedPin.RelativeX + (deltaX / OriginalImageSize.Width);
        float newRelY = _draggedPin.RelativeY + (deltaY / OriginalImageSize.Height);

        _draggedPin.RelativeX = Math.Clamp(newRelX, 0f, 1f);
        _draggedPin.RelativeY = Math.Clamp(newRelY, 0f, 1f);

        _lastTouchPoint = touchPoint;
    }

    private async Task PreloadPinBitmapsAsync(IEnumerable<MapPin> pins)
    {
        if (pins == null || _disposed) return;

        var missingPaths = pins
            .Select(p => p.IconPath)
            .Where(path => !string.IsNullOrEmpty(path)
                           && !_pinIconCache.ContainsKey(path)
                           && !_loadingPinPaths.Contains(path))
            .Distinct()
            .ToList();

        if (missingPaths.Count == 0) return;

        foreach (var path in missingPaths)
            _loadingPinPaths.Add(path);

        try
        {
            await Task.Run(() =>
            {
                foreach (var path in missingPaths)
                {
                    SKBitmap bitmap = null;

                    try
                    {
                        bitmap = IconBitmapLoader.Load(path);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Fehler beim Laden des Pin-Icons '{path}': {ex.Message}");
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _loadingPinPaths.Remove(path);

                        if (bitmap == null) return;

                        if (_disposed || !_pinIconCache.TryAdd(path, bitmap))
                        {
                            bitmap.Dispose();
                            return;
                        }

                        RequestRender();
                    });
                }
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var path in missingPaths)
                    _loadingPinPaths.Remove(path);
            });
        }
    }

    // ---------------------------------------------------------------------------------
    //  Bild laden und Pyramide erzeugen
    // ---------------------------------------------------------------------------------
    private async Task ProcessNewImageAsync(string imagePath)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ClearCache();
        _displayZoom = -1;
        _maxGeneratedLevel = -1;

        if (string.IsNullOrEmpty(imagePath))
        {
            _computedTileFolder = string.Empty;
            InvalidateView();
            return;
        }

        int tileSize = TileSize;
        int maxZoomLevel = MaxZoomLevel;
        SKColor backgroundColor = _placeholderSKColor;

        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;

        try
        {
            var prepared = await Task.Run(() =>
            {
                if (!File.Exists(imagePath))
                    return (Valid: false, Size: SKSize.Empty, Folder: string.Empty, TilesExist: false);

                SKSize size = SKSize.Empty;
                using (var codec = SKCodec.Create(imagePath))
                {
                    if (codec != null)
                        size = new SKSize(codec.Info.Width, codec.Info.Height);
                }

                if (size.IsEmpty)
                    return (Valid: false, Size: SKSize.Empty, Folder: string.Empty, TilesExist: false);

                CleanupOldTileFolders(imagePath, tileSize);

                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
                string folder = Path.Combine(FileSystem.AppDataDirectory, "Tiles", $"{fileNameWithoutExt}_{tileSize}");
                bool complete = File.Exists(Path.Combine(folder, PyramidCompleteMarker));

                return (Valid: true, Size: size, Folder: folder, TilesExist: complete);
            }, token);

            token.ThrowIfCancellationRequested();

            if (!prepared.Valid)
            {
                _computedTileFolder = string.Empty;
                OriginalImageSize = SKSize.Empty;
                return;
            }

            OriginalImageSize = prepared.Size;
            _computedTileFolder = prepared.Folder;
            RebuildLevelMetadata();

            _rotationDegrees = 0f;
            CurrentRotation = _rotationDegrees;

            if (_pendingPinId == null && !_pendingImageFit)
            {
                _scale = 1.0f;
                _panX = 0f;
                _panY = 0f;
                CurrentScale = _scale;
                CurrentPan = new SKPoint(_panX, _panY);
            }

            if (_pendingPinId != null)
                ZoomToPin(_pendingPinId, _pendingZoomFactor);
            else
                RequestRender();

            if (prepared.TilesExist)
            {
                _maxGeneratedLevel = maxZoomLevel;
            }
            else
            {
                await Task.Run(() => GenerateTilePyramidInternal(
                    imagePath,
                    prepared.Folder,
                    maxZoomLevel,
                    tileSize,
                    backgroundColor,
                    token,
                    level => MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (_disposed) return;
                        _maxGeneratedLevel = Math.Max(_maxGeneratedLevel, level);

                        if (_pendingPinId != null && HasValidViewport)
                            ZoomToPin(_pendingPinId, _pendingZoomFactor);
                        else
                            InvalidateView();
                    })
                ), token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Laden des Hintergrundbildes: {ex.Message}");
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
            _canvasView.IsVisible = true;
            InvalidateView();
        }
    }

    private static void CleanupOldTileFolders(string imagePath, int currentTileSize)
    {
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
        string baseFolder = Path.Combine(FileSystem.AppDataDirectory, "Tiles");

        if (!Directory.Exists(baseFolder)) return;

        foreach (var dir in Directory.GetDirectories(baseFolder, $"{fileNameWithoutExt}_*"))
        {
            if (dir.EndsWith($"_{currentTileSize}")) continue;

            try
            {
                Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Loeschen alter Kacheln: {ex.Message}");
            }
        }
    }

    private static void GenerateTilePyramidInternal(
        string sourceImagePath,
        string outputFolder,
        int maxZoomLevels,
        int tileSize,
        SKColor tileBackgroundColor,
        CancellationToken token,
        Action<int> onLevelGenerated = null)
    {
        int origWidth;
        int origHeight;

        using (var probe = SKCodec.Create(sourceImagePath))
        {
            if (probe == null) return;
            origWidth = probe.Info.Width;
            origHeight = probe.Info.Height;
        }

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        bool pyramidComplete = true;

        for (int zoom = 0; zoom <= maxZoomLevels; zoom++)
        {
            token.ThrowIfCancellationRequested();

            double scale = 1.0 / (1 << (maxZoomLevels - zoom));
            int levelWidth = Math.Max(1, (int)(origWidth * scale));
            int levelHeight = Math.Max(1, (int)(origHeight * scale));

            using var levelBitmap = DecodeScaled(sourceImagePath, levelWidth, levelHeight);

            if (levelBitmap == null || levelBitmap.Width != levelWidth || levelBitmap.Height != levelHeight)
            {
                Debug.WriteLine($"Pyramide: Stufe {zoom} ({levelWidth}x{levelHeight}) konnte nicht erzeugt werden.");
                pyramidComplete = false;
                continue;
            }

            int levelErrors = 0;

            int tilesX = (int)Math.Ceiling((double)levelBitmap.Width / tileSize);
            int tilesY = (int)Math.Ceiling((double)levelBitmap.Height / tileSize);

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
                    int width = Math.Min(tileSize, levelBitmap.Width - srcX);
                    int height = Math.Min(tileSize, levelBitmap.Height - srcY);

                    if (width <= 0 || height <= 0) continue;

                    var srcRectI = new SKRectI(srcX, srcY, srcX + width, srcY + height);

                    using var subsetBitmap = new SKBitmap();
                    if (!levelBitmap.ExtractSubset(subsetBitmap, srcRectI))
                    {
                        Interlocked.Exchange(ref levelErrors, 1);
                        continue;
                    }

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
                        string tempPath = tilePath + ".tmp";

                        using (var image = SKImage.FromBitmap(tileToSave))
                        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 85))
                        using (var stream = File.Create(tempPath))
                        {
                            data.SaveTo(stream);
                        }

                        File.Move(tempPath, tilePath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref levelErrors, 1);
                        Debug.WriteLine($"Fehler beim Schreiben der Kachel '{tilePath}': {ex.Message}");
                    }
                    finally
                    {
                        if (needsDispose)
                            tileToSave.Dispose();
                    }
                }
            });
            if (levelErrors != 0)
                pyramidComplete = false;

            onLevelGenerated?.Invoke(zoom);
        }

        if (!pyramidComplete)
        {
            Debug.WriteLine("Pyramide unvollstaendig - kein .complete-Marker geschrieben.");
            return;
        }

        try
        {
            File.WriteAllText(Path.Combine(outputFolder, PyramidCompleteMarker), DateTime.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Schreiben der Marker-Datei: {ex.Message}");
        }
    }

    private static SKBitmap DecodeScaled(string path, int targetWidth, int targetHeight)
    {
        SKBitmap decoded = TryDecodeViaCodec(path, targetWidth, targetHeight);
        decoded ??= TryDecodeFull(path);

        if (decoded == null)
        {
            Debug.WriteLine($"DecodeScaled: Dekodierung fehlgeschlagen fuer '{path}' ({targetWidth}x{targetHeight}).");
            return null;
        }

        if (decoded.Width == targetWidth && decoded.Height == targetHeight)
            return decoded;

        var resized = decoded.Resize(
        new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Opaque),
        LinearSampling);

        decoded.Dispose();

        if (resized == null)
            Debug.WriteLine($"DecodeScaled: Resize auf {targetWidth}x{targetHeight} fehlgeschlagen.");

        return resized;
    }

    private static SKBitmap TryDecodeViaCodec(string path, int targetWidth, int targetHeight)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec == null) return null;

            float desired = Math.Min(
            targetWidth / (float)codec.Info.Width,
            targetHeight / (float)codec.Info.Height);

            SKSizeI supported = codec.GetScaledDimensions(Math.Clamp(desired, 0.0001f, 1f));
            if (supported.Width <= 0 || supported.Height <= 0) return null;

            var info = new SKImageInfo(supported.Width, supported.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            var bmp = new SKBitmap(info);

            if (codec.GetPixels(info, bmp.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                bmp.Dispose();
                return null;
            }
            return bmp;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryDecodeViaCodec fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private static SKBitmap TryDecodeFull(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TryDecodeFull fehlgeschlagen: {ex.Message}");
            return null;
        }
    }

    private void DrawFpsCounter(SKCanvas canvas, float canvasWidth)
    {
        long now = Stopwatch.GetTimestamp();

        if (_lastFrameTicks != 0)
        {
            double ms = (now - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency;

            if (ms < 500)
            {
                _frameTimes[_frameTimeIndex] = ms;
                _frameTimeIndex = (_frameTimeIndex + 1) % _frameTimes.Length;

                double sum = 0;
                int count = 0;

                foreach (double t in _frameTimes)
                {
                    if (t <= 0) continue;
                    sum += t;
                    count++;
                }

                if (count > 0 && sum > 0)
                    _fpsValue = (float)(count * 1000.0 / sum);
            }
        }

        _lastFrameTicks = now;

        float density = (float)Settings.DisplayDensity;

        if (_fpsFont == null)
        {
            _fpsFont = new SKFont(SKTypeface.Default, 12f * density);
            _fpsTextPaint = new SKPaint { Color = SKColors.Lime, IsAntialias = true };
            _fpsBackPaint = new SKPaint { Color = SKColors.Black.WithAlpha(120), IsAntialias = true };
        }

        string[] lines =
            [
                $"{_fpsValue:0.0} FPS",
                $"Zoom: {_displayZoom}",
                $"Tiles: {_tileCache.Count + _permanentTiles.Count}",
                $"Queue: {_tileQueue.Count}"
            ];

        float margin = 8f * density;
        float pad = 5f * density;
        float textWidth = lines.Max(l => _fpsFont.MeasureText(l));
        float lineHeight = _fpsFont.Size * 1.2f;
        float boxHeight = lines.Length * lineHeight + 2 * pad;

        var box = new SKRect(
        canvasWidth - margin - textWidth - 2 * pad,
        margin,
        canvasWidth - margin,
        margin + boxHeight);

        canvas.DrawRoundRect(box, 4f * density, 4f * density, _fpsBackPaint);

        float y = box.Top + pad + _fpsFont.Size;

        foreach (var line in lines)
        {
            canvas.DrawText(
            line,
            box.Left + pad,
            y,
            SKTextAlign.Left,
            _fpsFont,
            _fpsTextPaint);

            y += lineHeight;
        }
    }

    // ---------------------------------------------------------------------------------
    //  Aufraeumen
    // ---------------------------------------------------------------------------------
    private void ClearCache()
    {
        _tileCache.Clear();
        _pendingTiles.Clear();
        _tileQueue.Clear();

        foreach (var bitmap in _permanentTiles.Values)
            bitmap?.Dispose();
        _permanentTiles.Clear();

        foreach (var bitmap in _pinIconCache.Values)
            bitmap?.Dispose();
        _pinIconCache.Clear();
        _loadingPinPaths.Clear();
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

        RequestRender();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (!disposing) return;

        if (_canvasView != null)
        {
            _canvasView.PaintSurface -= OnPaintSurface;
            _canvasView.Touch -= OnCanvasTouch;
        }

        _observedPins?.CollectionChanged -= OnPinsCollectionChanged;
        _observedPins = null;

#if WINDOWS
        Loaded -= OnLoadedWindows;
        Unloaded -= OnUnloadedWindows;
        DetachWindowsHandlers();
#endif

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;

        _tapCts?.Cancel();
        _tapCts?.Dispose();
        _tapCts = null;

        _fpsFont?.Dispose();
        _fpsTextPaint?.Dispose();
        _fpsBackPaint?.Dispose();

        ClearCache();
        _tileCache.Dispose();

        _cachedLoupePath?.Dispose();
        _cachedInnerShadowShader?.Dispose();
        _cachedGlareShader?.Dispose();

        _loupeShadowPaint?.Dispose();
        _loupeBorderPaint?.Dispose();
        _loupeCrosshairPaint?.Dispose();
        _loupeInnerShadowPaint?.Dispose();
        _loupeGlarePaint?.Dispose();

        _tileLoadSemaphore?.Dispose();

        _sortedPins.Clear();
    }
}

// =====================================================================================
//  MapPin
// =====================================================================================
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

// =====================================================================================
//  LruCache - Gibt verdraengte Werte, Kapazitaet zur Laufzeit anpassbar
// =====================================================================================
public partial class LruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache = [];
    private readonly LinkedList<CacheEntry> _list = [];
    private int _capacity;
    private bool _disposed;
    private readonly record struct CacheEntry(TKey Key, TValue Value);

    public LruCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public int Capacity
    {
        get { lock (_lock) { return _capacity; } }
    }

    public int Count
    {
        get { lock (_lock) { return _cache.Count; } }
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= 0) return;

        lock (_lock)
        {
            if (capacity > _capacity)
                _capacity = capacity;
        }
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Der Schluessel '{key}' wurde nicht gefunden.");
        set => Add(key, value);
    }

    public bool ContainsKey(TKey key)
    {
        lock (_lock)
        {
            return _cache.ContainsKey(key);
        }
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
        TValue toDispose = default;
        bool hasDisposable = false;

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var existingNode))
            {
                TValue oldValue = existingNode.Value.Value;
                _list.Remove(existingNode);
                _cache.Remove(key);

                if (!ReferenceEquals(oldValue, value))
                {
                    toDispose = oldValue;
                    hasDisposable = true;
                }
            }
            else
            {
                while (_cache.Count >= _capacity && _list.Last is not null)
                {
                    var lastNode = _list.Last;
                    _cache.Remove(lastNode.Value.Key);
                    _list.RemoveLast();

                    if (lastNode.Value.Value is IDisposable evicted)
                        evicted.Dispose();
                }
            }

            var newNode = _list.AddFirst(new CacheEntry(key, value));
            _cache[key] = newNode;
        }

        if (hasDisposable && toDispose is IDisposable disposable)
            disposable.Dispose();
    }

    public bool Remove(TKey key)
    {
        TValue removed = default;
        bool found;

        lock (_lock)
        {
            found = _cache.Remove(key, out var node);

            if (found)
            {
                removed = node.Value.Value;
                _list.Remove(node);
            }
        }

        if (found && removed is IDisposable disposable)
            disposable.Dispose();

        return found;
    }

    public void Clear()
    {
        List<TValue> values;

        lock (_lock)
        {
            values = [.. _list.Select(entry => entry.Value)];
            _cache.Clear();
            _list.Clear();
        }

        foreach (var value in values)
        {
            if (value is IDisposable disposable)
                disposable.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Clear();
        GC.SuppressFinalize(this);
    }
}

public readonly record struct TileKey(int Zoom, int X, int Y);
