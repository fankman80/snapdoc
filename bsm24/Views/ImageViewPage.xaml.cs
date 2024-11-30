#nullable disable

using bsm24.ViewModels;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Mopups.Services;
using MR.Gestures;
using SkiaSharp;
using System.Globalization;

namespace bsm24.Views;

[QueryProperty(nameof(PlanId), "planId")]
[QueryProperty(nameof(PinId), "pinId")]
[QueryProperty(nameof(PinIcon), "pinIcon")]
[QueryProperty(nameof(ImgSource), "imgSource")]

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId { get; set; }
    public string PinId { get; set; }
    public string PinIcon { get; set; }
    public string ImgSource { get; set; }

    private bool isCleared = false;

    public ImageViewPage()
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;

        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        if (query.TryGetValue("pinIcon", out object value3))
            PinIcon = value3 as string;

        if (query.TryGetValue("imgSource", out object value4))
        {
            ImgSource = value4 as string;
            var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, ImgSource);

            // Lade die Metadaten aus dem Bild
            var directories = ImageMetadataReader.ReadMetadata(imgPath);

            // Finde die EXIF-Unterverzeichnisse
            var exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            if (exifSubIfdDirectory != null)
            {
                // Beispiel: Lese das Aufnahmedatum
                var dateTimeOriginal = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
                // Konvertiere das Datum in ein DateTime-Objekt
                if (DateTime.TryParseExact(dateTimeOriginal, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                {
                    // Formatierte Ausgabe im europäischen Format
                    string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
                    this.Title = formattedDate;
                }
            }
            ImageView.Source = imgPath;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e)
    {
        ImageFit();
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        var imageView = (TransformViewModel)ImageView.BindingContext;
        imageView.AnchorX = 1 / ImageView.Width * ((this.Width / 2) - ImageView.TranslationX);
        imageView.AnchorY = 1 / ImageView.Height * ((this.Height / 2) - ImageView.TranslationY);
    }

    private void ImageFit()
    {
        var imageView = (TransformViewModel)ImageView.BindingContext;
        var scale = Math.Min(this.Width / ImageView.Width, this.Height / ImageView.Height);
        imageView.AnchorX = 0;
        imageView.AnchorY = 0;
        imageView.Scale = scale;
        imageView.TranslationX = (this.Width - ImageView.Width * scale) / 2;
        imageView.TranslationY = (this.Height - ImageView.Height * scale) / 2;
    }

    //private async void DrawingView_DrawingLineCompleted(object sender, CommunityToolkit.Maui.Core.DrawingLineCompletedEventArgs e)
    //{
    //var stream = await DrawView.GetImageStream(200, 200);

    //imageView.Source = ImageSource.FromStream(() => stream);
    //}

    private void OnDrawing(object sender, EventArgs e)
    {
        isCleared = false;
    }

    private void DrawClicked(object sender, EventArgs e)
    {
        var imageView = (TransformViewModel)ImageView.BindingContext;
        imageView.IsPanningEnabled = false;
        imageView.IsPinchingEnabled = false;
        DrawView.WidthRequest = ImageView.Width;
        DrawView.HeightRequest = ImageView.Height;

        DrawView.InputTransparent = false;

        CheckBtn.IsVisible = true;
        EraseBtn.IsVisible = true;
        DrawBtn.IsVisible = false;
        PenSizeSlider.IsVisible = true;
        ColorPicker.IsVisible = true;
    }

    private void CheckClicked(object sender, EventArgs e)
    {
        var imageView = (TransformViewModel)ImageView.BindingContext;
        imageView.IsPanningEnabled = true;
        imageView.IsPinchingEnabled = true;

        DrawView.InputTransparent = true;

        CheckBtn.IsVisible = false;
        EraseBtn.IsVisible = false;
        DrawBtn.IsVisible = true;
        PenSizeSlider.IsVisible = false;
        ColorPicker.IsVisible = false;

        var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, ImgSource);
        var origPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "originals", ImgSource);
        var thumbPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, ImgSource);

        if (isCleared)
        {
            if (File.Exists(imgPath))
                File.Delete(imgPath);
            File.Move(origPath, imgPath);

            Thumbnail.Generate(imgPath, thumbPath);
        }
        else
        {
            if (!File.Exists(origPath))
                File.Copy(imgPath, origPath);
            _ = SaveDrawingView(imgPath);

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = true;
            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        isCleared = true;
        ImageView.Source = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "originals", ImgSource);
        DrawView.Clear();
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        DrawView.LineWidth = (int)e.NewValue;
    }

    private void ColorButtonClicked(object sender, EventArgs e)
    {
        var button = (Microsoft.Maui.Controls.Button)sender;
        DrawView.LineColor = button.BackgroundColor;
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Bild wirklich löschen?");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        if (result != null)
        {
            string file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Remove(ImgSource);
            // save data to file
            GlobalJson.SaveToFile();

            await Shell.Current.GoToAsync($"..?planId={PlanId}&pinId={PinId}&pinIcon={PinIcon}");
        }
    }

    private async Task SaveDrawingView(string filePath)
    {
        // Eckpunkte für Boundingbox
        var boundingBox = new DrawingLine
        {
            Points = [
                new(0, 0),
                new((float)DrawView.Width, 0),
                new((float)DrawView.Width, (float)DrawView.Height),
                new(0, (float)DrawView.Height),
                new(0, 0)],
            LineColor = Colors.Black,
            LineWidth = 1f
        };
        DrawView.Lines.Add(boundingBox);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Stream draw_stream = await DrawingView.GetImageStream(DrawView.Lines,
                                                            new Size(DrawView.Width, DrawView.Height),
                                                            Colors.Transparent,
                                                            cts.Token);
        if (draw_stream != null)
        {
            // Konvertiere den Stream zu einem SKBitmap für die Bearbeitung
            using var dwStream = new SKManagedStream(draw_stream);
            using var dwBitmap = SKBitmap.Decode(dwStream);

            using var origStream = File.OpenRead(filePath);
            using var origBitmap = SKBitmap.Decode(origStream);

            // Zuschneiden mit SKBitmap
            using var croppedBitmap = new SKBitmap((int)ImageView.Width, (int)ImageView.Height);
            using var canvas = new SKCanvas(croppedBitmap);

            var sourceRect = new SKRect((dwBitmap.Width - (int)DrawView.Width) / 2,
                                        (dwBitmap.Height - (int)DrawView.Height) / 2,
                                        (dwBitmap.Width - (int)DrawView.Width) / 2 + (int)DrawView.Width,
                                        (dwBitmap.Height - (int)DrawView.Height) / 2 + (int)DrawView.Height);
            var destRect = new SKRect(0, 0, (int)DrawView.Width, (int)DrawView.Height);
            canvas.DrawBitmap(origBitmap, sourceRect, destRect);
            canvas.DrawBitmap(dwBitmap, sourceRect, destRect);
            canvas.Flush();

            // Speichere das zugeschnittene Bild
            using var image = SKImage.FromBitmap(croppedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

            if (File.Exists(filePath))
                File.Delete(filePath);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            data.SaveTo(fileStream);
            fileStream.Close();

            var thumbPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, ImgSource);
            Thumbnail.Generate(filePath, thumbPath);
        }
    }
}
