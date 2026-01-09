#nullable disable

using CommunityToolkit.Maui.Extensions;
using Microsoft.Maui.Layouts;
using MR.Gestures;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using SnapDoc.DrawingTool;

namespace SnapDoc.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public string PinIcon;
    public string ImgSource = null;
    private bool isCleared = false;
    private bool hasFittedImage = false;
    private readonly TransformViewModel fotoContainer;
    private readonly double density = DeviceDisplay.MainDisplayInfo.Density;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private SKCanvasView drawingView;
    private DrawMode drawMode = DrawMode.None;
    private float selectedOpacity = 0.5f;
    private int lineWidth = 6;

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

    private bool isGotoPinBtnVisible = false;
    public bool IsGotoPinBtnVisible
    {
        get => isGotoPinBtnVisible;
        set
        {
            isGotoPinBtnVisible = value;
            OnPropertyChanged();
        }
    }

    public ImageViewPage()
    {
        InitializeComponent();

        BindingContext = this;
        fotoContainer = new TransformViewModel();

        FotoContainer.BindingContext = fotoContainer;
        GestureContainer.BindingContext = fotoContainer;

        FotoContainer.SizeChanged += ImageViewContainer_SizeChanged;

        drawingController = new DrawingController(fotoContainer, density);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private void ImageViewContainer_SizeChanged(object sender, EventArgs e)
    {
        if (hasFittedImage) return;
        if (FotoContainer.Width < 10 || FotoContainer.Height < 10) return;

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
                DrawBtn.IsVisible = false;
            }
            else
            {
                imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
                var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
                string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
                this.Title = formattedDate;
            }
            FotoImage.Source = imgPath;
        }
        if (query.TryGetValue("gotoBtn", out var value5))
            IsGotoPinBtnVisible = bool.TryParse(value5?.ToString(), out var result) && result;
    }

    public void OnDoubleTapped(object sender, EventArgs e) => ImageFit(null, null);

    public void OnPinching(object sender, PinchEventArgs e)
    {
        fotoContainer.IsPanningEnabled = false;
        drawingController.ResizeHandles();
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        fotoContainer.IsPanningEnabled = true;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (!fotoContainer.IsPanningEnabled) return;

        var scaleSpeed = 1 / FotoContainer.Scale;
        double angle = FotoContainer.Rotation * Math.PI / 180.0;
        double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
        double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);
        fotoContainer.TranslationX += deltaX * scaleSpeed;
        fotoContainer.TranslationY += deltaY * scaleSpeed;
        fotoContainer.AnchorX = 1 / FotoContainer.Width * ((this.Width / 2) - fotoContainer.TranslationX);
        fotoContainer.AnchorY = 1 / FotoContainer.Height * ((this.Height / 2) - fotoContainer.TranslationY);
    }

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        var mousePos = e.Center;

        double zoomFactor;
        if (fotoContainer.Scale > 2)
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.05 : 0.95;
        else if (fotoContainer.Scale > 1)
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9;
        else
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.15 : 0.85;

        double targetScale = fotoContainer.Scale * zoomFactor;
        double newAnchorX = 1 / FotoContainer.Width * (mousePos.X - fotoContainer.TranslationX);
        double newAnchorY = 1 / FotoContainer.Height * (mousePos.Y - fotoContainer.TranslationY);
        double deltaTranslationX = (FotoContainer.Width * (newAnchorX - fotoContainer.AnchorX)) * (targetScale / fotoContainer.Scale - 1);
        double deltaTranslationY = (FotoContainer.Height * (newAnchorY - fotoContainer.AnchorY)) * (targetScale / fotoContainer.Scale - 1);

        fotoContainer.AnchorX = newAnchorX;
        fotoContainer.AnchorY = newAnchorY;
        fotoContainer.TranslationX -= deltaTranslationX;
        fotoContainer.TranslationY -= deltaTranslationY;
        fotoContainer.Scale = targetScale;

        drawingController.ResizeHandles();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}");
    }

    private void ImageFit(object sender, EventArgs e)
    {
        var scale = Math.Min(this.Width / FotoContainer.Width, this.Height / FotoContainer.Height);
        fotoContainer.Scale = scale;
        fotoContainer.TranslationX = (this.Width - FotoContainer.Width) / 2;
        fotoContainer.TranslationY = (this.Height - FotoContainer.Height) / 2;
        fotoContainer.AnchorX = 1 / FotoContainer.Width * ((this.Width / 2) - fotoContainer.TranslationX);
        fotoContainer.AnchorY = 1 / FotoContainer.Height * ((this.Height / 2) - fotoContainer.TranslationY);
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_dieses_bild_wirklich_loeschen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result == null)
            return;

        if (ImgSource == "showTitle")
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.TitleImage = "banner_thumbnail.png";
        }
        else
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].File);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Remove(ImgSource);
        }

        // save data to file
        GlobalJson.SaveToFile();  

        await Shell.Current.GoToAsync($"..");
    }

    private void DrawFreeClicked(object sender, EventArgs e)
        => SetDrawMode(DrawMode.Free);

    private void DrawPolyClicked(object sender, EventArgs e)
        => SetDrawMode(DrawMode.Poly);

    private void DrawRectClicked(object sender, EventArgs e)
        => SetDrawMode(DrawMode.Rect);

    private void SetDrawMode(DrawMode mode)
    {
        bool activate = drawMode != mode;

        // DrawMode setzen
        drawMode = activate ? mode : DrawMode.None;
        drawingController.DrawMode = drawMode;

        // Panning
        fotoContainer.IsPanningEnabled = !activate;

        // Buttons reset
        DrawFreeBtn.CornerRadius = 30;
        DrawPolyBtn.CornerRadius = 30;
        DrawRectBtn.CornerRadius = 30;

        // Aktiver Button
        if (activate)
        {
            switch (mode)
            {
                case DrawMode.Free:
                    DrawFreeBtn.CornerRadius = 10;
                    break;

                case DrawMode.Poly:
                    DrawPolyBtn.CornerRadius = 10;
                    break;

                case DrawMode.Rect:
                    DrawRectangleBtn.CornerRadius = 10;
                    break;
            }
        }

        // Handles
        var combined = drawingController.CombinedDrawable;
        if (combined != null)
        {
            combined.PolyDrawable?.DisplayHandles = activate && mode == DrawMode.Poly;
            combined.RectDrawable?.DisplayHandles = activate && mode == DrawMode.Rectangle;
        }

        drawingView?.InvalidateSurface();
    }

    private void DrawingClicked(object sender, EventArgs e)
    {
        DrawBtn.IsVisible = false;
        ToolBtns.IsVisible = true;

        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("FotoContainer");

        // Canvas erzeugen und anhängen
        drawingView = drawingController.CreateCanvasView();
        absoluteLayout.Children.Add(drawingView);
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutBounds(drawingView, new Rect(0, 0, 1, 1));
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutFlags(drawingView, AbsoluteLayoutFlags.All);

        // DrawingController initialisieren
        drawingController.InitializeDrawing(
            SelectedColor.ToSKColor(),
            lineWidth,
            SelectedColor.WithAlpha(selectedOpacity).ToSKColor(),
            (float)SettingsService.Instance.PolyLineHandleTouchRadius,
            (float)SettingsService.Instance.PolyLineHandleRadius,
            SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
            SKColor.Parse(SettingsService.Instance.PolyLineStartHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha)
        );

        // initialer Modus
        drawingController.DrawMode = DrawMode.None;
        drawMode = DrawMode.None;
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        drawMode = DrawMode.None;
        DrawPolyBtn.CornerRadius = 30;
        DrawFreeBtn.CornerRadius = 30;
        DrawRectBtn.CornerRadius = 30;
        drawingController.Reset();
        drawingView?.InvalidateSurface();

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
        {
            isCleared = true;
            FotoImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
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

                FotoImage.Source = imgPath;
                Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
            }
            else
            {
                if (!Directory.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals")))
                    Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals"));

                if (!File.Exists(origPath)) File.Copy(imgPath, origPath);

                // Save overlay: wir zeichnen die overlay auf overlayCanvas (ohne Handles)
                await SaveFotoWithOverlay(imgPath, imgPath);

                imgPath = await FileRenamer(imgPath);
                thumbPath = await FileRenamer(thumbPath);
                _ = await FileRenamer(origPath);

                FotoImage.Source = imgPath;
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
        DrawRectBtn.CornerRadius = 30;
        fotoContainer.IsPanningEnabled = true;
        ToolBtns.IsVisible = false;
        DrawBtn.IsVisible = true;

        isCleared = false;
    }

    private void RemoveDrawingView()
    {
        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("FotoContainer");
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

    public async Task SaveFotoWithOverlay(string fotoPath, string outputPath)
    {
        using var fotoStream = File.OpenRead(fotoPath);
        using var fotoBitmap = SKBitmap.Decode(fotoStream);

        int width = fotoBitmap.Width;
        int height = fotoBitmap.Height;

        var info = new SKImageInfo(width, height);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.DrawBitmap(fotoBitmap, new SKPoint(0, 0));

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
