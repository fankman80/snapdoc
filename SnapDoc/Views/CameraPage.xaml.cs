using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using SnapDoc.Services;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraPage : ContentPage
{
    // Nominale Seitenverhaeltnisse fuer die Ratio-Buttons (immer >= 1.0).
    private static readonly (string Name, double Value)[] NominalRatios =
    {
        ("16:9", 16.0 / 9.0),
        ("3:2", 3.0 / 2.0),
        ("4:3", 4.0 / 3.0),
        ("5:4", 5.0 / 4.0),
        ("1:1", 1.0),
    };

    private const double RatioTolerance = 0.05;

    private string? _tempFilePath = string.Empty;

    // Wird bei jedem Kamerastart automatisch gesetzt (GetBestRatio).
    // Eine manuelle Auswahl gilt nur bis zum naechsten Start / Kamerawechsel.
    private double _userSelectedRatio = 4.0 / 3.0;

    private CameraFlashMode _currentFlashMode = (CameraFlashMode)SettingsService.Instance.FlashMode;
    private bool _isZoomSupported = false;
    private bool _suppressZoomEvents = false;
    private bool _isRatioPickerExpanded = false;
    private bool _isInPreviewMode = false;
    private CancellationTokenSource? _zoomTimerCts;

    private IReadOnlyList<CameraInfo> _cameras = Array.Empty<CameraInfo>();
    private bool _isStarted = false;
    private bool _isCapturing = false;

    // MediaCaptured feuert asynchron; darueber wird das Ergebnis in den
    // Klick-Handler zurueckgefuehrt.
    private TaskCompletionSource<Stream?>? _captureTcs;

    // Physische Geraeteausrichtung - die Seite selbst bleibt im Hochformat.
    //   0  = Hochformat
    //   90 = Querformat, Geraeteoberkante zeigt nach links
    //  -90 = Querformat, Geraeteoberkante zeigt nach rechts
    // Gleichzeitig der Drehwinkel (im Uhrzeigersinn), damit Icons aufrecht stehen.
    private int _deviceRotation = 0;

    // Ausrichtung, in der das aktuell angezeigte Vorschaubild aufgenommen wurde.
    private int _previewRotation = 0;

    public CameraPage()
    {
        InitializeComponent();

        // Die Vorschau richtet sich nach der tatsaechlich verfuegbaren Flaeche unter
        // der Top-Bar - nicht nach der Seitengroesse.
        previewArea.SizeChanged += (_, _) => UpdateCameraLayout();
    }

    // ------------------------------------------------------------------
    // Lebenszyklus
    // ------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Wie die Systemkamera: Layout bleibt im Hochformat, nur die Icons drehen sich.
        OrientationLock.LockPortrait();
        StartOrientationTracking();
        ApplyControlRotation(animate: false);

        try
        {
            await InitializeCameraAsync();
        }
        catch (Exception ex)
        {
            Log($"Init-Fehler: {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        StopOrientationTracking();
        OrientationLock.Unlock();

        CameraResultService.SetResult(null);
        cameraView.StopCameraPreview();
        _isStarted = false;
    }

    private static void Log(string message) => Debug.WriteLine($"[CAM] {message}");

    private async Task InitializeCameraAsync()
    {
        if (_isStarted) return;

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            Log("Kameraberechtigung fehlt.");
            return;
        }

        // OnAppearing kann feuern, bevor der native Handler der CameraView verbunden ist.
        for (int i = 0; i < 30 && (cameraView.Handler == null || !cameraView.IsAvailable); i++)
            await Task.Delay(100);

        if (cameraView.Handler == null)
        {
            Log("CameraView-Handler nach 3 s nicht verbunden.");
            return;
        }

        _cameras = await cameraView.GetAvailableCameras(CancellationToken.None);
        if (_cameras.Count == 0)
        {
            Log("GetAvailableCameras lieferte 0 Kameras.");
            return;
        }

        switchCameraButton.IsVisible = _cameras.Count > 1;

        var camera = _cameras.FirstOrDefault(c => c.Position == CameraPosition.Rear) ?? _cameras[0];

        _isStarted = true;
        await ApplyCameraAsync(camera);
    }

    /// <summary>
    /// Erststart: nur Properties setzen - das Toolkit startet die Preview selbst.
    /// Kamerawechsel: Stop, warten, Properties setzen, warten, Start.
    /// </summary>
    private async Task ApplyCameraAsync(CameraInfo camera, bool isSwitch = false)
    {
        try
        {
            if (isSwitch)
            {
                cameraView.StopCameraPreview();
                await Task.Delay(300);
            }

            _userSelectedRatio = GetBestRatio(camera);

            cameraView.SelectedCamera = camera;
            cameraView.ImageCaptureResolution = SelectResolution(camera, _userSelectedRatio);
            cameraView.CameraFlashMode = _currentFlashMode;

            UpdateFlashButtonUI();
            UpdateCameraLayout();

            Log($"{camera.Name}: Ratio {_userSelectedRatio:F2}, Capture {cameraView.ImageCaptureResolution.Width}x{cameraView.ImageCaptureResolution.Height}");

            if (isSwitch)
            {
                await Task.Delay(300);
                var startTask = cameraView.StartCameraPreview(CancellationToken.None);
                if (await Task.WhenAny(startTask, Task.Delay(5000)) != startTask)
                    Log("StartCameraPreview hat nach 5 s nicht zurückgekehrt.");
                else
                    await startTask;
            }
        }
        catch (Exception ex)
        {
            Log($"Start-Fehler ({camera.Position}): {ex.GetType().Name}: {ex.Message}");
        }

        // Zoom-Grenzen liefert Android erst, wenn die Kamera tatsaechlich gebunden ist.
        await Task.Delay(700);
        PopulateRatioButtons(camera);
        await ConfigureZoomAsync(camera);
    }

    // ------------------------------------------------------------------
    // Geraeteausrichtung
    // ------------------------------------------------------------------

    private void StartOrientationTracking()
    {
        try
        {
            if (!Accelerometer.Default.IsSupported)
            {
                Log("Kein Beschleunigungssensor - Icons werden nicht gedreht.");
                return;
            }

            Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
            Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;

            if (!Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Start(SensorSpeed.UI);
        }
        catch (Exception ex)
        {
            Log($"Accelerometer-Start fehlgeschlagen: {ex.Message}");
        }
    }

    private void StopOrientationTracking()
    {
        try
        {
            Accelerometer.Default.ReadingChanged -= OnAccelerometerReadingChanged;
            if (Accelerometer.Default.IsMonitoring)
                Accelerometer.Default.Stop();
        }
        catch (Exception ex)
        {
            Log($"Accelerometer-Stop fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Ermittelt die Ausrichtung aus der Schwerkraft in der Displayebene.
    /// Mit Hysterese (35-55 Grad), damit die Icons an der Grenze nicht flackern.
    /// Liegt das Geraet flach oder steht es auf dem Kopf, bleibt der letzte Zustand.
    /// </summary>
    private void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        var a = e.Reading.Acceleration;
        double x = a.X, y = a.Y;

        // Geraet liegt (fast) flach -> keine verlaessliche Aussage.
        if (Math.Sqrt(x * x + y * y) < 0.5) return;

        // 0 = aufrecht, +90 = rechte Kante oben, -90 = linke Kante oben.
        double angle = Math.Atan2(x, y) * 180.0 / Math.PI;

        int candidate = _deviceRotation;
        if (Math.Abs(angle) < 35) candidate = 0;
        else if (angle > 55 && angle < 125) candidate = 90;
        else if (angle < -55 && angle > -125) candidate = -90;

        if (candidate == _deviceRotation) return;

        _deviceRotation = candidate;
        MainThread.BeginInvokeOnMainThread(() => ApplyControlRotation(animate: true));
    }

    private IEnumerable<VisualElement> RotatableViews()
    {
        yield return closeButton;
        yield return flashButton;
        yield return switchCameraButton;
        yield return retakeButton;
        yield return confirmButton;

        foreach (var child in ratioContainer.Children)
            if (child is VisualElement ve)
                yield return ve;
    }

    private void ApplyControlRotation(bool animate)
    {
        double target = _deviceRotation;

        foreach (var view in RotatableViews())
        {
            view.CancelAnimations();
            if (animate)
                _ = view.RotateToAsync(target, 200, Easing.CubicOut);
            else
                view.Rotation = target;
        }
    }

    // ------------------------------------------------------------------
    // Aufloesung / Seitenverhaeltnis
    // ------------------------------------------------------------------

    private static double NormalizeRatio(double w, double h)
        => Math.Max(w, h) / Math.Min(w, h);

    private static IEnumerable<Size> ValidResolutions(CameraInfo camera)
        => camera.SupportedResolutions?.Where(r => r.Width > 0 && r.Height > 0) ?? Enumerable.Empty<Size>();

    /// <summary>
    /// Das beste Format fuer das Geraet:
    /// Android: 16:9, weil der Preview-Stream des Toolkits dort immer 16:9 ist -
    ///          nur so zeigt der Sucher exakt, was aufgenommen wird.
    /// iOS/Windows: das Verhaeltnis der groessten Sensoraufloesung (meist 4:3).
    /// </summary>
    private static double GetBestRatio(CameraInfo camera)
    {
        var valid = ValidResolutions(camera).ToList();
        if (valid.Count == 0) return 4.0 / 3.0;

#if ANDROID
        const double previewRatio = 16.0 / 9.0;
        if (valid.Any(r => Math.Abs(NormalizeRatio(r.Width, r.Height) - previewRatio) < RatioTolerance))
            return previewRatio;
#endif

        var largest = valid.OrderByDescending(r => r.Width * r.Height).First();
        double ratio = NormalizeRatio(largest.Width, largest.Height);

        var nominal = NominalRatios.OrderBy(n => Math.Abs(n.Value - ratio)).First();
        return Math.Abs(nominal.Value - ratio) < RatioTolerance ? nominal.Value : ratio;
    }

    /// <summary>
    /// Loest den Ratio-Wert auf. -1.0 bedeutet "Bildschirmformat". Rueckgabe immer >= 1.0.
    /// </summary>
    private double ResolveUserRatio()
    {
        if (_userSelectedRatio == -1.0)
            return (Width > 0 && Height > 0) ? NormalizeRatio(Width, Height) : 4.0 / 3.0;

        if (_userSelectedRatio <= 0) return 4.0 / 3.0;

        return _userSelectedRatio < 1.0 ? 1.0 / _userSelectedRatio : _userSelectedRatio;
    }

    /// <summary>
    /// Groesste Aufloesung mit passendem Seitenverhaeltnis; sonst die groesste ueberhaupt.
    /// Gibt nie Size.Zero zurueck.
    /// </summary>
    private Size SelectResolution(CameraInfo camera, double userRatio)
    {
        var valid = ValidResolutions(camera).ToList();
        if (valid.Count == 0) return new Size(1920, 1080);

        double target = userRatio == -1.0
            ? ((Width > 0 && Height > 0) ? NormalizeRatio(Width, Height) : 4.0 / 3.0)
            : (userRatio <= 0 ? 4.0 / 3.0 : (userRatio < 1.0 ? 1.0 / userRatio : userRatio));

        var matching = valid
            .Where(r => Math.Abs(NormalizeRatio(r.Width, r.Height) - target) < RatioTolerance)
            .OrderByDescending(r => r.Width * r.Height)
            .ToList();

        return matching.Count > 0
            ? matching[0]
            : valid.OrderByDescending(r => r.Width * r.Height).First();
    }

    private double GetCaptureRatio()
    {
        var res = cameraView.ImageCaptureResolution;
        if (res.Width <= 0 || res.Height <= 0) return 4.0 / 3.0;
        return NormalizeRatio(res.Width, res.Height);
    }

    /// <summary>
    /// Passt die Vorschau in die Flaeche unter der Top-Bar ein und verankert sie oben.
    /// Da die Seite im Hochformat gesperrt ist, ist das praktisch immer der Hochformat-Fall.
    /// </summary>
    private void UpdateCameraLayout()
    {
        double width = previewArea.Width;
        double height = previewArea.Height;
        if (width <= 0 || height <= 0) return;

        bool isPortrait = height > width;
        double userRatio = ResolveUserRatio();
        double target = isPortrait ? (1 / userRatio) : userRatio;

        double finalWidth, finalHeight;
        if ((width / height) > target)
        {
            finalHeight = height;
            finalWidth = height * target;
        }
        else
        {
            finalWidth = width;
            finalHeight = width / target;
        }

        Dispatcher.Dispatch(() =>
        {
            previewHost.WidthRequest = finalWidth;
            previewHost.HeightRequest = finalHeight;
            cameraFrame.WidthRequest = finalWidth;
            cameraFrame.HeightRequest = finalHeight;
            cameraView.WidthRequest = finalWidth;
            cameraView.HeightRequest = finalHeight;

            if (_isInPreviewMode)
                LayoutPreviewImage();
        });
    }

    /// <summary>
    /// Ein Button pro nominalem Verhaeltnis, das die Kamera tatsaechlich anbietet.
    /// </summary>
    private void PopulateRatioButtons(CameraInfo camera)
    {
        var ratios = ValidResolutions(camera)
            .Select(r => NormalizeRatio(r.Width, r.Height))
            .ToList();

        var available = NominalRatios
            .Where(n => ratios.Any(r => Math.Abs(r - n.Value) < RatioTolerance))
            .Select(n => new CameraRatio { Name = n.Name, Value = n.Value })
            .ToList();

        if (available.Count > 0 &&
            !available.Any(r => Math.Abs(r.Value - _userSelectedRatio) < RatioTolerance))
        {
            double current = ResolveUserRatio();
            _userSelectedRatio = available.OrderBy(r => Math.Abs(r.Value - current)).First().Value;
            UpdateCameraLayout();
        }

        Dispatcher.Dispatch(() =>
        {
            BindableLayout.SetItemsSource(ratioContainer, available);
            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI(animate: false);

            // Neu erzeugte Buttons sofort in die aktuelle Ausrichtung bringen.
            ApplyControlRotation(animate: false);
        });
    }

    /// <summary>
    /// Eingeklappt: nur das aktive Format (gelb). Ausgeklappt: alle Formate
    /// nebeneinander; der Blitz-Button wird dann ausgeblendet.
    /// </summary>
    private void UpdateRatioPickerUI(bool animate = true)
    {
        foreach (var child in ratioContainer.Children)
        {
            if (child is Button btn && btn.CommandParameter is double val)
            {
                bool isSelected = Math.Abs(val - _userSelectedRatio) < RatioTolerance;
                bool shouldBeVisible = _isRatioPickerExpanded || isSelected;

                btn.TextColor = isSelected ? Colors.Yellow : Colors.White;

                if (shouldBeVisible)
                {
                    btn.IsVisible = true;
                    if (animate) btn.FadeToAsync(1, 200); else btn.Opacity = 1;
                }
                else
                {
                    btn.IsVisible = false;
                }
            }
        }

        flashButton.IsVisible = !_isInPreviewMode && !_isRatioPickerExpanded;
    }

    // ------------------------------------------------------------------
    // Zoom
    // ------------------------------------------------------------------

    /// <summary>
    /// Versucht nach dem Start echte Zoom-Grenzen zu bekommen. Liefert das Toolkit
    /// weiterhin 1 - 1, bleibt der Slider ausgeblendet.
    /// </summary>
    private async Task ConfigureZoomAsync(CameraInfo camera)
    {
        ApplyZoomRange(camera);
        if (_isZoomSupported) return;

        try
        {
            var services = cameraView.Handler?.MauiContext?.Services;
            if (services?.GetService(typeof(ICameraProvider)) is not ICameraProvider provider)
            {
                Log("ICameraProvider nicht gefunden - Zoom bleibt deaktiviert.");
                return;
            }

            await provider.RefreshAvailableCameras(CancellationToken.None);
            var refreshed = provider.AvailableCameras;
            var match = refreshed?.FirstOrDefault(c => c.Position == camera.Position && c.Name == camera.Name);

            if (match == null || match.MaximumZoomFactor <= match.MinimumZoomFactor)
            {
                Log($"Zoom nach Refresh weiterhin {match?.MinimumZoomFactor} - {match?.MaximumZoomFactor}.");
                return;
            }

            _cameras = refreshed!;
            cameraView.SelectedCamera = match;
            cameraView.ImageCaptureResolution = SelectResolution(match, _userSelectedRatio);
            ApplyZoomRange(match);
            Log($"Zoom nach Refresh: {match.MinimumZoomFactor} - {match.MaximumZoomFactor}");
        }
        catch (Exception ex)
        {
            Log($"Zoom-Refresh fehlgeschlagen: {ex.Message}");
        }
    }

    private void ApplyZoomRange(CameraInfo camera)
    {
        _suppressZoomEvents = true;
        try
        {
            _isZoomSupported = camera.MaximumZoomFactor > camera.MinimumZoomFactor;

            if (_isZoomSupported)
            {
                customZoomSlider.Minimum = camera.MinimumZoomFactor;
                customZoomSlider.Maximum = Math.Min(camera.MaximumZoomFactor, 10);
                customZoomSlider.Value = camera.MinimumZoomFactor;
            }

            cameraView.ZoomFactor = camera.MinimumZoomFactor;
        }
        finally
        {
            _suppressZoomEvents = false;
        }
    }

    private void OnZoomSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (_suppressZoomEvents || !_isZoomSupported) return;

        cameraView.ZoomFactor = (float)e.NewValue;
        ShowZoomSliderWithTimeout();
    }

    private async void ShowZoomSliderWithTimeout()
    {
        _zoomTimerCts?.Cancel();
        _zoomTimerCts = new CancellationTokenSource();
        var token = _zoomTimerCts.Token;

        try
        {
            customZoomSlider.IsVisible = true;
            await customZoomSlider.FadeToAsync(1, 200);
            await Task.Delay(3000, token);
            await customZoomSlider.FadeToAsync(0, 500);
            customZoomSlider.IsVisible = false;
        }
        catch (OperationCanceledException) { }
    }

    // ------------------------------------------------------------------
    // Aufnahme
    // ------------------------------------------------------------------

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        if (_isCapturing) return;
        _isCapturing = true;

        // Ausrichtung im Moment des Ausloesens festhalten.
        int captureRotation = _deviceRotation;

        var flashTask = FlashAsync();

        var tcs = new TaskCompletionSource<Stream?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureTcs = tcs;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var timeoutRegistration = cts.Token.Register(() => tcs.TrySetResult(null));

        try
        {
            _ = RunCaptureAsync(tcs, cts.Token);

            using var stream = await tcs.Task;
            if (stream == null)
            {
                Log(cts.IsCancellationRequested ? "Capture-Timeout." : "Capture ohne Ergebnis.");
                return;
            }

            _tempFilePath = await SavePhotoToCache(stream, ResolveUserRatio(), GetCaptureRatio(), captureRotation);

            ShowCapturedImage(_tempFilePath!, captureRotation);
            ToggleUI(isPreview: true);
            cameraView.StopCameraPreview();
        }
        catch (Exception ex)
        {
            Log($"Capture error: {ex.Message}");
            await RestartPreview();
        }
        finally
        {
            _captureTcs = null;
            await flashTask;
            flashOverlay.IsVisible = false;
            _isCapturing = false;
        }
    }

    private async Task RunCaptureAsync(TaskCompletionSource<Stream?> tcs, CancellationToken token)
    {
        try
        {
            await cameraView.CaptureImage(token);
        }
        catch (Exception ex)
        {
            Log($"CaptureImage: {ex.GetType().Name}: {ex.Message}");
            tcs.TrySetResult(null);
        }
    }

    private async Task FlashAsync()
    {
        flashOverlay.Opacity = 1;
        flashOverlay.IsVisible = true;
        await flashOverlay.FadeToAsync(0, 250);
        flashOverlay.IsVisible = false;
    }

    private void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
        => _captureTcs?.TrySetResult(e.Media);

    private void OnMediaCaptureFailed(object? sender, MediaCaptureFailedEventArgs e)
    {
        Log($"Capture failed: {e.FailureReason}");
        _captureTcs?.TrySetResult(null);
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream, double targetRatio, double captureRatio, int deviceRotation)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"Cap_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        using (var fs = File.Create(path))
            await photoStream.CopyToAsync(fs);

        await Task.Run(() => NormalizeImage(path, targetRatio, captureRatio, deviceRotation));

        return path;
    }

    // ------------------------------------------------------------------
    // Bildnachbearbeitung (EXIF, Drehung, Zuschnitt)
    // ------------------------------------------------------------------

    /// <summary>
    /// 1. EXIF-Ausrichtung in die Pixel uebernehmen.
    /// 2. Fehlende Drehung ergaenzen (Seite ist auf Hochformat gesperrt -
    ///    je nach Plattform dreht das Toolkit das Querformatbild nicht selbst).
    /// 3. Bei Bedarf auf das Zielverhaeltnis zuschneiden.
    /// Die Datei wird nur neu geschrieben, wenn sich tatsaechlich etwas geaendert hat.
    /// </summary>
    private static void NormalizeImage(string path, double targetRatio, double captureRatio, int deviceRotation)
    {
        try
        {
            SKEncodedOrigin origin;
            SKBitmap? bitmap;

            using (var codec = SKCodec.Create(path))
            {
                if (codec == null) return;
                origin = codec.EncodedOrigin;
                bitmap = SKBitmap.Decode(codec);
            }

            if (bitmap == null) return;

            try
            {
                bool changed = false;

                if (origin != SKEncodedOrigin.TopLeft)
                {
                    Replace(ref bitmap, ApplyOrigin(bitmap, origin));
                    changed = true;
                }

                int missing = GetMissingRotation(bitmap.Width, bitmap.Height, deviceRotation);
                if (missing != 0)
                {
                    Replace(ref bitmap, Transform(bitmap, missing));
                    changed = true;
                }

                if (Math.Abs(captureRatio - targetRatio) > RatioTolerance)
                {
                    var cropped = CropToRatio(bitmap, targetRatio);
                    if (cropped != null)
                    {
                        Replace(ref bitmap, cropped);
                        changed = true;
                    }
                }

                if (!changed) return;

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoQuality);
                using var fs = File.Create(path);
                data.SaveTo(fs);
            }
            finally
            {
                bitmap.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log($"NormalizeImage failed: {ex.Message}");
        }
    }

    private static void Replace(ref SKBitmap current, SKBitmap next)
    {
        if (ReferenceEquals(current, next)) return;
        current.Dispose();
        current = next;
    }

    /// <summary>
    /// Wurde im Querformat ausgeloest, das Bild ist aber hochkant,
    /// hat das Toolkit nicht gedreht -> Gegenrotation liefern.
    /// Bei 1:1 ist das nicht erkennbar; dann wird angenommen, dass nicht gedreht wurde.
    /// </summary>
    private static int GetMissingRotation(int width, int height, int deviceRotation)
    {
        if (deviceRotation == 0) return 0;

        bool isLandscape = width > height;
        if (width == height || !isLandscape)
            return -deviceRotation;

        return 0;
    }

    private static SKBitmap ApplyOrigin(SKBitmap src, SKEncodedOrigin origin) => origin switch
    {
        SKEncodedOrigin.TopRight => Transform(src, 0, flipH: true),
        SKEncodedOrigin.BottomRight => Transform(src, 180),
        SKEncodedOrigin.BottomLeft => Transform(src, 180, flipH: true),
        SKEncodedOrigin.LeftTop => Transform(src, 90, flipH: true),
        SKEncodedOrigin.RightTop => Transform(src, 90),
        SKEncodedOrigin.RightBottom => Transform(src, -90, flipH: true),
        SKEncodedOrigin.LeftBottom => Transform(src, -90),
        _ => src
    };

    /// <summary>
    /// Dreht (im Uhrzeigersinn, Vielfache von 90) und spiegelt optional horizontal.
    /// </summary>
    private static SKBitmap Transform(SKBitmap src, int degrees, bool flipH = false)
    {
        bool swap = Math.Abs(degrees) % 180 == 90;
        int w = swap ? src.Height : src.Width;
        int h = swap ? src.Width : src.Height;

        var dst = new SKBitmap(w, h, src.ColorType, src.AlphaType);
        using var canvas = new SKCanvas(dst);
        using var image = SKImage.FromBitmap(src);

        canvas.Translate(w / 2f, h / 2f);
        if (flipH) canvas.Scale(-1, 1);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-src.Width / 2f, -src.Height / 2f);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return dst;
    }

    /// <summary>
    /// Schneidet mittig auf das gewuenschte Seitenverhaeltnis zu (>= 1.0).
    /// Gibt null zurueck, wenn nichts zu tun ist.
    /// </summary>
    private static SKBitmap? CropToRatio(SKBitmap original, double resolvedRatio)
    {
        if (resolvedRatio <= 0) return null;

        bool isLandscape = original.Width >= original.Height;
        double longSide = Math.Max(original.Width, original.Height);
        double shortSide = Math.Min(original.Width, original.Height);
        double currentRatio = longSide / shortSide;

        if (Math.Abs(currentRatio - resolvedRatio) < 0.02) return null;

        int newLong, newShort;
        if (currentRatio > resolvedRatio)
        {
            newShort = (int)shortSide;
            newLong = (int)Math.Round(shortSide * resolvedRatio);
        }
        else
        {
            newLong = (int)longSide;
            newShort = (int)Math.Round(longSide / resolvedRatio);
        }

        int newWidth = isLandscape ? newLong : newShort;
        int newHeight = isLandscape ? newShort : newLong;
        int left = (original.Width - newWidth) / 2;
        int top = (original.Height - newHeight) / 2;

        // ExtractSubset teilt sich den Speicher mit dem Original -> Kopie erzeugen.
        using var subset = new SKBitmap();
        if (!original.ExtractSubset(subset, new SKRectI(left, top, left + newWidth, top + newHeight)))
            return null;

        return subset.Copy();
    }

    // ------------------------------------------------------------------
    // Vorschau des aufgenommenen Bildes
    // ------------------------------------------------------------------

    private void ShowCapturedImage(string path, int rotation)
    {
        _previewRotation = rotation;
        previewImage.Source = ImageSource.FromFile(path);
        LayoutPreviewImage();
    }

    /// <summary>
    /// Querformatbilder werden gedreht dargestellt - so sieht der Benutzer das Foto
    /// genau so, wie er es im Sucher gesehen hat. Dafuer bekommt das Image vertauschte
    /// Masse und wird zentriert um 90 Grad gedreht.
    /// </summary>
    private void LayoutPreviewImage()
    {
        double w = previewHost.Width > 0 ? previewHost.Width : previewHost.WidthRequest;
        double h = previewHost.Height > 0 ? previewHost.Height : previewHost.HeightRequest;
        if (w <= 0 || h <= 0) return;

        bool rotated = _previewRotation != 0;
        var bounds = rotated
            ? new Rect((w - h) / 2, (h - w) / 2, h, w)
            : new Rect(0, 0, w, h);

        AbsoluteLayout.SetLayoutFlags(previewImage, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(previewImage, bounds);
        previewImage.Rotation = _previewRotation;
    }

    private async Task RestartPreview()
    {
        try
        {
            cameraView.StopCameraPreview();
            await Task.Delay(100);
            await cameraView.StartCameraPreview(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log($"Restart error: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // UI-Aktionen
    // ------------------------------------------------------------------

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        _currentFlashMode = _currentFlashMode switch
        {
            CameraFlashMode.Auto => CameraFlashMode.On,
            CameraFlashMode.On => CameraFlashMode.Off,
            _ => CameraFlashMode.Auto
        };

        cameraView.CameraFlashMode = _currentFlashMode;
        SettingsService.Instance.FlashMode = (int)_currentFlashMode;
        UpdateFlashButtonUI();
    }

    private void UpdateFlashButtonUI()
    {
        flashButton.Text = _currentFlashMode switch
        {
            CameraFlashMode.On => MaterialIcons.Flash_on,
            CameraFlashMode.Off => MaterialIcons.Flash_off,
            _ => MaterialIcons.Flash_auto
        };

        flashButton.TextColor = _currentFlashMode == CameraFlashMode.Off ? Colors.White : Colors.Yellow;
    }

    /// <summary>
    /// Manuelle Auswahl gilt nur fuer die laufende Sitzung.
    /// </summary>
    private void OnRatioClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not double val) return;

        if (!_isRatioPickerExpanded)
        {
            _isRatioPickerExpanded = true;
            UpdateRatioPickerUI();
            return;
        }

        _userSelectedRatio = val;
        _isRatioPickerExpanded = false;
        UpdateRatioPickerUI();

        if (cameraView.SelectedCamera is { } cam)
        {
            cameraView.ImageCaptureResolution = SelectResolution(cam, val);
            UpdateCameraLayout();
        }
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        previewImage.Source = null;

        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);

        _tempFilePath = string.Empty;

        ToggleUI(isPreview: false);
        await RestartPreview();
    }

    private async void OnConfirmClicked(object s, EventArgs e)
    {
        CameraResultService.SetResult(!string.IsNullOrEmpty(_tempFilePath) ? new FileResult(_tempFilePath) : null);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCloseClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnSwitchCameraClicked(object sender, EventArgs e)
    {
        if (_cameras.Count <= 1)
        {
            Log($"Kamerawechsel nicht möglich: {_cameras.Count} Kamera(s) bekannt.");
            return;
        }

        var currentPosition = cameraView.SelectedCamera?.Position ?? CameraPosition.Rear;
        var next = _cameras.FirstOrDefault(c => c.Position != currentPosition) ?? _cameras[0];

        await ApplyCameraAsync(next, isSwitch: true);
    }

    private void ToggleUI(bool isPreview)
    {
        _isInPreviewMode = isPreview;

        previewImageLayer.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
        ratioContainer.IsVisible = !isPreview;

        if (isPreview)
        {
            _isRatioPickerExpanded = false;
            customZoomSlider.IsVisible = false;
        }
        else
        {
            _previewRotation = 0;
            previewImage.Rotation = 0;
        }

        flashButton.IsVisible = !isPreview && !_isRatioPickerExpanded;
    }

    private void OnContainerTapped(object sender, EventArgs e)
    {
        if (_isRatioPickerExpanded)
        {
            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI();
        }

        if (_isZoomSupported) ShowZoomSliderWithTimeout();
    }
}

public static class CameraResultService
{
    private static TaskCompletionSource<FileResult?>? _tcs;

    public static Task<FileResult?> WaitForCaptureAsync()
    {
        _tcs = new TaskCompletionSource<FileResult?>();
        return _tcs.Task;
    }

    public static void SetResult(FileResult? result) => _tcs?.TrySetResult(result);
}

public class CameraRatio
{
    public string? Name { get; set; }
    public double Value { get; set; }
}
