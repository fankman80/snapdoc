using Camera.MAUI;
using SnapDoc.Services;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? _tempFilePath = string.Empty;
    private Size _optimalSize;
    private double _userSelectedRatio = SettingsService.Instance.CaptureRatio;
    private FlashMode _currentFlashMode = (FlashMode)SettingsService.Instance.FlashMode;
    private CancellationTokenSource? _resizeCts;
    private bool _isRatioPickerExpanded = false;
    private CancellationTokenSource? _zoomTimerCts;
    private bool _isZoomSupported = false;

    public CameraView()
    {
        InitializeComponent();
        cameraView.CamerasLoaded += CameraView_CamerasLoaded;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraResultService.SetResult(null);
        _ = cameraView.StopCameraAsync();
    }

    private void CameraView_CamerasLoaded(object? sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 0)
        {
            switchCameraButton.IsVisible = cameraView.Cameras.Count > 1;

            cameraView.Camera = cameraView.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Back)
                                ?? cameraView.Cameras.First();

            MainThread.BeginInvokeOnMainThread(async () => {
                await InitializeCameraSelection();
            });
        }
    }

    private async Task InitializeCameraSelection()
    {
        var photo = GetOptimalSize(_userSelectedRatio);
        _optimalSize = photo;

        if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
        {
            // Prüfen, ob Zoom unterstützt wird
            _isZoomSupported = cameraView.MaxZoomFactor > cameraView.MinZoomFactor;
            if (_isZoomSupported)
            {
                customZoomSlider.Minimum = cameraView.MinZoomFactor;
                customZoomSlider.Maximum = Math.Min(cameraView.MaxZoomFactor, 10);
                customZoomSlider.Value = cameraView.ZoomFactor;
            }
            customZoomSlider.IsVisible = false;
            customZoomSlider.Opacity = 0;

            // Blitzmodus setzen
            cameraView.FlashMode = _currentFlashMode;
            UpdateFlashButtonUI();

            // Ratio-Buttons befüllen
            PopulateRatioButtons();

            // Layout anpassen
            UpdateCameraLayout(this.Width, this.Height);
        }
    }

    private void PopulateRatioButtons()
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null) return;

        var uniqueRatios = available
            .Select(r => new { Ratio = Math.Round((double)r.Width / r.Height, 2), Name = GetRatioName(Math.Round((double)r.Width / r.Height, 2)) })
            .GroupBy(x => x.Name)
            .Select(g => new RatioItem { Name = g.Key, Value = g.First().Ratio })
            .OrderByDescending(r => r.Value)
            .ToList();

        BindableLayout.SetItemsSource(ratioContainer, uniqueRatios);

        MainThread.BeginInvokeOnMainThread(async () => {
            int timeout = 0;
            while (ratioContainer.Children.Count == 0 && timeout < 10)
            {
                await Task.Delay(50);
                timeout++;
            }

            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI(animate: false);
            await ratioContainer.FadeToAsync(1, 200);
        });
    }

    private static string GetRatioName(double ratio)
    {
        if (ratio >= 1.7) return "16:9";
        if (ratio >= 1.5) return "3:2";
        if (ratio >= 1.3) return "4:3";
        if (ratio >= 1.2) return "5:4";
        if (ratio == 1.0) return "1:1";
        return ratio.ToString("F2");
    }

    private Size GetOptimalSize(double? targetRatio = null)
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null || available.Count == 0) return new Size(0, 0);

        double finalTarget;
        if (targetRatio == -1.0)
            finalTarget = Math.Max(this.Width, this.Height) / Math.Min(this.Width, this.Height);
        else
            finalTarget = targetRatio ?? 1.33;

        return available
            .GroupBy(r => Math.Round((double)r.Width / r.Height, 2))
            .OrderBy(g => Math.Abs(g.Key - finalTarget))
            .First()
            .OrderByDescending(r => r.Width)
            .First();
    }

    private async Task RestartPreview(Size? specificSize = null)
    {
        try
        {
            await cameraView.StopCameraAsync();

            // 1. Layout-Reset: Control zwingen, seine Größe zu vergessen
            cameraView.WidthRequest = -1;
            cameraView.HeightRequest = -1;

            // Kurze Pause, damit MAUI das UI-Grid entspannen kann
            await Task.Delay(150);

            // Wenn eine neue Größe (Ratio) mitgegeben wurde, nutzen wir diese
            if (specificSize.HasValue)
                _optimalSize = specificSize.Value;

            if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
            {
                // Blitz und UI wiederherstellen
                cameraView.FlashMode = _currentFlashMode;
                UpdateFlashButtonUI();

                // 2. Den UI-Thread kurz atmen lassen für die Hardware-Initialisierung
                MainThread.BeginInvokeOnMainThread(async () => {
                    await Task.Delay(200);

                    UpdateCameraLayout();

                    // 3. Layout-Zyklus erzwingen
                    this.InvalidateMeasure();
                    if (this.Parent is View parent) parent.InvalidateMeasure();
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Restart: {ex.Message}");
        }
    }

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        _currentFlashMode = cameraView.FlashMode switch
        {
            FlashMode.Auto => FlashMode.Enabled,
            FlashMode.Enabled => FlashMode.Disabled,
            _ => FlashMode.Auto
        };

        cameraView.FlashMode = _currentFlashMode;
        UpdateFlashButtonUI();

        SettingsService.Instance.FlashMode = (int)_currentFlashMode;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        UpdateCameraLayout(width, height);

        _resizeCts?.Cancel();
        _resizeCts = new CancellationTokenSource();
        var token = _resizeCts.Token;

        _ = Task.Run(async () => {
            try
            {
                await Task.Delay(500, token);
            }
            catch (OperationCanceledException) { }
        });
    }

    private void UpdateCameraLayout(double? overrideWidth = null, double? overrideHeight = null)
    {
        if (cameraView?.Camera == null)
            return;

        double availableWidth = (overrideWidth > 0) ? overrideWidth.Value : this.Width;
        double availableHeight = (overrideHeight > 0) ? overrideHeight.Value : this.Height;

        if (availableWidth <= 0 || availableHeight <= 0)
            return;

        // Reset
        cameraView.WidthRequest = -1;
        cameraView.HeightRequest = -1;

        double sWidth = _optimalSize.Width;
        double sHeight = _optimalSize.Height;
        bool isPortrait = availableHeight > availableWidth;

        if (isPortrait && sWidth > sHeight)
        {
            sWidth = _optimalSize.Height;
            sHeight = _optimalSize.Width;
        }

        double targetRatio = sWidth / sHeight;
        double containerRatio = availableWidth / availableHeight;
        double finalWidth, finalHeight;

        if (containerRatio > targetRatio)
        {
            finalHeight = availableHeight;
            finalWidth = availableHeight * targetRatio;
        }
        else
        {
            finalWidth = availableWidth;
            finalHeight = availableWidth / targetRatio;
        }

        cameraView.WidthRequest = finalWidth;
        cameraView.HeightRequest = finalHeight;
        cameraView.HorizontalOptions = LayoutOptions.Center;
        cameraView.VerticalOptions = LayoutOptions.Center;
    }

    private void UpdateFlashButtonUI()
    {
        if (liveButtons.Children.FirstOrDefault() is not Button flashBtn)
            return;

        flashBtn.Text = _currentFlashMode switch
        {
            FlashMode.Enabled => MaterialIcons.Flash_on,
            FlashMode.Disabled => MaterialIcons.Flash_off,
            _ => MaterialIcons.Flash_auto
        };
    }

    private void OnZoomSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (!_isZoomSupported)
            return;

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
            if (!customZoomSlider.IsVisible)
            {
                customZoomSlider.IsVisible = true;
                await customZoomSlider.FadeToAsync(1, 250, Easing.CubicOut);
            }
            await Task.Delay(3000, token);
            await customZoomSlider.FadeToAsync(0, 500, Easing.CubicIn);
            customZoomSlider.IsVisible = false;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        try
        {
            flashOverlay.IsVisible = true;
            flashOverlay.Opacity = 1;

            using var stream = await cameraView.TakePhotoAsync();

            if (stream != null)
            {
                _tempFilePath = await SavePhotoToCache(stream);
                await cameraView.StopCameraAsync();

                if (!string.IsNullOrEmpty(_tempFilePath))
                {
                    previewImage.Source = ImageSource.FromFile(_tempFilePath);
                    ToggleUI(isPreview: true);
                    await flashOverlay.FadeToAsync(0, 400, Easing.CubicOut);
                }
            }
            flashOverlay.IsVisible = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Capture Fehler: {ex.Message}");
            flashOverlay.IsVisible = false;
            await RestartPreview();
        }
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
        _tempFilePath = string.Empty;

        await RestartPreview();
        ToggleUI(isPreview: false);
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        CameraResultService.SetResult(!string.IsNullOrEmpty(_tempFilePath) ? new FileResult(_tempFilePath) : null);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await cameraView.StopCameraAsync();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRatioClicked(object sender, EventArgs e)
    {
        if (sender is not Button selectedButton || selectedButton.CommandParameter == null)
            return;

        double newRatio = Convert.ToDouble(selectedButton.CommandParameter);

        if (!_isRatioPickerExpanded)
        {
            _isRatioPickerExpanded = true;
            UpdateRatioPickerUI();
            return;
        }

        _userSelectedRatio = newRatio;
        _isRatioPickerExpanded = false;
        UpdateRatioPickerUI();

        SettingsService.Instance.CaptureRatio = _userSelectedRatio;
        SettingsService.Instance.SaveSettings();

        var newSize = GetOptimalSize(_userSelectedRatio);
        await RestartPreview(newSize);
    }

    private void OnContainerTapped(object sender, EventArgs e)
    {
        if (_isRatioPickerExpanded)
        {
            _isRatioPickerExpanded = false;
            UpdateRatioPickerUI();
        }

        if (_isZoomSupported)
            ShowZoomSliderWithTimeout();
    }

    private void UpdateRatioPickerUI(bool animate = true)
    {
        foreach (var child in ratioContainer.Children)
        {
            if (child is Button btn && btn.CommandParameter is double val)
            {
                bool isSelected = Math.Abs(val - _userSelectedRatio) < 0.05;
                bool shouldBeVisible = _isRatioPickerExpanded || isSelected;

                // Farbe setzen
                btn.TextColor = isSelected ? Colors.Yellow : Colors.White;

                if (shouldBeVisible)
                {
                    if (!btn.IsVisible)
                    {
                        btn.IsVisible = true;
                        if (animate)
                        {
                            btn.Opacity = 0;
                            btn.FadeToAsync(0.8, 250);
                        }
                        else
                        {
                            btn.Opacity = 0.8;
                        }
                    }
                }
                else
                {
                    btn.IsVisible = false;
                }
            }
        }
    }

    private void ToggleUI(bool isPreview)
    {
        previewImage.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
        ratioContainer.IsVisible = !isPreview;
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream)
    {
        if (photoStream == null) return null;
        string cachePath = Path.Combine(FileSystem.CacheDirectory, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
        using (FileStream fileStream = File.Create(cachePath))
        {
            if (photoStream.CanSeek)
                photoStream.Position = 0;
            await photoStream.CopyToAsync(fileStream);
        }
        return cachePath;
    }

    private async void OnSwitchCameraClicked(object sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 1)
        {
            int nextIndex = (cameraView.Cameras.IndexOf(cameraView.Camera) + 1) % cameraView.Cameras.Count;
            await cameraView.StopCameraAsync();
            cameraView.Camera = cameraView.Cameras[nextIndex];

            var newSize = GetOptimalSize(_userSelectedRatio);
            await RestartPreview(newSize);
        }
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

    public static void SetResult(FileResult? result)
    {
        _tcs?.TrySetResult(result);
    }
}
