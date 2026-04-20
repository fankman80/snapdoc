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

            if (filepath != null)
            {
                resultPath = Path.Combine(Settings.DataDirectory, filepath, filename);

                await Task.Run(async () =>
                {
                    using var stream = await foto.OpenReadAsync();
                    using var managedStream = new SKManagedStream(stream);
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
                });
            }

            if (thumbnailPath != null && resultPath != null)
            {
                string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath, filename);
                Thumbnail.Generate(resultPath, thumbFilePath);
            }

            if (!string.IsNullOrEmpty(foto.FullPath) && File.Exists(foto.FullPath))
                File.Delete(foto.FullPath);

            if (resultPath != null)
            {
                using var finalCodec = SKCodec.Create(resultPath);
                return (new FileResult(resultPath), new Size(finalCodec.Info.Width, finalCodec.Info.Height));
            }

            return (null, new Size(0, 0));
        }
        catch (Exception)
        {
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
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.RightTop: // 90° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(bitmap.Height, 0);
                    canvas.RotateDegrees(90);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            case SKEncodedOrigin.LeftBottom: // 270° CW
                rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                using (var canvas = new SKCanvas(rotated))
                {
                    canvas.Clear();
                    canvas.Translate(0, bitmap.Width);
                    canvas.RotateDegrees(270);
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                break;
            default:
                return bitmap;
        }
        return rotated;
    }
}