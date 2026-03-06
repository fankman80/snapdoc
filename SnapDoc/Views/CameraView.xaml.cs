
using Camera.MAUI;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    private string? tempFilePath = string.Empty;

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
            var backCamera = cameraView.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Back);            
            cameraView.Camera = backCamera ?? cameraView.Cameras.First();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                Size selectedRes = GetOptimalResolution(highRes: false);

                if (await cameraView.StartCameraAsync(selectedRes) == CameraResult.Success)
                {
                    await Task.Delay(1000);
                    cameraView.FlashMode = FlashMode.Auto;
                }
            });
        }
    }

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        try
        {
            flashOverlay.IsVisible = true;
            flashOverlay.Opacity = 1;

            await cameraView.StopCameraAsync();
            var maxRes = GetOptimalResolution(highRes: true);

            if (await cameraView.StartCameraAsync(maxRes) == CameraResult.Success)
            {
                await Task.Delay(350); // Puffer für Sensor-Pegel

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
            Debug.WriteLine($"Fehler: {ex.Message}");
            flashOverlay.IsVisible = false; // Im Fehlerfall Vorhang auf
            OnRetakeClicked(sender, e);
        }
    }

    private Size GetOptimalResolution(bool highRes)
    {
        var available = cameraView.Camera?.AvailableResolutions;
        if (available == null || available.Count == 0) return new Size(0, 0);

        if (highRes)
        {
            return available.OrderByDescending(r => r.Width * r.Height).First();
        }
        else
        {
            var preferred = available
                .Where(r => r.Width <= 1920 && r.Width > 0)
                .OrderByDescending(r => r.Width * r.Height)
                .FirstOrDefault();

            return preferred.Width > 0 ? preferred : available.OrderBy(r => r.Width).First();
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(tempFilePath))
            CameraResultService.SetResult(new FileResult(tempFilePath));
        else
            CameraResultService.SetResult(null);
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);

        var stableRes = GetOptimalResolution(highRes: false);
        await cameraView.StartCameraAsync(stableRes);

        ToggleUI(isPreview: false);
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await cameraView.StopCameraAsync();
        await Shell.Current.GoToAsync("..");
    }

    private void ToggleUI(bool isPreview)
    {
        previewImage.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
    }

    private static async Task<string?> SavePhotoToCache(Stream photoStream)
    {
        if (photoStream == null)
            return null;

        string fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        string cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);

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

            Size selectedRes = GetOptimalResolution(highRes: false);

            if (await cameraView.StartCameraAsync(selectedRes) == CameraResult.Success)
            {
                await Task.Delay(1000);
            }
        }
    }

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        var button = sender as Button;

        switch (cameraView.FlashMode)
        {
            case FlashMode.Auto:
                cameraView.FlashMode = FlashMode.Enabled;
                button?.Text = MaterialIcons.Flash_on; 
                break;

            case FlashMode.Enabled:
                cameraView.FlashMode = FlashMode.Disabled;
                button?.Text = MaterialIcons.Flash_off;
                break;

            case FlashMode.Disabled:
            default:
                cameraView.FlashMode = FlashMode.Auto;
                button?.Text = MaterialIcons.Flash_auto;
                break;
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