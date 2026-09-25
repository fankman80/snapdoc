using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using SkiaSharp;
using SnapDoc.Services;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? _tempFilePath = string.Empty;

    private double _userSelectedRatio = SettingsService.Instance.CaptureRatio;
    private CameraFlashMode _currentFlashMode = (CameraFlashMode)SettingsService.Instance.FlashMode;

    private bool _isZoomSupported = false;
    private bool _suppressZoomEvents = false;
    private bool _isRatioPickerExpanded = false;
    private CancellationTokenSource? _zoomTimerCts;

    private IReadOnlyList<CameraInfo> _cameras = Array.Empty<CameraInfo>();
    private bool _isStarted = false;

    // Ergebnis des Capture-Vorgangs. MediaCaptured feuert asynchron, deshalb wird
    // der aufrufende Klick-Handler darueber wieder zusammengefuehrt.
    private TaskCompletionSource<Stream?>? _captureTcs;

    public CameraView()
    {
        InitializeComponent();
    }

    // ------------------------------------------------------------------
    // Lebenszyklus
    // ------------------------------------------------------------------

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await InitializeCameraAsync();
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

    private async Task InitializeCameraAsync()
    {
        if (_isStarted) return;

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            Debug.WriteLine("[CAM] Kameraberechtigung fehlt.");
            return;
        }

        if (!cameraView.IsAvailable)
        {
            Debug.WriteLine("[CAM] Keine Kamera verfuegbar.");
            return;
        }

        _cameras = await cameraView.GetAvailableCameras(CancellationToken.None);
        if (_cameras.Count == 0) return;

        switchCameraButton.IsVisible = _cameras.Count > 1;

        var camera = _cameras.FirstOrDefault(c => c.Position == CameraPosition.Rear) ?? _cameras[0];
        await ApplyCameraAsync(camera);

        _isStarted = true;
    }

    /// <summary>
    /// Setzt Kamera, Aufnahmeaufloesung, Blitz und Zoom in der richtigen Reihenfolge.
    /// ImageCaptureResolution MUSS nach SelectedCamera gesetzt werden, sonst geht der
    /// Wert beim Rekonfigurieren der Session verloren.
    /// </summary>
    private async Task ApplyCameraAsync(CameraInfo camera)
    {
        cameraView.StopCameraPreview();

        cameraView.SelectedCamera = camera;
        cameraView.ImageCaptureResolution = SelectResolution(camera, _userSelectedRatio);

        Debug.WriteLine($"[CAM] {camera.Name} -> {cameraView.ImageCaptureResolution.Width}x{cameraView.ImageCaptureResolution.Height}");

        ConfigureZoom(camera);
        cameraView.CameraFlashMode = _currentFlashMode;
        UpdateFlashButtonUI();

        PopulateRatioButtons(camera);
        UpdateCameraLayout(Width, Height);

        await cameraView.StartCameraPreview(CancellationToken.None);
    }

    // ------------------------------------------------------------------
    // Aufloesung / Seitenverhaeltnis
    // ------------------------------------------------------------------

    private static double NormalizeRatio(double w, double h)
        => Math.Max(w, h) / Math.Min(w, h);

    /// <summary>
    /// Loest den gespeicherten Ratio-Wert auf. -1.0 bedeutet "Bildschirmformat".
    /// Rueckgabe immer >= 1.0.
    /// </summary>
    private double ResolveUserRatio()
    {
        if (_userSelectedRatio == -1.0)
            return (Width > 0 && Height > 0) ? NormalizeRatio(Width, Height) : 4.0 / 3.0;

        if (_userSelectedRatio <= 0) return 4.0 / 3.0;
        return _userSelectedRatio < 1.0 ? 1.0 / _userSelectedRatio : _userSelectedRatio;
    }

    /// <summary>
    /// Waehlt die groesste Aufloesung, deren Seitenverhaeltnis dem gewuenschten
    /// entspricht. Gibt es keine passende, wird die groesste ueberhaupt genommen -
    /// der Rest erledigt CropToRatio beim Speichern.
    /// Niemals Size.Zero zurueckgeben: das war unter iOS die Ursache fuer den
    /// Fallback auf Bildschirmaufloesung.
    /// </summary>
    private Size SelectResolution(CameraInfo camera, double userRatio)
    {
        var supported = camera.SupportedResolutions;
        if (supported == null || supported.Count == 0)
            return new Size(1920, 1080);

        double target = userRatio == -1.0
            ? ((Width > 0 && Height > 0) ? NormalizeRatio(Width, Height) : 4.0 / 3.0)
            : (userRatio < 1.0 ? 1.0 / userRatio : userRatio);

        var matching = supported
            .Where(r => r.Width > 0 && r.Height > 0)
            .Where(r => Math.Abs(NormalizeRatio(r.Width, r.Height) - target) < 0.05)
            .OrderByDescending(r => r.Width * r.Height)
            .ToList();

        if (matching.Count > 0) return matching[0];

        return supported.OrderByDescending(r => r.Width * r.Height).First();
    }

    /// <summary>
    /// Das Verhaeltnis, das die Aufnahme tatsaechlich liefert.
    /// </summary>
    private double GetCaptureRatio()
    {
        var res = cameraView.ImageCaptureResolution;
        if (res.Width <= 0 || res.Height <= 0) return 4.0 / 3.0;
        return NormalizeRatio(res.Width, res.Height);
    }

    /// <summary>
    /// Der Rahmen bekommt das gewuenschte Seitenverhaeltnis, die Preview exakt
    /// dieselbe Groesse. Die native Preview beschneidet sich per Center-Crop selbst -
    /// nach derselben Regel wie CropToRatio. Keine zusaetzliche Skalierung, sonst
    /// entsteht ein doppelter Zuschnitt (sichtbar als Zoom im Sucher).
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

    private void PopulateRatioButtons(CameraInfo camera)
    {
        var supported = camera.SupportedResolutions;
        if (supported == null || supported.Count == 0) return;

        var uniqueRatios = supported
            .Where(r => r.Width > 0 && r.Height > 0)
            .Select(r =>
            {
                double ratio = Math.Round(NormalizeRatio(r.Width, r.Height), 2);
                return new { Ratio = ratio, Name = GetRatioName(ratio) };
            })
            .GroupBy(x => x.Name)
            .Select(g => new CameraRatio { Name = g.Key, Value = g.First().Ratio })
            .OrderByDescending(r => r.Value)
            .ToList();

        Dispatcher.Dispatch(() =>
        {
            BindableLayout.SetItemsSource(ratioContainer, uniqueRatios);
            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI(animate: false);
        });
    }

    private static string GetRatioName(double ratio)
    {
        return ratio switch
        {
            >= 1.7 => "16:9",
            >= 1.5 => "3:2",
            >= 1.3 => "4:3",
            >= 1.2 => "5:4",
            _ when Math.Abs(ratio - 1.0) < 0.02 => "1:1",
            _ => ratio.ToString("F2")
        };
    }

    private void UpdateRatioPickerUI(bool animate = true)
    {
        foreach (var child in ratioContainer.Children)
        {
            if (child is Button btn && btn.CommandParameter is double val)
            {
                bool isSelected = Math.Abs(val - _userSelectedRatio) < 0.05;
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

    private void ConfigureZoom(CameraInfo camera)
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
        if (flashOverlay.IsVisible || cameraView.IsCameraBusy) return;

        try
        {
            flashOverlay.IsVisible = true;
            flashOverlay.Opacity = 1;

            _captureTcs = new TaskCompletionSource<Stream?>(TaskCreationOptions.RunContinuationsAsynchronously);

            await cameraView.CaptureImage(CancellationToken.None);

            // Timeout als Schutz, falls weder MediaCaptured noch MediaCaptureFailed feuert.
            var completed = await Task.WhenAny(_captureTcs.Task, Task.Delay(15000));
            if (completed != _captureTcs.Task)
            {
                Debug.WriteLine("[CAM] Capture-Timeout.");
                return;
            }

            using var stream = await _captureTcs.Task;
            if (stream == null) return;

            _tempFilePath = await SavePhotoToCache(stream, ResolveUserRatio(), GetCaptureRatio());
            previewImage.Source = ImageSource.FromFile(_tempFilePath);

            ToggleUI(isPreview: true);
            cameraView.StopCameraPreview();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Capture error: {ex.Message}");
            await RestartPreview();
        }
        finally
        {
            _captureTcs = null;
            await flashOverlay.FadeToAsync(0, 200);
            flashOverlay.IsVisible = false;
        }
    }

    private void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
        => _captureTcs?.TrySetResult(e.Media);

    private void OnMediaCaptureFailed(object? sender, MediaCaptureFailedEventArgs e)
    {
        Debug.WriteLine($"[CAM] Capture failed: {e.FailureReason}");
        _captureTcs?.TrySetResult(null);
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream, double targetRatio, double captureRatio)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"Cap_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        using (var fs = File.Create(path))
            await photoStream.CopyToAsync(fs);

        // Nur zuschneiden, wenn die Kamera kein passendes Seitenverhaeltnis anbot.
        if (Math.Abs(captureRatio - targetRatio) > 0.05)
            await Task.Run(() => CropToRatio(path, targetRatio));

        return path;
    }

    /// <summary>
    /// Schneidet das Bild mittig auf das gewuenschte Seitenverhaeltnis zu.
    /// Erwartet einen bereits aufgeloesten Wert >= 1.0 (siehe ResolveUserRatio).
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
            Debug.WriteLine($"CropToRatio failed: {ex.Message}");
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
            Debug.WriteLine($"Restart error: {ex.Message}");
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

    private async void OnRatioClicked(object sender, EventArgs e)
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

        SettingsService.Instance.CaptureRatio = val;
        SettingsService.Instance.SaveSettings();

        if (cameraView.SelectedCamera is { } cam)
        {
            cameraView.ImageCaptureResolution = SelectResolution(cam, val);
            UpdateCameraLayout(Width, Height);
        }

        await Task.CompletedTask;
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
        if (_cameras.Count <= 1 || cameraView.SelectedCamera is null) return;

        int currentIndex = _cameras.ToList().IndexOf(cameraView.SelectedCamera);
        int nextIndex = (currentIndex + 1) % _cameras.Count;

        await ApplyCameraAsync(_cameras[nextIndex]);
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
