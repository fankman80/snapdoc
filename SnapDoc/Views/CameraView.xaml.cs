using Camera.MAUI;
using SnapDoc.Services;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? _tempFilePath = string.Empty;
    private Size _optimalSize;
    private double _userSelectedRatio = SettingsService.Instance.CaptureRatio;
    private FlashMode _currentFlashMode = (FlashMode)SettingsService.Instance.FlashMode;
    private bool _isZoomSupported = false;
    private bool _isRatioPickerExpanded = false;
    private CancellationTokenSource? _zoomTimerCts;
    private bool _isInitialized = false;

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

    private async Task OnCamerasLoaded()
    {
        if (cameraView.Cameras.Count == 0) return;

        switchCameraButton.IsVisible = cameraView.Cameras.Count > 1;

        cameraView.Camera = cameraView.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Back)
                           ?? cameraView.Cameras.First();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0 || height <= 0 || _isInitialized) return;

        _isInitialized = true;
        Dispatcher.Dispatch(async () => await InitializeCameraSelection());
    }

    private async Task InitializeCameraSelection()
    {
        _optimalSize = GetOptimalSize(_userSelectedRatio);

        if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
        {
            ConfigureZoom();
            cameraView.FlashMode = _currentFlashMode;
            UpdateFlashButtonUI();
            PopulateRatioButtons();
            UpdateCameraLayout(this.Width, this.Height);
        }
    }

    private void ConfigureZoom()
    {
        _isZoomSupported = cameraView.MaxZoomFactor > cameraView.MinZoomFactor;
        if (_isZoomSupported)
        {
            customZoomSlider.Minimum = cameraView.MinZoomFactor;
            customZoomSlider.Maximum = Math.Min(cameraView.MaxZoomFactor, 10);
            customZoomSlider.Value = cameraView.ZoomFactor;
        }
    }

    private Size GetOptimalSize(double? targetRatio = null)
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null || available.Count == 0) return new Size(0, 0);

        double finalTarget = (targetRatio == -1.0)
            ? Math.Max(Width, Height) / Math.Min(Width, Height)
            : targetRatio ?? 1.33;

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
            cameraView.IsVisible = false;

            if (specificSize.HasValue) _optimalSize = specificSize.Value;

            await Task.Delay(150);

            if (await cameraView.StartCameraAsync(_optimalSize) == CameraResult.Success)
            {
                cameraView.FlashMode = _currentFlashMode;
                UpdateCameraLayout(Width, Height);
                cameraView.IsVisible = true;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Restart Error: {ex.Message}"); }
    }

    private void UpdateCameraLayout(double width, double height)
    {
        if (cameraView?.Camera == null || _optimalSize.Width <= 0) return;

        bool isPortrait = height > width;
        double camW = isPortrait ? Math.Min(_optimalSize.Width, _optimalSize.Height) : Math.Max(_optimalSize.Width, _optimalSize.Height);
        double camH = isPortrait ? Math.Max(_optimalSize.Width, _optimalSize.Height) : Math.Min(_optimalSize.Width, _optimalSize.Height);

        double targetRatio = camW / camH;
        double finalWidth, finalHeight;

        if ((width / height) > targetRatio)
        {
            finalHeight = height;
            finalWidth = height * targetRatio;
        }
        else
        {
            finalWidth = width;
            finalHeight = width / targetRatio;
        }

        Dispatcher.Dispatch(() => {
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

        var uniqueRatios = available
            .Select(r => {
                double ratio = Math.Round((double)r.Width / r.Height, 2);
                return new { Ratio = ratio, Name = GetRatioName(ratio) };
            })
            .GroupBy(x => x.Name)
            .Select(g => new CameraRatio { Name = g.Key, Value = g.First().Ratio })
            .OrderByDescending(r => r.Value)
            .ToList();

        Dispatcher.Dispatch(() => {
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
            1.0 => "1:1",
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

    // --- Events ---

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
                _tempFilePath = await SavePhotoToCache(stream);
                previewImage.Source = ImageSource.FromFile(_tempFilePath);
                ToggleUI(isPreview: true);
                await cameraView.StopCameraAsync();
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex.Message); await RestartPreview(); }
        finally { await flashOverlay.FadeToAsync(0, 200); flashOverlay.IsVisible = false; }
    }

    private async void OnFlashButtonClicked(object sender, EventArgs e)
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

        if (!_isRatioPickerExpanded) { _isRatioPickerExpanded = true; UpdateRatioPickerUI(); return; }

        _userSelectedRatio = val;
        _isRatioPickerExpanded = false;
        UpdateRatioPickerUI();

        SettingsService.Instance.CaptureRatio = val;
        SettingsService.Instance.SaveSettings();
        await RestartPreview(GetOptimalSize(val));
    }

    private void OnZoomSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        if (_isZoomSupported)
        {
            cameraView.ZoomFactor = (float)e.NewValue;
            ShowZoomSliderWithTimeout();
        }
    }

    private async void ShowZoomSliderWithTimeout()
    {
        _zoomTimerCts?.Cancel();
        _zoomTimerCts = new CancellationTokenSource();
        try
        {
            customZoomSlider.IsVisible = true;
            await customZoomSlider.FadeToAsync(1, 200);
            await Task.Delay(3000, _zoomTimerCts.Token);
            await customZoomSlider.FadeToAsync(0, 500);
            customZoomSlider.IsVisible = false;
        }
        catch (OperationCanceledException) { }
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath);
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
        if (cameraView.Cameras.Count > 1)
        {
            int nextIndex = (cameraView.Cameras.IndexOf(cameraView.Camera) + 1) % cameraView.Cameras.Count;
            await cameraView.StopCameraAsync();
            cameraView.Camera = cameraView.Cameras[nextIndex];
            await RestartPreview(GetOptimalSize(_userSelectedRatio));
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
        var path = Path.Combine(FileSystem.CacheDirectory, $"Cap_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
        using var fs = File.Create(path);
        await photoStream.CopyToAsync(fs);
        return path;
    }

    private void OnContainerTapped(object sender, EventArgs e)
    {
        if (_isRatioPickerExpanded) { _isRatioPickerExpanded = false; UpdateRatioPickerUI(); }
        if (_isZoomSupported) ShowZoomSliderWithTimeout();
    }
}

public static class CameraResultService
{
    private static TaskCompletionSource<FileResult?>? _tcs;
    public static Task<FileResult?> WaitForCaptureAsync() { _tcs = new TaskCompletionSource<FileResult?>(); return _tcs.Task; }
    public static void SetResult(FileResult? result) => _tcs?.TrySetResult(result);
}

public class CameraRatio
{
    public string? Name { get; set; }
    public double Value { get; set; }
}