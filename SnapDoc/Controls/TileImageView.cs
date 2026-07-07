#nullable disable
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

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
    private readonly Dictionary<string, SKBitmap> _tileCache = [];
    private readonly Dictionary<long, SKPoint> _activeTouches = [];
    private float _oldFingerDistance = 0f;
    private float _oldFingerAngle = 0f;
    private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    private SKPoint _touchStartPoint;
    private const float ClickThreshold = 15f;
    private MapPin _draggedPin = null;

    public static readonly BindableProperty SourceImagePathProperty =
        BindableProperty.Create(nameof(SourceImagePath), typeof(string), typeof(TileImageView), default(string),
            propertyChanged: async (bindable, oldValue, newValue) =>
            {
                var control = (TileImageView)bindable;
                await control.ProcessNewImageAsync((string)newValue);
            });

    public string SourceImagePath
    {
        get => (string)GetValue(SourceImagePathProperty);
        set => SetValue(SourceImagePathProperty, value);
    }

    public event EventHandler<MapPin> PinTapped;
    public event EventHandler<MapPin> PinMoved;

    public static readonly BindableProperty TileSizeProperty =
        BindableProperty.Create(nameof(TileSize), typeof(int), typeof(TileImageView), 256,
            propertyChanged: (bindable, o, n) => ((TileImageView)bindable)._canvasView.InvalidateSurface());

    public int TileSize
    {
        get => (int)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public static readonly BindableProperty MaxZoomLevelProperty =
        BindableProperty.Create(nameof(MaxZoomLevel), typeof(int), typeof(TileImageView), 4,
            propertyChanged: (bindable, o, n) => ((TileImageView)bindable)._canvasView.InvalidateSurface());

    public int MaxZoomLevel
    {
        get => (int)GetValue(MaxZoomLevelProperty);
        set => SetValue(MaxZoomLevelProperty, value);
    }

    public static readonly BindableProperty PinsProperty =
        BindableProperty.Create(nameof(Pins), typeof(IEnumerable<MapPin>), typeof(TileImageView), default(IEnumerable<MapPin>),
            propertyChanged: (bindable, o, n) => ((TileImageView)bindable)._canvasView.InvalidateSurface());

    public IEnumerable<MapPin> Pins
    {
        get => (IEnumerable<MapPin>)GetValue(PinsProperty);
        set => SetValue(PinsProperty, value);
    }

    private static readonly BindablePropertyKey OriginalImageSizePropertyKey =
        BindableProperty.CreateReadOnly(nameof(OriginalImageSize), typeof(SKSize), typeof(TileImageView), SKSize.Empty);

    public static readonly BindableProperty OriginalImageSizeProperty = OriginalImageSizePropertyKey.BindableProperty;

    public SKSize OriginalImageSize
    {
        get => (SKSize)GetValue(OriginalImageSizeProperty);
        private set => SetValue(OriginalImageSizePropertyKey, value);
    }

    // Aktueller Zoom-Faktor
    private static readonly BindablePropertyKey CurrentScalePropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentScale), typeof(float), typeof(TileImageView), 1.0f);

    public static readonly BindableProperty CurrentScaleProperty = CurrentScalePropertyKey.BindableProperty;

    public float CurrentScale
    {
        get => (float)GetValue(CurrentScaleProperty);
        private set => SetValue(CurrentScalePropertyKey, value);
    }

    private static readonly BindablePropertyKey CurrentPanPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentPan), typeof(SKPoint), typeof(TileImageView), SKPoint.Empty);

    public static readonly BindableProperty CurrentPanProperty = CurrentPanPropertyKey.BindableProperty;

    public SKPoint CurrentPan
    {
        get => (SKPoint)GetValue(CurrentPanProperty);
        private set => SetValue(CurrentPanPropertyKey, value);
    }

    private static readonly BindablePropertyKey CurrentRotationPropertyKey =
        BindableProperty.CreateReadOnly(nameof(CurrentRotation), typeof(float), typeof(TileImageView), 0f);

    public static readonly BindableProperty CurrentRotationProperty = CurrentRotationPropertyKey.BindableProperty;

    public float CurrentRotation
    {
        get => (float)GetValue(CurrentRotationProperty);
        private set => SetValue(CurrentRotationPropertyKey, value);
    }

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

        _loadingIndicator = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Color = Colors.CadetBlue
        };

        _layoutGrid.Children.Add(_canvasView);
        _layoutGrid.Children.Add(_loadingIndicator);
        Content = _layoutGrid;
    }

    private async Task ProcessNewImageAsync(string imagePath)
    {
        if (_isGenerating) return;

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            _computedTileFolder = string.Empty;
            ClearCache();
            _canvasView.InvalidateSurface();
            return;
        }

        _isGenerating = true;
        ClearCache();

        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(imagePath);
        _computedTileFolder = Path.Combine(FileSystem.AppDataDirectory, "Tiles", $"{fileNameWithoutExt}");

        bool tilesExist = Directory.Exists(_computedTileFolder) &&
                          Directory.GetFiles(_computedTileFolder, "*.png", SearchOption.AllDirectories).Length > 0;

        if (!tilesExist)
        {
            _loadingIndicator.IsVisible = true;
            _loadingIndicator.IsRunning = true;
            _canvasView.IsVisible = false;

            await Task.Run(() => GenerateTilePyramidInternal(imagePath, _computedTileFolder, MaxZoomLevel, TileSize));
        }

        _loadingIndicator.IsRunning = false;
        _loadingIndicator.IsVisible = false;
        _canvasView.IsVisible = true;

        using (var codec = SKCodec.Create(imagePath))
        {
            if (codec != null)
                OriginalImageSize = new SKSize(codec.Info.Width, codec.Info.Height);
        }

        _scale = 1.0f;
        _panX = 0f;
        _panY = 0f;
        _rotationDegrees = 0f;

        CurrentScale = _scale;
        CurrentPan = new SKPoint(_panX, _panY);
        CurrentRotation = _rotationDegrees;

        _isGenerating = false;
        _canvasView.InvalidateSurface();
    }

    private void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.LightGray);

        if (_isGenerating || string.IsNullOrEmpty(_computedTileFolder)) return;

        int currentZoom = MaxZoomLevel + (int)Math.Floor(Math.Log2(_scale));
        currentZoom = Math.Clamp(currentZoom, 2, MaxZoomLevel);

        canvas.Save();
        canvas.Translate(_panX, _panY);
        canvas.RotateDegrees(_rotationDegrees);
        canvas.Scale(_scale);

        float tileScaleFactor = (float)Math.Pow(2, MaxZoomLevel - currentZoom);
        float currentTileSizeInCanvasSpace = TileSize * tileScaleFactor;

        float canvasWidth = (float)_canvasView.Width;
        float canvasHeight = (float)_canvasView.Height;

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

        int maxTiles = (int)Math.Pow(2, currentZoom);

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
                    string tilePath = $"{xFolder}/{y}.png";
                    if (File.Exists(tilePath))
                    {
                        using var stream = File.OpenRead(tilePath);
                        bitmap = SKBitmap.Decode(stream);
                        if (bitmap != null)
                        {
                            _tileCache[cacheKey] = bitmap;
                        }
                    }
                }

                if (bitmap == null) continue;

                float posX = x * currentTileSizeInCanvasSpace;
                float posY = y * currentTileSizeInCanvasSpace;

                var destRect = new SKRect(posX, posY, posX + currentTileSizeInCanvasSpace, posY + currentTileSizeInCanvasSpace);
                canvas.DrawBitmap(bitmap, destRect, LinearSampling, null);
            }
        }

        if (Pins != null && OriginalImageSize != SKSize.Empty)
        {
            float padding = 50f;
            float l = viewLeft - padding;
            float r = viewRight + padding;
            float t = viewTop - padding;
            float b = viewBottom + padding;

            foreach (var pin in Pins)
            {
                if (pin.Icon == null) continue;

                float absoluteX = pin.RelativeX * OriginalImageSize.Width;
                float absoluteY = pin.RelativeY * OriginalImageSize.Height;

                if (absoluteX < l || absoluteX > r || absoluteY < t || absoluteY > b)
                    continue;

                canvas.Save();
                canvas.Translate(absoluteX, absoluteY);

                if (pin.AutoRotate)
                {
                    canvas.RotateDegrees(-_rotationDegrees);
                }

                float left = -pin.Icon.Width / 2f;
                float top = -pin.Icon.Height;

                canvas.DrawBitmap(pin.Icon, left, top, LinearSampling, null);
                canvas.Restore();
            }
        }

        canvas.Restore();
    }

    private void ClearCache()
    {
        foreach (var bitmap in _tileCache.Values)
        {
            bitmap?.Dispose();
        }
        _tileCache.Clear();
    }

    private static void GenerateTilePyramidInternal(string sourceImagePath, string outputFolder, int maxZoomLevels, int tileSize)
    {
        using var codec = SKCodec.Create(sourceImagePath);
        if (codec == null) return;
        using var originalBitmap = SKBitmap.Decode(codec);
        int origWidth = originalBitmap.Width;
        int origHeight = originalBitmap.Height;

        for (int zoom = 0; zoom <= maxZoomLevels; zoom++)
        {
            double scale = Math.Pow(0.5, maxZoomLevels - zoom);
            int levelWidth = (int)(origWidth * scale);
            int levelHeight = (int)(origHeight * scale);

            using var scaledBitmap = originalBitmap.Resize(new SKImageInfo(levelWidth, levelHeight), LinearSampling);
            if (scaledBitmap == null) continue;

            int tilesX = (int)Math.Ceiling((double)levelWidth / tileSize);
            int tilesY = (int)Math.Ceiling((double)levelHeight / tileSize);

            for (int x = 0; x < tilesX; x++)
            {
                for (int y = 0; y < tilesY; y++)
                {
                    string tileDirectory = Path.Combine(outputFolder, zoom.ToString(), x.ToString());
                    Directory.CreateDirectory(tileDirectory);
                    string tilePath = Path.Combine(tileDirectory, $"{y}.png");

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
                    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
                    using var stream = File.OpenWrite(tilePath);
                    data.SaveTo(stream);
                }
            }
        }
    }

    private void OnCanvasTouch(object sender, SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _activeTouches[e.Id] = e.Location;
                if (_activeTouches.Count == 1)
                {
                    _touchStartPoint = e.Location;
                    _draggedPin = GetPinAtPosition(e.Location);
                }
                if (_activeTouches.Count == 2)
                {
                    _draggedPin = null;
                    var points = _activeTouches.Values.ToArray();
                    _oldFingerDistance = SKPoint.Distance(points[0], points[1]);
                    _oldFingerAngle = (float)Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
                }
                break;

            case SKTouchAction.Moved:
                if (_isGenerating) break;

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
                if (_draggedPin != null)
                {
                    if (SKPoint.Distance(_touchStartPoint, e.Location) < ClickThreshold)
                    {
                        PinTapped?.Invoke(this, _draggedPin);
                    }
                    else
                    {
                        PinMoved?.Invoke(this, _draggedPin);
                    }
                    _draggedPin = null;
                }

                _activeTouches.Remove(e.Id);
                if (_activeTouches.Count < 2)
                {
                    _oldFingerDistance = 0f;
                    _oldFingerAngle = 0f;
                }
                break;

            case SKTouchAction.Cancelled:
                _draggedPin = null;
                _activeTouches.Remove(e.Id);
                break;
        }

        e.Handled = true;
    }

    private MapPin GetPinAtPosition(SKPoint touchPoint)
    {
        if (Pins == null || OriginalImageSize == SKSize.Empty) return null;

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix)) return null;
        SKPoint planPoint = inverseMatrix.MapPoint(touchPoint);

        foreach (var pin in Pins.Reverse())
        {
            if (pin.Icon == null) continue;

            float pinX = pin.RelativeX * OriginalImageSize.Width;
            float pinY = pin.RelativeY * OriginalImageSize.Height;

            float clickRadiusX = pin.Icon.Width / 2f;
            float clickRadiusY = pin.Icon.Height;

            var pinBounds = new SKRect(pinX - clickRadiusX, pinY - clickRadiusY, pinX + clickRadiusX, pinY);

            if (pinBounds.Contains(planPoint.X, planPoint.Y))
            {
                return pin; // Pin gefunden!
            }
        }
        return null;
    }

    private void UpdateDraggedPinPosition(SKPoint touchPoint)
    {
        if (_draggedPin == null || OriginalImageSize == SKSize.Empty) return;

        SKMatrix matrix = SKMatrix.CreateTranslation(_panX, _panY);
        matrix = matrix.PreConcat(SKMatrix.CreateRotationDegrees(_rotationDegrees));
        matrix = matrix.PreConcat(SKMatrix.CreateScale(_scale, _scale));

        if (!matrix.TryInvert(out SKMatrix inverseMatrix)) return;
        SKPoint planPoint = inverseMatrix.MapPoint(touchPoint);

        float newRelX = planPoint.X / OriginalImageSize.Width;
        float newRelY = planPoint.Y / OriginalImageSize.Height;

        _draggedPin.RelativeX = Math.Clamp(newRelX, 0f, 1f);
        _draggedPin.RelativeY = Math.Clamp(newRelY, 0f, 1f);
    }

    public Point GetPlanFactorAtControlCenter()
    {
        if (OriginalImageSize == SKSize.Empty || _canvasView.Width <= 0 || _canvasView.Height <= 0)
            return new Point(0, 0);

        float centerX = (float)_canvasView.Width / 2f;
        float centerY = (float)_canvasView.Height / 2f;
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
}

public class MapPin
{
    public string Id { get; set; }
    public float RelativeX { get; set; }
    public float RelativeY { get; set; }
    public SKBitmap Icon { get; set; }
    public bool AutoRotate { get; set; } = true;
}