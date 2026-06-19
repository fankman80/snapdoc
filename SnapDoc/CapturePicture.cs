#nullable disable
using SkiaSharp;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.Views;

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
            await Application.Current.Windows[0].Page.DisplayAlertAsync(
                AppResources.berechtigung_fehlt,
                AppResources.berechtigung_kamera_dateien_info,
                AppResources.ok);
            return (null, new Size(0, 0));
        }

        try
        {
            FileResult foto = SettingsService.Instance.SelectedCameraTool switch
            {
                var s when s == Settings.CameraTools[1] => await MediaPicker.Default.CapturePhotoAsync(),
                var s when s == Settings.CameraTools[2] => await OpenCustomCamera(),
                _ => DeviceInfo.Current.Platform == DevicePlatform.WinUI
                     ? await OpenCustomCamera()
                     : await MediaPicker.Default.CapturePhotoAsync()
            };

            static async Task<FileResult> OpenCustomCamera()
            {
                await Shell.Current.GoToAsync("cameraView");
                return await CameraResultService.WaitForCaptureAsync();
            }

            if (foto == null)
                return (null, new Size(0, 0));

            string filename = customFilename ?? $"IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string resultPath = null;
            int finalWidth = 0;
            int finalHeight = 0;

            if (filepath != null)
            {
                resultPath = Path.Combine(Settings.DataDirectory, filepath, filename);

                string mainDir = Path.GetDirectoryName(resultPath);
                if (!Directory.Exists(mainDir))
                    Directory.CreateDirectory(mainDir);

                // Bildmasse direkt aus der temporären Datei lesen, statt alles in den RAM zu kopieren
                using (var fileStream = File.OpenRead(foto.FullPath))
                using (var managedStream = new SKManagedStream(fileStream, false))
                using (var codec = SKCodec.Create(managedStream))
                {
                    if (codec != null)
                    {
                        var orientation = codec.EncodedOrigin;
                        if (orientation == SKEncodedOrigin.RightTop || orientation == SKEncodedOrigin.LeftBottom)
                        {
                            finalWidth = codec.Info.Height;
                            finalHeight = codec.Info.Width;
                        }
                        else
                        {
                            finalWidth = codec.Info.Width;
                            finalHeight = codec.Info.Height;
                        }
                    }
                }

                // Direkte Dateipfad-Überladung nutzen (wartet auf das nun optimierte, schnelle Thumbnail)
                if (thumbnailPath != null)
                {
                    string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath, filename);
                    await Task.Run(() => Thumbnail.Generate(foto.FullPath, thumbFilePath));
                }

                // Den schweren Hauptbild-Pass entkoppeln (Fire-and-Forget)
                string tempFullPath = foto.FullPath;
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var fileStream = File.OpenRead(tempFullPath);
                        using var managedStream = new SKManagedStream(fileStream, false);
                        using var codec = SKCodec.Create(managedStream);

                        if (codec == null)
                            return;

                        var decodeInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height);
                        using var originalBitmap = SKBitmap.Decode(codec, decodeInfo);

                        if (originalBitmap == null)
                            return;

                        var orientation = codec.EncodedOrigin;
                        SKBitmap finalBitmap = originalBitmap;

                        if (orientation != SKEncodedOrigin.TopLeft)
                            finalBitmap = RotateBitmap(originalBitmap, orientation);

                        using (var image = SKImage.FromBitmap(finalBitmap))
                        using (var data = image.Encode(SKEncodedImageFormat.Jpeg, SettingsService.Instance.FotoQuality))
                        {
                            using var newStream = File.Create(resultPath);
                            data.SaveTo(newStream);
                        }

                        if (finalBitmap != originalBitmap) finalBitmap.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Main image save failed: {ex.Message}");
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(tempFullPath) && File.Exists(tempFullPath))
                            try { File.Delete(tempFullPath); } catch { }
                    }
                });
            }
            else
            {
                // Falls filepath null ist, die temporäre Datei direkt löschen
                if (!string.IsNullOrEmpty(foto.FullPath) && File.Exists(foto.FullPath))
                    try { File.Delete(foto.FullPath); } catch { }
            }

            if (resultPath != null)
                return (new FileResult(resultPath), new Size(finalWidth, finalHeight));

            return (null, new Size(0, 0));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Capture crashed completely: {ex.Message}");
            return (null, new Size(0, 0));
        }
    }

    private static SKBitmap RotateBitmap(SKBitmap bitmap, SKEncodedOrigin orientation)
    {
        SKBitmap rotated;
        switch (orientation)
        {
            case SKEncodedOrigin.BottomRight: // 180°
                rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            case SKEncodedOrigin.RightTop: // 90° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(bitmap.Height, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            case SKEncodedOrigin.LeftBottom: // 270° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(0, bitmap.Width);
                    canvas.RotateDegrees(270);
                    canvas.DrawBitmap(bitmap, 0, 0, SKSamplingOptions.Default);
                }
                break;
            default:
                return bitmap;
        }
        return rotated;
    }
}