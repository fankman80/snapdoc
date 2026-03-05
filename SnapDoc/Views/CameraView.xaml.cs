
using Camera.MAUI;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    bool playing = false;
    private string tempFilePath = string.Empty;

    public CameraView()
    {
        InitializeComponent();
        cameraView.CamerasLoaded += CameraView_CamerasLoaded;
    }

    private void CameraView_CamerasLoaded(object sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 0)
        {
            cameraView.Camera = cameraView.Cameras.First();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var result = await cameraView.StartCameraAsync();

                if (result == CameraResult.Success)
                    playing = true;
            });
        }
    }

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        try
        {
            var stream = await cameraView.TakePhotoAsync();
            if (stream != null)
            {
                tempFilePath = await SavePhotoToCache(stream);

                if (File.Exists(tempFilePath))
                {
                    await cameraView.StopCameraAsync();
                    previewImage.Source = ImageSource.FromFile(tempFilePath);
                    ToggleUI(isPreview: true);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Snapshot: {ex.Message}");
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        CameraResultService.SetResult(new FileResult(tempFilePath));
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        await cameraView.StartCameraAsync();
        ToggleUI(isPreview: false);
    }

    private void ToggleUI(bool isPreview)
    {
        previewImage.IsVisible = isPreview;
        previewButtons.IsVisible = isPreview;
        liveButtons.IsVisible = !isPreview;
    }

    private async Task<string> SavePhotoToCache(Stream photoStream)
    {
        if (photoStream == null) return null;

        string fileName = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        string cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        using (FileStream fileStream = File.Create(cachePath))
        {
            if (photoStream.CanSeek) photoStream.Position = 0;
            await photoStream.CopyToAsync(fileStream);
        }

        return cachePath; // Rückgabe des Pfads zur Datei
    }

    private async void OnSwitchCameraClicked(object sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 1)
        {
            int index = cameraView.Cameras.IndexOf(cameraView.Camera);
            int nextIndex = (index + 1) % cameraView.Cameras.Count;

            await cameraView.StopCameraAsync();
            cameraView.Camera = cameraView.Cameras[nextIndex];
        }
    }

    private void OnFlashButtonClicked(object sender, EventArgs e)
    {
        var button = sender as Button;

        switch (cameraView.FlashMode)
        {
            case FlashMode.Auto:
                cameraView.FlashMode = FlashMode.Enabled;
                if (button != null) button.Text = "Flash: ON";
                break;

            case FlashMode.Enabled:
                cameraView.FlashMode = FlashMode.Disabled;
                if (button != null) button.Text = "Flash: OFF";
                break;

            case FlashMode.Disabled:
            default:
                cameraView.FlashMode = FlashMode.Auto;
                if (button != null) button.Text = "Flash: AUTO";
                break;
        }

        Debug.WriteLine($"Blitz-Modus geändert auf: {cameraView.FlashMode}");
    }
}

public static class CameraResultService
{
    private static TaskCompletionSource<FileResult> _tcs;

    public static Task<FileResult> WaitForCaptureAsync()
    {
        _tcs = new TaskCompletionSource<FileResult>();
        return _tcs.Task;
    }

    public static void SetResult(FileResult result)
    {
        _tcs?.TrySetResult(result);
    }
}