using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using System.Diagnostics;

namespace SnapDoc.Views;

public partial class CameraView : ContentPage
{
    public CameraView()
    {
        InitializeComponent();

        // WICHTIG: Die Seite ist ihr eigener Datenlieferant
        BindingContext = this;

        // --- Manuelle Bindings im Code-Behind ---

        // Zoom: Slider -> Kamera
        MainCamera.SetBinding(CommunityToolkit.Maui.Views.CameraView.ZoomFactorProperty,
            new Binding(nameof(Slider.Value), source: ZoomSlider));

        // Slider Limits an die gewählte Kamera binden
        ZoomSlider.SetBinding(Slider.MinimumProperty,
            new Binding("SelectedCamera.MinimumZoomFactor", source: MainCamera));
        ZoomSlider.SetBinding(Slider.MaximumProperty,
            new Binding("SelectedCamera.MaximumZoomFactor", source: MainCamera));

        // Blitz: Picker -> Kamera
        FlashPicker.ItemsSource = Enum.GetValues(typeof(CameraFlashMode));
        MainCamera.SetBinding(CommunityToolkit.Maui.Views.CameraView.CameraFlashModeProperty,
            new Binding(nameof(Picker.SelectedItem), source: FlashPicker));

        // Auflösung: Kamera -> Picker -> Kamera
        ResolutionPicker.SetBinding(Picker.ItemsSourceProperty,
            new Binding("SelectedCamera.SupportedResolutions", source: MainCamera));
        MainCamera.SetBinding(CommunityToolkit.Maui.Views.CameraView.ImageCaptureResolutionProperty,
            new Binding(nameof(Picker.SelectedItem), source: ResolutionPicker));
        ResolutionPicker.ItemDisplayBinding = new Binding(".");

        CaptureButton.Clicked += OnCaptureClicked;
        MainCamera.PropertyChanged += OnCameraPropertyChanged;
    }

    private void OnCameraPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Wir prüfen, ob die Kamera fertig geladen hat oder ob sich die Auflösungen geändert haben
        if (e.PropertyName == nameof(MainCamera.SelectedCamera) ||
            e.PropertyName == "SelectedResolution") // Manchmal reicht auch die Auswahl der Kamera
        {
            TrySetMaxResolution();
        }
    }

    private void TrySetMaxResolution()
    {
        var currentCam = MainCamera.SelectedCamera;
        if (currentCam?.SupportedResolutions?.Count > 0)
        {
            var maxRes = currentCam.SupportedResolutions
                .OrderByDescending(r => r.Width * r.Height)
                .First();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (MainCamera.ImageCaptureResolution != maxRes)
                {
                    MainCamera.ImageCaptureResolution = maxRes;
                    ResolutionPicker.SelectedItem = maxRes;
                    Debug.WriteLine($"Max Auflösung gesetzt: {maxRes.Width}x{maxRes.Height}");
                }
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CameraResultService.SetResult(null); // Verhindert endloses Warten
    }

    private async void OnCaptureClicked(object sender, EventArgs e)
    {
        try
        {
            var captureImageCTS = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Stream stream = await MainCamera.CaptureImage(captureImageCTS.Token);

            if (stream != null)
                await ProcessCapturedStream(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Kamerafehler: {ex.Message}");
        }
    }

    private async Task ProcessCapturedStream(Stream stream)
    {
        string filename = $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        string path = Path.Combine(FileSystem.CacheDirectory, filename); // Temporär speichern

        using (var fileStream = File.Create(path))
        {
            await stream.CopyToAsync(fileStream);
        }

        // Ergebnis an den wartenden Service senden
        CameraResultService.SetResult(new FileResult(path));

        await Navigation.PopAsync();
    }

    private async void OnSwitchCameraClicked(object sender, EventArgs e)
    {
        try
        {
            var cameras = await MainCamera.GetAvailableCameras(CancellationToken.None);

            if (cameras == null || cameras.Count < 2)
                return;

            var currentCamera = MainCamera.SelectedCamera;
            int currentIndex = -1;
            for (int i = 0; i < cameras.Count; i++)
            {
                if (cameras[i] == currentCamera)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + 1) % cameras.Count;
            MainCamera.SelectedCamera = cameras[nextIndex];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Kamera-Wechsel: {ex.Message}");
        }
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