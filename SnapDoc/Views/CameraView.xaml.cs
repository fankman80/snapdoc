using Camera.MAUI;
using SkiaSharp;
using SnapDoc.Services;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? _tempFilePath = string.Empty;
    private Size _optimalSize;
    private double _userSelectedRatio = SettingsService.Instance.CaptureRatio;
    private FlashMode _currentFlashMode = (FlashMode)SettingsService.Instance.FlashMode;
    private bool _isZoomSupported = false;
    private bool _suppressZoomEvents = false;
    private bool _isRatioPickerExpanded = false;
    private CancellationTokenSource? _zoomTimerCts;
    private bool _camerasReady = false;
    private bool _sizeReady = false;
    private bool _isStarted = false;

    public CameraView()
    {
        InitializeComponent();
        cameraView.CamerasLoaded += async (s, e) => await OnCamerasLoaded();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraResultService.SetResult(null);
        _ = cameraView.StopCameraAsync();
    }

    // ------------------------------------------------------------------
    // Initialisierung
    // ------------------------------------------------------------------

    private async Task OnCamerasLoaded()
    {
        if (cameraView.Cameras.Count == 0) return;

        switchCameraButton.IsVisible = cameraView.Cameras.Count > 1;

        cameraView.Camera = cameraView.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Back)
                            ?? cameraView.Cameras.First();

        _camerasReady = true;
        await TryStartCameraAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0) return;

        _sizeReady = true;

        if (_isStarted)
        {
            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(100);
                await RestartPreview();
            });
            return;
        }

        Dispatcher.Dispatch(async () => await TryStartCameraAsync());
    }

    /// <summary>
    /// Startet die Kamera erst, wenn Kameraliste UND Layoutgroesse bekannt sind.
    /// Ohne dieses Gating liefert GetOptimalSize() unter iOS haeufig Size(0,0),
    /// weil OnSizeAllocated vor CamerasLoaded feuert -> die Library faellt dann
    /// auf die View-Aufloesung x Display-Density zurueck.
    /// </summary>
    private async Task TryStartCameraAsync()
    {
        if (!_camerasReady || !_sizeReady || _isStarted) return;
        _isStarted = true;

        await InitializeCameraSelection();
    }

    private async Task InitializeCameraSelection()
    {
        _optimalSize = GetOptimalSize(_userSelectedRatio);

        if (_optimalSize.Width <= 0 || _optimalSize.Height <= 0)
        {
            var fallback = cameraView.Camera?.AvailableResolutions?
                .OrderByDescending(r => r.Width * r.Height)
                .FirstOrDefault();

            if (fallback is null or { Width: <= 0 })
            {
                Debug.WriteLine("[CAM] Keine verwertbare Aufloesung gefunden.");
                _isStarted = false;
                return;
            }

            _optimalSize = fallback.Value;
        }

        Debug.WriteLine($"[CAM] StartCameraAsync mit {_optimalSize.Width}x{_optimalSize.Height}");

        if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
        {
            ConfigureZoom();
            cameraView.FlashMode = _currentFlashMode;
            UpdateFlashButtonUI();
            PopulateRatioButtons();
            UpdateCameraLayout(Width, Height);
        }
        else
        {
            Debug.WriteLine("[CAM] StartCameraAsync fehlgeschlagen.");
            _isStarted = false;
        }
    }

    // ------------------------------------------------------------------
    // Aufloesung / Seitenverhaeltnis
    // ------------------------------------------------------------------

    /// <summary>
    /// Normalisiert ein Seitenverhaeltnis immer auf einen Wert >= 1.0 (Landscape-Sicht).
    /// </summary>
    private static double NormalizeRatio(double w, double h)
        => Math.Max(w, h) / Math.Min(w, h);

    /// <summary>
    /// Loest den gespeicherten Ratio-Wert auf. -1.0 bedeutet "Bildschirmformat".
    /// Rueckgabe immer >= 1.0.
    /// </summary>
    private double ResolveUserRatio()
    {
        if (_userSelectedRatio == -1.0)
        {
            if (Width <= 0 || Height <= 0) return 4.0 / 3.0;
            return NormalizeRatio(Width, Height);
        }

        if (_userSelectedRatio <= 0) return 4.0 / 3.0;
        return _userSelectedRatio < 1.0 ? 1.0 / _userSelectedRatio : _userSelectedRatio;
    }

    /// <summary>
    /// Ermittelt die Aufnahmeaufloesung.
    /// Android: immer das native Sensorformat mit maximaler Pixelzahl. Die Preview
    /// folgt unter Android der uebergebenen Capture-Size NICHT, deshalb wuerde jedes
    /// abweichende Seitenverhaeltnis zu einem sichtbaren Zoom fuehren. Der gewuenschte
    /// Zuschnitt erfolgt optisch ueber den Clip-Rahmen und real in CropToRatio.
    /// iOS/Windows: die uebergebene Size gilt auch fuer die Preview.
    /// </summary>
    private Size GetOptimalSize(double? targetRatio = null)
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null || available.Count == 0) return new Size(0, 0);

#if ANDROID
        return available.OrderByDescending(r => r.Width * r.Height).First();
