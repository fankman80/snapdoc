#nullable disable
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace SnapDoc.Controls;

public partial class TileImageView : ContentView
{
    private readonly SKCanvasView _canvasView;
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

    public TileImageView()
    {
        BackgroundColor = Colors.White;
        _layoutGrid = [];
        _canvasView = new SKCanvasView
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
        _computedTileFolder = Path.Combine(FileSystem.AppDataDirectory, "Tiles", $"{fileNameWithoutExt}_{TileSize}");

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

        _scale = 1.0f;
        _panX = 0f;
        _panY = 0f;

        _isGenerating = false;
        _canvasView.InvalidateSurface();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
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

        var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

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

        int minX = (int)Math.Floor(viewLeft / currentTileSizeInCanvasSpace);
        int minY = (int)Math.Floor(viewTop / currentTileSizeInCanvasSpace);
        int maxX = (int)Math.Ceiling(viewRight / currentTileSizeInCanvasSpace);
        int maxY = (int)Math.Ceiling(viewBottom / currentTileSizeInCanvasSpace);
        int maxTiles = (int)Math.Pow(2, currentZoom);

        minX = Math.Clamp(minX, 0, maxTiles - 1);
        minY = Math.Clamp(minY, 0, maxTiles - 1);
        maxX = Math.Clamp(maxX, 0, maxTiles - 1);
        maxY = Math.Clamp(maxY, 0, maxTiles - 1);

        string zoomFolder = Path.Combine(_computedTileFolder, currentZoom.ToString());

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                string cacheKey = $"{currentZoom}_{x}_{y}";

                if (!_tileCache.TryGetValue(cacheKey, out var bitmap))
                {
                    string tilePath = Path.Combine(zoomFolder, x.ToString(), $"{y}.png");

                    if (File.Exists(tilePath))
                    {
                        try
                        {
                            using var stream = File.OpenRead(tilePath);
                            bitmap = SKBitmap.Decode(stream);
                            if (bitmap != null)
                            {
                                _tileCache[cacheKey] = bitmap;
                            }
                        }
                        catch { continue; }
                    }
                }

                if (bitmap == null) continue;

                float posX = x * currentTileSizeInCanvasSpace;
                float posY = y * currentTileSizeInCanvasSpace;

                var destRect = new SKRect(posX, posY, posX + currentTileSizeInCanvasSpace, posY + currentTileSizeInCanvasSpace);
                canvas.DrawBitmap(bitmap, destRect, samplingOptions, null);
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

            using var scaledBitmap = originalBitmap.Resize(new SKImageInfo(levelWidth, levelHeight), SKSamplingOptions.Default);
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
                        canvas.DrawBitmap(scaledBitmap, srcRect, destRect, SKSamplingOptions.Default, null);
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
                if (_activeTouches.Count == 2)
                {
                    var points = _activeTouches.Values.ToArray();
                    _oldFingerDistance = SKPoint.Distance(points[0], points[1]);
                    _oldFingerAngle = (float)Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
                }
                break;

            case SKTouchAction.Moved:
                if (_isGenerating) break;

                if (_activeTouches.Count == 1 && _activeTouches.TryGetValue(e.Id, out SKPoint oldPt))
                {
                    _panX += e.Location.X - oldPt.X;
                    _panY += e.Location.Y - oldPt.Y;
                    _activeTouches[e.Id] = e.Location;
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
                }
                _canvasView.InvalidateSurface();
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _activeTouches.Remove(e.Id);
                if (_activeTouches.Count < 2)
                {
                    _oldFingerDistance = 0f;
                    _oldFingerAngle = 0f;
                }
                break;
        }

        e.Handled = true;
    }
}