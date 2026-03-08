using Camera.MAUI;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? tempFilePath = string.Empty;
    private Size _optimalPhotoSize;
    private Size _optimalPreviewSize;
    private double? _userSelectedRatio = null;
    private FlashMode _currentFlashMode = FlashMode.Auto;

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
        var (photo, preview) = GetOptimalMatchedPair(_userSelectedRatio); // Ratio übergeben!
        _optimalPhotoSize = photo;
        _optimalPreviewSize = preview;

        if (await cameraView.StartCameraAsync(_optimalPreviewSize) == CameraResult.Success)
        {
            UpdateCameraLayout();
            cameraView.FlashMode = _currentFlashMode;
            UpdateFlashButtonUI();
            PopulateRatioButtons();
        }
    }

    private void PopulateRatioButtons()
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null)
            return;

        var uniqueRatios = available
            .Select(r => new { Ratio = Math.Round((double)r.Width / r.Height, 2), Name = GetRatioName(Math.Round((double)r.Width / r.Height, 2)) })
            .GroupBy(x => x.Name) // Gruppiere nach dem Namen "16:9", "4:3" etc.
            .Select(g => new RatioItem { 
                Name = g.Key, 
                Value = g.First().Ratio 
            })
            .OrderByDescending(r => r.Value)
            .ToList();

        BindableLayout.SetItemsSource(ratioContainer, uniqueRatios);

        // Initialen Button markieren
        MainThread.BeginInvokeOnMainThread(async () => {
            await Task.Delay(200); // Warten auf UI Rendering
            if (ratioContainer.Children.FirstOrDefault() is Button firstButton)
                firstButton.TextColor = Colors.Yellow;
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

    private (Size photo, Size preview) GetOptimalMatchedPair(double? targetRatio = null)
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null || available.Count == 0)
            return (new Size(0, 0), new Size(0, 0));

        var ratioGroups = available
            .GroupBy(r => Math.Round((double)r.Width / r.Height, 2))
            .Select(g => new {
                Ratio = g.Key,
                Resolutions = g.OrderByDescending(r => r.Width * r.Height).ToList()
            })
            .ToList();

        var selectedGroup = targetRatio.HasValue
            ? ratioGroups.FirstOrDefault(g => g.Ratio == targetRatio.Value)
            : ratioGroups.OrderByDescending(g => g.Resolutions.First().Width * g.Resolutions.First().Height).FirstOrDefault();

        selectedGroup ??= ratioGroups.First();

        var photoCandidate = selectedGroup.Resolutions.First();
        var previewCandidate = selectedGroup.Resolutions
            .Where(r => r.Width >= 1500) 
            .OrderBy(r => r.Width) // Nimm die kleinste, die aber noch groß genug ist
            .FirstOrDefault();

        // Falls keine gefunden wurde (sehr altes Gerät), nimm die größte verfügbare
        if (previewCandidate.Width == 0)
            previewCandidate = selectedGroup.Resolutions.OrderByDescending(r => r.Width).First();
    
        return (photoCandidate, previewCandidate);
    }

    private async Task RestartPreview()
    {
        await cameraView.StopCameraAsync();
        if (await cameraView.StartCameraAsync(_optimalPreviewSize) == CameraResult.Success)
        {
            UpdateCameraLayout();
            cameraView.FlashMode = _currentFlashMode;
            UpdateFlashButtonUI();
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
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        MainThread.BeginInvokeOnMainThread(async () => {
            await Task.Delay(200);

            if (cameraView.Camera != null)
            {
                try
                {
                    await cameraView.StopCameraAsync();
                    if (await cameraView.StartCameraAsync(_optimalPreviewSize) == CameraResult.Success)
                    {
                        cameraView.FlashMode = _currentFlashMode;
                        UpdateCameraLayout();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Fehler beim Re-Start nach Rotation: {ex.Message}");
                }
            }
            else
                UpdateCameraLayout();
        });
    }

    private void UpdateCameraLayout()
    {
        if (cameraView.Camera == null || CameraContainer.Width <= 0 || _optimalPreviewSize.Width <= 0)
            return;

        cameraView.WidthRequest = -1;
        cameraView.HeightRequest = -1;

        double streamWidth = _optimalPreviewSize.Width;
        double streamHeight = _optimalPreviewSize.Height;

        bool isPortrait = DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Portrait;
        if (isPortrait && streamWidth > streamHeight)
        {
            streamWidth = _optimalPreviewSize.Height;
            streamHeight = _optimalPreviewSize.Width;
        }

        double streamRatio = streamWidth / streamHeight;
        double containerWidth = CameraContainer.Width;
        double containerHeight = CameraContainer.Height;

        double finalWidth, finalHeight;
        if ((containerWidth / containerHeight) > streamRatio)
        {
            finalHeight = containerHeight;
            finalWidth = containerHeight * streamRatio;
        }
        else
        {
            finalWidth = containerWidth;
            finalHeight = containerWidth / streamRatio;
        }

        cameraView.WidthRequest = finalWidth;
        cameraView.HeightRequest = finalHeight;
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

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        try
        {
            flashOverlay.IsVisible = true;
            flashOverlay.Opacity = 1;

            await cameraView.StopCameraAsync();

            if (await cameraView.StartCameraAsync(_optimalPhotoSize) == CameraResult.Success)
            {
                await Task.Delay(350);
                using var stream = await cameraView.TakePhotoAsync();

                if (stream != null)
                {
                    tempFilePath = await SavePhotoToCache(stream);
                    await cameraView.StopCameraAsync();

                    if (!string.IsNullOrEmpty(tempFilePath))
                    {
                        previewImage.Source = ImageSource.FromFile(tempFilePath);
                        ToggleUI(isPreview: true);
                        await flashOverlay.FadeToAsync(0, 400, Easing.CubicOut);
                        flashOverlay.IsVisible = false;
                    }
                }
            }
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
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        tempFilePath = string.Empty;
        await RestartPreview();
        ToggleUI(isPreview: false);
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        CameraResultService.SetResult(!string.IsNullOrEmpty(tempFilePath) ? new FileResult(tempFilePath) : null);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await cameraView.StopCameraAsync();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRatioClicked(object sender, EventArgs e)
    {
        var selectedButton = sender as Button;
        if (selectedButton?.CommandParameter is double newRatio)
        {
            _userSelectedRatio = newRatio;

            foreach (var child in ratioContainer.Children)
            {
                if (child is Button btn)
                    btn.TextColor = Colors.White;
            }

            if (Application.Current?.Resources.TryGetValue("Primary", out var primaryColor) == true)
                selectedButton.BackgroundColor = (Color)primaryColor;
            else
                selectedButton.BackgroundColor = Colors.Cyan; // Fallback

            var (photo, preview) = GetOptimalMatchedPair(_userSelectedRatio);
            _optimalPhotoSize = photo;
            _optimalPreviewSize = preview;

            await cameraView.StopCameraAsync();
            if (await cameraView.StartCameraAsync(_optimalPreviewSize) == CameraResult.Success)
                UpdateCameraLayout();
        }
    }

    private void ToggleUI(bool isPreview)
    {
        previewImage.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream)
    {
        if (photoStream == null) return null;
        string cachePath = Path.Combine(FileSystem.CacheDirectory, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
        using (FileStream fileStream = File.Create(cachePath))
        {
            if (photoStream.CanSeek) photoStream.Position = 0;
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
            await InitializeCameraSelection();
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
