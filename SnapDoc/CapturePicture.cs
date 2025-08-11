#nullable disable

using SkiaSharp;

namespace SnapDoc;

public class CapturePicture
{
    public static async Task<(FileResult, Size)> Capture(string filepath, string thumbnailPath = null, string customFilename = null)
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            try
            {
                FileResult photo = await MediaPicker.Default.CapturePhotoAsync();

                if (photo != null)
                {
                    string originalFilePath = photo.FullPath;
                    string resultPath = null;
                    string filename;
                    if (customFilename != null)
                        filename = customFilename;
                    else
                        filename = "IMG_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Path.GetExtension(originalFilePath);

                    if (filepath != null)
                    {
                        var type = photo.ContentType;
                        string newFilePath = Path.Combine(Settings.DataDirectory, filepath, filename);
                        var originalStream = File.OpenRead(originalFilePath);
                        var newStream = File.Create(newFilePath);
                        await originalStream.CopyToAsync(newStream);
                        newStream.Close();
                        originalStream.Close();
                        resultPath = newFilePath;
                    }

                    if (thumbnailPath != null)
                    {
                        string thumbFilePath = Path.Combine(Settings.DataDirectory, thumbnailPath, filename);
                        Thumbnail.Generate(originalFilePath, thumbFilePath);
                    }

                    if (File.Exists(originalFilePath)) //lösche das Originalfoto
                        File.Delete(originalFilePath);

                    var codec = SKCodec.Create(resultPath.ToString());

                    return (new FileResult(resultPath), new Size(codec.Info.Size.Width, codec.Info.Size.Height));
                }
                else
                    return (null, new Size(0,0));
            }
            catch (Exception ex)
            {
                // Fehlerbehandlung
                Console.WriteLine($"Fehler beim Aufnehmen oder Umbenennen des Fotos: {ex.Message}");
                return (null, new Size(0, 0));
            }
        }
        else
            return (null, new Size(0, 0));
    }
}
