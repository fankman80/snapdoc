#nullable disable

using bsm24.ViewModels;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using Mopups.Services;
using MR.Gestures;
using SkiaSharp;

namespace bsm24.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId { get; set; }
    public string PinId { get; set; }
    public string PinIcon { get; set; }
    public string ImgSource { get; set; }

    private bool isCleared = false;

    private readonly TransformViewModel imageViewContainer;

    public ImageViewPage()
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
        imageViewContainer = (TransformViewModel)ImageViewContainer.BindingContext;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        ImageViewContainer.PropertyChanged += ImageView_PropertyChanged;
    }

    private void ImageView_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewContainer.Width) || e.PropertyName == nameof(ImageViewContainer.Height))
        {
            if (ImageViewContainer.Width > 0 && ImageViewContainer.Height > 0)
            {
                ImageViewContainer.PropertyChanged -= ImageView_PropertyChanged;
                ImageFit();
            }   
        }
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
            var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
            string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
            this.Title = formattedDate;

            ImageView.Source = imgPath;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e)
    {
        ImageFit();
    }

    public void OnPinching(object sender, PinchEventArgs e)
    {
        imageViewContainer.IsPanningEnabled = false;
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        imageViewContainer.IsPanningEnabled = true;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (imageViewContainer.IsPanningEnabled)
        {
            var scaleSpeed = 1 / ImageViewContainer.Scale;
            double angle = ImageViewContainer.Rotation * Math.PI / 180.0;
            double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
            double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);
            imageViewContainer.TranslationX += deltaX * scaleSpeed;
            imageViewContainer.TranslationY += deltaY * scaleSpeed;
            imageViewContainer.AnchorX = 1 / ImageViewContainer.Width * ((this.Width / 2) - ImageViewContainer.TranslationX);
            imageViewContainer.AnchorY = 1 / ImageViewContainer.Height * ((this.Height / 2) - ImageViewContainer.TranslationY);
        }
    }

    private void ImageFit()
    {
        var scale = Math.Min(this.Width / ImageViewContainer.Width, this.Height / ImageViewContainer.Height);
        imageViewContainer.Scale = scale;
        imageViewContainer.TranslationX = (this.Width - ImageViewContainer.Width) / 2;
        imageViewContainer.TranslationY = (this.Height - ImageViewContainer.Height) / 2;
        imageViewContainer.AnchorX = 1 / ImageViewContainer.Width * ((this.Width / 2) - ImageViewContainer.TranslationX);
        imageViewContainer.AnchorY = 1 / ImageViewContainer.Height * ((this.Height / 2) - ImageViewContainer.TranslationY);
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
        imageViewContainer.IsPanningEnabled = false;
        imageViewContainer.IsPinchingEnabled = false;
        DrawView.WidthRequest = ImageViewContainer.Width;
        DrawView.HeightRequest = ImageViewContainer.Height;

        DrawView.InputTransparent = false;

        CheckBtn.IsVisible = true;
        EraseBtn.IsVisible = true;
        DrawBtn.IsVisible = false;
        PenSizeSlider.IsVisible = true;
        ColorPicker.IsVisible = true;
    }

    private void CheckClicked(object sender, EventArgs e)
    {
        imageViewContainer.IsPanningEnabled = true;
        imageViewContainer.IsPinchingEnabled = true;

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
            if (!Directory.Exists(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "originals")))
                Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "originals"));

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
        DrawView.Clear();
        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
        {
            isCleared = true;
            ImageView.Source = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "originals", ImgSource);
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
            // save data to file
            GlobalJson.SaveToFile();
        }
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
            using var dwStream = new SKManagedStream(draw_stream);
            using var dwBitmap = SKBitmap.Decode(dwStream);
            using var origStream = File.OpenRead(filePath);
            using var origBitmap = SKBitmap.Decode(origStream);
            double scaleFaktorW = (double)origBitmap.Width / dwBitmap.Width;
            double scaleFaktorH = (double)origBitmap.Height / dwBitmap.Height;
            var destRect = new SKRect(0, 0, (int)origBitmap.Width, (int)origBitmap.Height);

            using var croppedBitmap = new SKBitmap((int)(origBitmap.Width), (int)(origBitmap.Height));
            using var canvas = new SKCanvas(croppedBitmap);
            canvas.DrawBitmap(origBitmap, new SKPoint(0,0));
            canvas.DrawBitmap(dwBitmap, destRect);
            canvas.Flush();

            if (File.Exists(filePath))
                File.Delete(filePath);

            // Speichere das Bild als JPEG
            var image = SKImage.FromBitmap(croppedBitmap);
            var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            var newStream = File.Create(filePath);
            data.SaveTo(newStream);
            newStream.Close();

            var thumbPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, ImgSource);
            Thumbnail.Generate(filePath, thumbPath);
        }
    }
}
