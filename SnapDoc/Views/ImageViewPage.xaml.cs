#nullable disable

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Layouts;
using MR.Gestures;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public string PinIcon;
    public string ImgSource = null;
    private int lineWidth = 6;
    private float selectedOpacity = 0.5f;
    private bool isCleared = false;
    private bool hasFittedImage = false;
    private readonly TransformViewModel photoContainer;
    private readonly double density = DeviceDisplay.MainDisplayInfo.Density;

    // --- DrawingController + Canvas ---
    private readonly DrawingController drawingController;
    private SKCanvasView drawingView;

    // UI state
    private DrawMode drawMode = DrawMode.None;

    private Color selectedColor = new(255, 0, 0);
    public Color SelectedColor
    {
        get => selectedColor;
        set
        {
            selectedColor = value;
            OnPropertyChanged();
        }
    }

    public ImageViewPage()
    {
        InitializeComponent();
        PhotoContainer.SizeChanged += ImageViewContainer_SizeChanged;
        photoContainer = new TransformViewModel();
        BindingContext = photoContainer;

        drawingController = new DrawingController(photoContainer, density);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private void ImageViewContainer_SizeChanged(object sender, EventArgs e)
    {
        if (hasFittedImage) return;
        if (PhotoContainer.Width < 10 || PhotoContainer.Height < 10) return;

        hasFittedImage = true;
        Dispatcher.Dispatch(() => ImageFit(null, null));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1)) PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2)) PinId = value2 as string;
        if (query.TryGetValue("pinIcon", out object value3)) PinIcon = value3 as string;

        if (query.TryGetValue("imgSource", out object value4))
        {
            ImgSource ??= value4 as string;
            string imgPath;
            if (ImgSource == "showTitle")
            {
                imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                this.Title = "Titelbild";
            }
            else
            {
                imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
                var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
                string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
                this.Title = formattedDate;
            }
            PhotoImage.Source = imgPath;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e) => ImageFit(null, null);

    public void OnPinching(object sender, PinchEventArgs e)
    {
        photoContainer.IsPanningEnabled = false;
        drawingController.ResizePolyHandles();
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        photoContainer.IsPanningEnabled = true;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (!photoContainer.IsPanningEnabled) return;

        var scaleSpeed = 1 / PhotoContainer.Scale;
        double angle = PhotoContainer.Rotation * Math.PI / 180.0;
        double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
        double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);
        photoContainer.TranslationX += deltaX * scaleSpeed;
        photoContainer.TranslationY += deltaY * scaleSpeed;
        photoContainer.AnchorX = 1 / PhotoContainer.Width * ((this.Width / 2) - photoContainer.TranslationX);
        photoContainer.AnchorY = 1 / PhotoContainer.Height * ((this.Height / 2) - photoContainer.TranslationY);
    }

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        var mousePos = e.Center;

        double zoomFactor;
        if (photoContainer.Scale > 2)
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.05 : 0.95;
        else if (photoContainer.Scale > 1)
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9;
        else
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.15 : 0.85;

        double targetScale = photoContainer.Scale * zoomFactor;
        double newAnchorX = 1 / PhotoContainer.Width * (mousePos.X - photoContainer.TranslationX);
        double newAnchorY = 1 / PhotoContainer.Height * (mousePos.Y - photoContainer.TranslationY);
        double deltaTranslationX = (PhotoContainer.Width * (newAnchorX - photoContainer.AnchorX)) * (targetScale / photoContainer.Scale - 1);
        double deltaTranslationY = (PhotoContainer.Height * (newAnchorY - photoContainer.AnchorY)) * (targetScale / photoContainer.Scale - 1);

        photoContainer.AnchorX = newAnchorX;
        photoContainer.AnchorY = newAnchorY;
        photoContainer.TranslationX -= deltaTranslationX;
        photoContainer.TranslationY -= deltaTranslationY;
        photoContainer.Scale = targetScale;

        drawingController.ResizePolyHandles();
    }

    private void ImageFit(object sender, EventArgs e)
    {
        var scale = Math.Min(this.Width / PhotoContainer.Width, this.Height / PhotoContainer.Height);
        photoContainer.Scale = scale;
        photoContainer.TranslationX = (this.Width - PhotoContainer.Width) / 2;
        photoContainer.TranslationY = (this.Height - PhotoContainer.Height) / 2;
        photoContainer.AnchorX = 1 / PhotoContainer.Width * ((this.Width / 2) - photoContainer.TranslationX);
        photoContainer.AnchorY = 1 / PhotoContainer.Height * ((this.Height / 2) - photoContainer.TranslationY);
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Bild wirklich löschen?");
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result == null) return;

        if (ImgSource == "showTitle")
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
            if (File.Exists(file)) File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);
            if (File.Exists(file)) File.Delete(file);

            GlobalJson.Data.TitleImage = "banner_thumbnail.png";
            GlobalJson.SaveToFile();
            await Shell.Current.GoToAsync($"..");
        }
        else
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file)) File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file)) File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file)) File.Delete(file);

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Remove(ImgSource);
            GlobalJson.SaveToFile();
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}");
        }
    }

    private void DrawingClicked(object sender, EventArgs e)
    {
        DrawBtn.IsVisible = false;
        ToolBtns.IsVisible = true;

        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PhotoContainer");

        // 1) Canvas erzeugen und anhängen
        drawingView = drawingController.CreateCanvasView();
        absoluteLayout.Children.Add(drawingView);
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutBounds(drawingView, new Rect(0, 0, 1, 1));
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutFlags(drawingView, AbsoluteLayoutFlags.All);

        // 2) DrawingController initialisieren
        drawingController.InitializeDrawing(
            SelectedColor.ToSKColor(),
            lineWidth,
            SelectedColor.WithAlpha(selectedOpacity).ToSKColor(),
            (float)SettingsService.Instance.PolyLineHandleTouchRadius,
            (float)SettingsService.Instance.PolyLineHandleRadius,
            SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
            SKColor.Parse(SettingsService.Instance.PolyLineStartHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha)
        );

        // 3) initialer Modus
        drawingController.DrawMode = DrawMode.None;
        drawMode = DrawMode.None;
    }

    private void DrawFreeClicked(object sender, EventArgs e)
    {
        if (drawMode == DrawMode.Poly || drawMode == DrawMode.None)
        {
            photoContainer.IsPanningEnabled = false;
            drawMode = DrawMode.Free;
            drawingController.DrawMode = DrawMode.Free;
            DrawPolyBtn.CornerRadius = 30;
            DrawFreeBtn.CornerRadius = 10;
            drawingController.CombinedDrawable?.PolyDrawable?.DisplayHandles = false;
            drawingView?.InvalidateSurface();
        }
        else
        {
            photoContainer.IsPanningEnabled = true;
            drawMode = DrawMode.None;
            drawingController.DrawMode = DrawMode.None;
            DrawFreeBtn.CornerRadius = 30;
            drawingController.CombinedDrawable?.PolyDrawable?.DisplayHandles = false;
            drawingView?.InvalidateSurface();
        }
    }

    private void DrawPolyClicked(object sender, EventArgs e)
    {
        if (drawMode == DrawMode.Free || drawMode == DrawMode.None)
        {
            photoContainer.IsPanningEnabled = false;
            drawMode = DrawMode.Poly;
            drawingController.DrawMode = DrawMode.Poly;
            DrawPolyBtn.CornerRadius = 10;
            DrawFreeBtn.CornerRadius = 30;
            drawingController.CombinedDrawable?.PolyDrawable?.DisplayHandles = true;
            drawingController.ResizePolyHandles();
            drawingView?.InvalidateSurface();
        }
        else
        {
            photoContainer.IsPanningEnabled = true;
            drawMode = DrawMode.None;
            drawingController.DrawMode = DrawMode.None;
            DrawPolyBtn.CornerRadius = 30;
            drawingController.CombinedDrawable?.PolyDrawable?.DisplayHandles = false;
            drawingView?.InvalidateSurface();
        }
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        drawMode = DrawMode.None;
        DrawPolyBtn.CornerRadius = 30;
        DrawFreeBtn.CornerRadius = 30;
        drawingController.Reset();
        drawingView?.InvalidateSurface();

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
        {
            isCleared = true;
            PhotoImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
            GlobalJson.SaveToFile();
        }
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (drawingView != null && !drawingController.IsEmpty() || isCleared == true)
        {
            var imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
            var origPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
            var thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, ImgSource);

            if (isCleared)
            {
                if (File.Exists(imgPath)) File.Delete(imgPath);
                File.Move(origPath, imgPath);

                imgPath = await FileRenamer(imgPath);
                thumbPath = await FileRenamer(thumbPath);
                _ = await FileRenamer(origPath);

                PhotoImage.Source = imgPath;
                Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
            }
            else
            {
                if (!Directory.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals")))
                    Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals"));

                if (!File.Exists(origPath)) File.Copy(imgPath, origPath);

                // Save overlay: wir zeichnen die overlay auf overlayCanvas (ohne Handles)
                await SavePhotoWithOverlay(imgPath, imgPath);

                imgPath = await FileRenamer(imgPath);
                thumbPath = await FileRenamer(thumbPath);
                _ = await FileRenamer(origPath);

                PhotoImage.Source = imgPath;
                Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = true;
            }

            // ändere Json-Key
            var fotos = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos;
            if (fotos.TryGetValue(ImgSource, out var value))
            {
                fotos.Remove(ImgSource);
                fotos[Path.GetFileName(imgPath)] = value;
                ImgSource = Path.GetFileName(imgPath);
            }
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File = Path.GetFileName(imgPath);
            GlobalJson.SaveToFile();
        }

        // Cleanup drawing canvas
        drawingController.Detach();
        RemoveDrawingView();

        drawMode = DrawMode.None;
        DrawPolyBtn.CornerRadius = 30;
        DrawFreeBtn.CornerRadius = 30;
        photoContainer.IsPanningEnabled = true;
        ToolBtns.IsVisible = false;
        DrawBtn.IsVisible = true;

        isCleared = false;
    }

    private void RemoveDrawingView()
    {
        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PhotoContainer");
        if (drawingView != null && absoluteLayout != null)
        {
            absoluteLayout.Children.Remove(drawingView);
            drawingView = null;
        }
    }

    private static async Task<string> FileRenamer(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath).Split('_');
        var onlyPath = Path.GetDirectoryName(filePath);
        string newFileName;
        var extension = Path.GetExtension(filePath);

        if (name.Length == 3)
            newFileName = $"{name[0]}_{name[1]}_{name[2]}_{1}" + extension;
        else
        {
            if (Int32.TryParse(name[3], out int i))
                newFileName = $"{name[0]}_{name[1]}_{name[2]}_{i + 1}" + extension;
            else
                newFileName = $"{name[0]}_{name[1]}_{name[2]}_{0}" + extension;
        }

        if (File.Exists(Path.Combine(filePath)))
        {
            File.Move(filePath, Path.Combine(onlyPath, newFileName));
            return Path.Combine(onlyPath, newFileName);
        }
        else
            return filePath;
    }

    public async Task SavePhotoWithOverlay(string photoPath, string outputPath)
    {
        using var photoStream = File.OpenRead(photoPath);
        using var photoBitmap = SKBitmap.Decode(photoStream);

        int width = photoBitmap.Width;
        int height = photoBitmap.Height;

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.DrawBitmap(photoBitmap, new SKPoint(0, 0));

        // Overlay erstellen (ohne Handles)
        using (var overlaySurface = SKSurface.Create(info))
        {
            var overlayCanvas = overlaySurface.Canvas;
            overlayCanvas.Clear(SKColors.Transparent);

            float scaleX = width / drawingView.CanvasSize.Width;
            float scaleY = height / drawingView.CanvasSize.Height;
            overlayCanvas.Scale(scaleX, scaleY);

            // Zeichne ohne Handles auf overlayCanvas
            drawingController.DrawWithoutHandles(overlayCanvas);

            overlayCanvas.Flush();

            using var overlayImage = overlaySurface.Snapshot();
            var destRect = new SKRect(0, 0, width, height);
            canvas.DrawImage(overlayImage, destRect);
        }

        canvas.Flush();

        using var finalImage = surface.Snapshot();
        using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var output = File.Create(outputPath);
        data.SaveTo(output);
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(lineWidth, SelectedColor, fillOpacity: (byte)(selectedOpacity * 255), lineWidthVisibility: true, fillOpacityVisibility: true);
        var result = await this.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result == null) return;

        SelectedColor = Color.FromArgb(result.Result.PenColorHex);
        selectedOpacity = 1f / 255f * result.Result.FillOpacity;
        lineWidth = result.Result.PenWidth;

        drawingController?.UpdateDrawingStyles(
            SelectedColor.ToSKColor(),
            lineWidth,
            selectedOpacity
        );
    }
}