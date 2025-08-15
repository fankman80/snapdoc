#nullable disable

using SkiaSharp;

namespace SnapDoc;

public class CapturePicture
{
    public static async Task<(FileResult, Size)> Capture(string filepath, string thumbnailPath = null, string customFilename = null)
    {
        var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (cameraStatus != PermissionStatus.Granted)
            cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();

        if (cameraStatus != PermissionStatus.Granted)
        {
            await Application.Current.Windows[0].Page.DisplayAlert(
                "Berechtigung fehlt",
                "Bitte erlaube Kamera- und Speicherzugriff in den App-Einstellungen.",
                "OK");
            return (null, new Size(0, 0));
        }

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo == null)
            {
                Console.WriteLine("Keine Datei zurückgegeben (Picker evtl. abgebrochen).");
                return (null, new Size(0, 0));
            }

            using var stream = await photo.OpenReadAsync();
            string filename = customFilename ?? $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}{Path.GetExtension(photo.FileName)}";
            string resultPath = null;

            if (filepath != null)
            {
                string newFilePath = Path.Combine(Settings.DataDirectory, filepath, filename);
                using var newStream = File.Create(newFilePath);
                await stream.CopyToAsync(newStream);
                resultPath = newFilePath;
            }

            if (thumbnailPath != null && resultPath != null)
            {
                string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath, filename);
                Thumbnail.Generate(resultPath, thumbFilePath);
            }

            if (!string.IsNullOrEmpty(photo.FullPath) && File.Exists(photo.FullPath))
            {
                File.Delete(photo.FullPath);
            }

            if (resultPath != null)
            {
                var codec = SKCodec.Create(resultPath);
                return (new FileResult(resultPath), new Size(codec.Info.Size.Width, codec.Info.Size.Height));
            }

            return (null, new Size(0, 0));
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Fotovorgang vom Benutzer abgebrochen oder Intent unterbrochen.");
            return (null, new Size(0, 0));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Aufnehmen oder Speichern des Fotos: {ex.Message}");
            return (null, new Size(0, 0));
        }
    }
}