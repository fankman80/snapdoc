#nullable disable

using bsm24.ViewModels;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Extensions;
using MR.Gestures;
using SkiaSharp;

namespace bsm24.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public string PinIcon;
    public string ImgSource;
    private int lineWidth = 15;
    private Color selectedColor = new(255, 0, 0);
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
        DrawView.LineWidth = lineWidth;
    }

    private void ImageView_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImageViewContainer.Width) || e.PropertyName == nameof(ImageViewContainer.Height))
        {
            if (ImageViewContainer.Width > 0 && ImageViewContainer.Height > 0)
            {
                ImageViewContainer.PropertyChanged -= ImageView_PropertyChanged;
                ImageFit(null, null);
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
            var imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
            var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
            string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
            this.Title = formattedDate;

            ImageView.Source = imgPath;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e)
    {
        ImageFit(null, null);
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

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        var mousePos = e.Center;

        // Dynamischer Zoomfaktor basierend auf der aktuellen Skalierung
        double zoomFactor;
        if (imageViewContainer.Scale > 2) // Sehr stark vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.05 : 0.95;  // Sehr langsame Zoom-Änderung
        else if (imageViewContainer.Scale > 1) // Moderat vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9;  // Langsame Zoom-Änderung
        else // Wenig vergrößert oder sehr klein
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.15 : 0.85;  // Moderate Zoom-Änderung

        double targetScale = imageViewContainer.Scale * zoomFactor; ;
        double newAnchorX = 1 / ImageViewContainer.Width * (mousePos.X - imageViewContainer.TranslationX);
        double newAnchorY = 1 / ImageViewContainer.Height * (mousePos.Y - imageViewContainer.TranslationY);
        double deltaTranslationX = (ImageViewContainer.Width * (newAnchorX - imageViewContainer.AnchorX)) * (targetScale / imageViewContainer.Scale - 1);
        double deltaTranslationY = (ImageViewContainer.Height * (newAnchorY - imageViewContainer.AnchorY)) * (targetScale / imageViewContainer.Scale - 1);

        imageViewContainer.AnchorX = newAnchorX;
        imageViewContainer.AnchorY = newAnchorY;
        imageViewContainer.TranslationX -= deltaTranslationX;
        imageViewContainer.TranslationY -= deltaTranslationY;
        imageViewContainer.Scale = targetScale;
    }

    private void ImageFit(object sender, EventArgs e)
    {
        var scale = Math.Min(this.Width / ImageViewContainer.Width, this.Height / ImageViewContainer.Height);
        imageViewContainer.Scale = scale;
        imageViewContainer.TranslationX = (this.Width - ImageViewContainer.Width) / 2;
        imageViewContainer.TranslationY = (this.Height - ImageViewContainer.Height) / 2;
        imageViewContainer.AnchorX = 1 / ImageViewContainer.Width * ((this.Width / 2) - ImageViewContainer.TranslationX);
        imageViewContainer.AnchorY = 1 / ImageViewContainer.Height * ((this.Height / 2) - ImageViewContainer.TranslationY);
    }

    private void OnDrawing(object sender, EventArgs e)
    {
        isCleared = false;
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(lineWidth, selectedColor);
        var result = await this.ShowPopupAsync<ColorPickerReturn>(popup, Settings.popupOptions);

        if (result.Result != null)
        {
            selectedColor = Color.FromArgb(result.Result.PenColorHex);
            lineWidth = result.Result.PenWidth;
        }

        DrawView.LineColor = selectedColor;
        DrawView.LineWidth = lineWidth;
    }

    private void DrawClicked(object sender, EventArgs e)
    {
        imageViewContainer.IsPanningEnabled = false;
        imageViewContainer.IsPinchingEnabled = false;
        DrawView.WidthRequest = ImageViewContainer.Width;
        DrawView.HeightRequest = ImageViewContainer.Height;

        DrawView.InputTransparent = false;

        PenSettingsBtn.IsVisible = true;
        CheckBtn.IsVisible = true;
        EraseBtn.IsVisible = true;
        DrawBtn.IsVisible = false;
    }

    private void CheckClicked(object sender, EventArgs e)
    {
        imageViewContainer.IsPanningEnabled = true;
        imageViewContainer.IsPinchingEnabled = true;

        DrawView.InputTransparent = true;

        PenSettingsBtn.IsVisible = false;
        CheckBtn.IsVisible = false;
        EraseBtn.IsVisible = false;
        DrawBtn.IsVisible = true;

        var imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
        var origPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
        var thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, ImgSource);

        if (isCleared)
        {
            if (File.Exists(imgPath))
                File.Delete(imgPath);
            File.Move(origPath, imgPath);
            
            Thumbnail.Generate(imgPath, thumbPath);
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
        }
        else
        {
            if (!Directory.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals")))
                Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals"));

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
            ImageView.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
            
            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Bild wirklich löschen?");
        var result = await this.ShowPopupAsync<string>(popup, Settings.popupOptions);

        if (result.Result != null)
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Remove(ImgSource);
            // save data to file
            GlobalJson.SaveToFile();

            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}&pinIcon={PinIcon}");
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

        await using var imageStream = await DrawingViewService.GetImageStream(
                                            ImageLineOptions.FullCanvas(DrawView.Lines,
                                            new Size(DrawView.Width, DrawView.Height),
                                            Brush.Transparent,
                                            new Size(DrawView.Width, DrawView.Height)));

        if (imageStream != null)
        {
            using var dwStream = new SKManagedStream(imageStream);
            using var dwBitmap = SKBitmap.Decode(dwStream);
            using var origStream = File.OpenRead(filePath);
            using var origBitmap = SKBitmap.Decode(origStream);
            var destRect = new SKRect(0, 0, (int)origBitmap.Width, (int)origBitmap.Height);

            using var mergedBitmap = new SKBitmap((int)(origBitmap.Width), (int)(origBitmap.Height));
            using var canvas = new SKCanvas(mergedBitmap);
            canvas.DrawBitmap(origBitmap, new SKPoint(0,0));
            canvas.DrawBitmap(dwBitmap, destRect);

            canvas.Flush();

            if (File.Exists(filePath))
                File.Delete(filePath);

            // Speichere das Bild als JPEG
            var image = SKImage.FromBitmap(mergedBitmap);
            var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            var newStream = File.Create(filePath);
            data.SaveTo(newStream);
            newStream.Close();

            var thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, ImgSource);
            Thumbnail.Generate(filePath, thumbPath);
        }
    }
}