#else
        double finalTarget = (targetRatio == -1.0)
            ? ((Width > 0 && Height > 0) ? NormalizeRatio(Width, Height) : 4.0 / 3.0)
            : (targetRatio is < 1.0 ? 1.0 / targetRatio.Value : targetRatio ?? 1.33);

        return available
            .GroupBy(r => Math.Round(NormalizeRatio(r.Width, r.Height), 2))
            .OrderBy(g => Math.Abs(g.Key - finalTarget))
            .First()
            .OrderByDescending(r => r.Width * r.Height)
            .First();
#endif
    }

    private async Task RestartPreview(Size? specificSize = null)
    {
        try
        {
            await cameraView.StopCameraAsync();
            cameraView.IsVisible = false;

            if (specificSize.HasValue && specificSize.Value.Width > 0)
                _optimalSize = specificSize.Value;

            await Task.Delay(150);

            if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
            {
                cameraView.FlashMode = _currentFlashMode;
                ConfigureZoom();
                UpdateCameraLayout(Width, Height);
                cameraView.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Restart Error: {ex.Message}");
        }
    }

    private void UpdateCameraLayout(double width, double height)
    {
        if (cameraView?.Camera == null || _optimalSize.Width <= 0) return;
        if (width <= 0 || height <= 0) return;

        bool isPortrait = height > width;
        double userRatio = ResolveUserRatio();
        double target = isPortrait ? (1 / userRatio) : userRatio;

        // Rahmen in den verfuegbaren Platz einpassen (Contain).
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

    private void PopulateRatioButtons()
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null) return;

        // Gleiche Normalisierung wie in GetOptimalSize, sonst passen die Buttons
        // nicht zu _userSelectedRatio (Portrait-Aufloesungen ergaeben < 1.0).
        var uniqueRatios = available
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

    /// <summary>
    /// Das Zuweisen von Minimum/Maximum loest ValueChanged aus, deshalb werden die
    /// Events waehrenddessen unterdrueckt - sonst wird ein ungewollter ZoomFactor
    /// in die Kamera geschrieben.
    /// </summary>
    private void ConfigureZoom()
    {
        _suppressZoomEvents = true;
        try
        {
            _isZoomSupported = cameraView.MaxZoomFactor > cameraView.MinZoomFactor;

            if (_isZoomSupported)
            {
                customZoomSlider.Minimum = cameraView.MinZoomFactor;
                customZoomSlider.Maximum = Math.Min(cameraView.MaxZoomFactor, 10);
                customZoomSlider.Value = cameraView.MinZoomFactor;
            }

            cameraView.ZoomFactor = cameraView.MinZoomFactor;
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
        if (flashOverlay.IsVisible) return;

        try
        {
            flashOverlay.IsVisible = true;
            flashOverlay.Opacity = 1;

            using var stream = await cameraView.TakePhotoAsync();
            if (stream != null)
            {
                _tempFilePath = await SavePhotoToCache(stream, ResolveUserRatio());
                previewImage.Source = ImageSource.FromFile(_tempFilePath);
                ToggleUI(isPreview: true);
                await cameraView.StopCameraAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            await RestartPreview();
        }
        finally
        {
            await flashOverlay.FadeToAsync(0, 200);
            flashOverlay.IsVisible = false;
        }
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream, double resolvedRatio)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"Cap_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg");

        using (var fs = File.Create(path))
            await photoStream.CopyToAsync(fs);

#if ANDROID
        // Unter Android wird im nativen Sensorformat aufgenommen (maximales FOV und
        // maximale Pixelzahl). Der Zuschnitt wird hier in der Pixelebene nachgezogen
        // und entspricht exakt dem, was der Clip-Rahmen im Sucher gezeigt hat.
        await Task.Run(() => CropToRatio(path, resolvedRatio));
#endif
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

            if (Math.Abs(currentRatio - resolvedRatio) < 0.02) return; // passt bereits

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

    // ------------------------------------------------------------------
    // UI-Aktionen
    // ------------------------------------------------------------------

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        _currentFlashMode = cameraView.FlashMode switch
        {
            FlashMode.Auto => FlashMode.Enabled,
            FlashMode.Enabled => FlashMode.Disabled,
            _ => FlashMode.Auto
        };

        cameraView.FlashMode = _currentFlashMode;
        SettingsService.Instance.FlashMode = (int)_currentFlashMode;
        UpdateFlashButtonUI();
    }

    private void UpdateFlashButtonUI()
    {
        if (liveButtons.Children.FirstOrDefault() is Button btn)
            btn.Text = _currentFlashMode switch
            {
                FlashMode.Enabled => MaterialIcons.Flash_on,
                FlashMode.Disabled => MaterialIcons.Flash_off,
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

#if ANDROID
        // Kein Neustart noetig: das Sensorformat bleibt, nur der sichtbare
        // Ausschnitt und der spaetere Zuschnitt aendern sich.
        UpdateCameraLayout(Width, Height);
        await Task.CompletedTask;
#else
        await RestartPreview(GetOptimalSize(val));
#endif
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
        if (cameraView.Cameras.Count <= 1) return;

        int nextIndex = (cameraView.Cameras.IndexOf(cameraView.Camera) + 1) % cameraView.Cameras.Count;

        await cameraView.StopCameraAsync();
        cameraView.Camera = cameraView.Cameras[nextIndex];

        // Aufloesungen sind kameraspezifisch -> neu ermitteln, nie die alte weiterverwenden.
        var newSize = GetOptimalSize(_userSelectedRatio);
        await RestartPreview(newSize);
        PopulateRatioButtons();
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
