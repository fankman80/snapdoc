using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
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
    private CancellationTokenSource? _zoomTimerCts;

    private IReadOnlyList<CameraInfo> _cameras = Array.Empty<CameraInfo>();
    private bool _isStarted = false;
    private bool _isCapturing = false;

    // MediaCaptured feuert asynchron; darueber wird das Ergebnis in den
    // Klick-Handler zurueckgefuehrt.
    private TaskCompletionSource<Stream?>? _captureTcs;

    public CameraPage()
    {
        InitializeComponent();
    }

    // ------------------------------------------------------------------
    // Lebenszyklus
    // ------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();

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
        CameraResultService.SetResult(null);
        cameraView.StopCameraPreview();
        _isStarted = false;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        UpdateCameraLayout(width, height);
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
            UpdateCameraLayout(Width, Height);

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
    /// iOS/Windows: das Verhaeltnis der groessten Sensoraufloesung (meist 4:3),
    ///          dort folgt die Preview der Aufnahme.
    /// Faellt auf das Verhaeltnis der groessten Aufloesung zurueck, falls die
    /// Kamera kein 16:9 anbietet.
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

        // Auf den naechsten nominalen Wert einrasten, damit der passende Button markiert wird.
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
    /// Rahmen und Preview bekommen dieselbe Groesse im gewaehlten Verhaeltnis.
    /// </summary>
    private void UpdateCameraLayout(double width, double height)
    {
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
            cameraFrame.WidthRequest = finalWidth;
            cameraFrame.HeightRequest = finalHeight;
            cameraFrame.HorizontalOptions = LayoutOptions.Center;
            cameraFrame.VerticalOptions = LayoutOptions.Center;

            cameraView.WidthRequest = finalWidth;
            cameraView.HeightRequest = finalHeight;
            cameraView.HorizontalOptions = LayoutOptions.Center;
            cameraView.VerticalOptions = LayoutOptions.Center;
        });
    }

    /// <summary>
    /// Ein Button pro nominalem Verhaeltnis, das die Kamera tatsaechlich anbietet.
    /// Der Button-Wert ist der nominale Wert (z. B. exakt 16/9), nicht der einer
    /// zufaelligen Einzelaufloesung. Exotische Formate wie 2304x1040 (2.22) werden
    /// nicht mehr faelschlich als "16:9" einsortiert.
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
            UpdateCameraLayout(Width, Height);
        }

        Dispatcher.Dispatch(() =>
        {
            BindableLayout.SetItemsSource(ratioContainer, available);
            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI(animate: false);
        });
    }

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
                    if (animate) btn.FadeToAsync(0.8, 250); else btn.Opacity = 0.8;
                }
                else
                {
                    btn.IsVisible = false;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Zoom
    // ------------------------------------------------------------------

    /// <summary>
    /// Unter Android liest das Toolkit die Zoom-Grenzen beim Aufzaehlen der Kameras
    /// aus CameraX. Zu diesem Zeitpunkt ist die Kamera noch nicht gebunden, CameraX
    /// hat noch keinen Zoom-Zustand, und die Grenzen landen als 1 - 1 im CameraInfo.
    /// Deshalb: nach dem Start die Kameraliste ueber den ICameraProvider neu einlesen
    /// und - falls jetzt echte Grenzen vorliegen - das aktualisierte CameraInfo setzen.
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

            // Neues CameraInfo setzen, damit ZoomFactor nicht auf das alte Maximum 1 begrenzt wird.
            // Reihenfolge wie beim Start: erst Kamera, dann Aufloesung.
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

    /// <summary>
    /// Der weisse Screen entstand, weil das Blitz-Overlay bis zum Ende der Aufnahme
    /// sichtbar blieb und der Timeout erst NACH "await CaptureImage" startete. Hing
    /// CaptureImage, blieb das Overlay fuer immer stehen und _isCapturing auf true.
    /// Jetzt: kurzer, unabhaengiger Blitz; Timeout umfasst den ganzen Vorgang.
    /// </summary>
    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        if (_isCapturing) return;
        _isCapturing = true;

        var flashTask = FlashAsync();
        var tcs = new TaskCompletionSource<Stream?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureTcs = tcs;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var timeoutRegistration = cts.Token.Register(() => tcs.TrySetResult(null));

        try
        {
            // Nicht direkt awaiten: das Ergebnis kommt ueber MediaCaptured bzw. den Timeout.
            _ = RunCaptureAsync(tcs, cts.Token);

            using var stream = await tcs.Task;
            if (stream == null)
            {
                Log(cts.IsCancellationRequested ? "Capture-Timeout." : "Capture ohne Ergebnis.");
                return;
            }

            _tempFilePath = await SavePhotoToCache(stream, ResolveUserRatio(), GetCaptureRatio());
            previewImage.Source = ImageSource.FromFile(_tempFilePath);

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

    private static async Task<string?> SavePhotoToCache(Stream photoStream, double targetRatio, double captureRatio)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"Cap_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        using (var fs = File.Create(path))
            await photoStream.CopyToAsync(fs);

        // Nur zuschneiden, wenn die Kamera kein passendes Seitenverhaeltnis anbot.
        if (Math.Abs(captureRatio - targetRatio) > RatioTolerance)
            await Task.Run(() => CropToRatio(path, targetRatio));

        return path;
    }

    /// <summary>
    /// Schneidet das Bild mittig auf das gewuenschte Seitenverhaeltnis zu.
    /// Erwartet einen aufgeloesten Wert >= 1.0 (siehe ResolveUserRatio).
    /// </summary>
    private static void CropToRatio(string path, double resolvedRatio)
    {
        try
        {
            if (resolvedRatio <= 0) return;

            using var original = SKBitmap.Decode(path);
            if (original == null) return;

            bool isLandscape = original.Width >= original.Height;

            double longSide = Math.Max(original.Width, original.Height);
            double shortSide = Math.Min(original.Width, original.Height);
            double currentRatio = longSide / shortSide;

            if (Math.Abs(currentRatio - resolvedRatio) < 0.02) return;

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

            using var cropped = new SKBitmap(newWidth, newHeight);
            if (!original.ExtractSubset(cropped, new SKRectI(left, top, left + newWidth, top + newHeight)))
                return;

            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoQuality);
            using var fs = File.Create(path);
            data.SaveTo(fs);
        }
        catch (Exception ex)
        {
            Log($"CropToRatio failed: {ex.Message}");
        }
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
    }

    /// <summary>
    /// Manuelle Auswahl gilt nur fuer die laufende Sitzung. Beim naechsten Start
    /// oder Kamerawechsel wird wieder automatisch das beste Format gewaehlt.
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
            UpdateCameraLayout(Width, Height);
        }
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
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

        // Nach Position wechseln statt per IndexOf - SelectedCamera kann ein anderes
        // CameraInfo-Objekt sein als der Eintrag in der Liste.
        var currentPosition = cameraView.SelectedCamera?.Position ?? CameraPosition.Rear;
        var next = _cameras.FirstOrDefault(c => c.Position != currentPosition) ?? _cameras[0];

        await ApplyCameraAsync(next, isSwitch: true);
    }

    private void ToggleUI(bool isPreview)
    {
        previewImage.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
        ratioContainer.IsVisible = !isPreview;
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
