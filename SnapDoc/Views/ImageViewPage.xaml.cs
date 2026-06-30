#nullable disable
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using MR.Gestures;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.Views;

public partial class ImageViewPage : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public string PinIcon;
    public string ImgSource = null;
    private bool isCleared = false;
    private bool hasFittedImage = false;
    private double minScale = 0.1;
    private readonly TransformViewModel fotoContainer;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private SKCanvasView drawingView;
    private DrawMode drawMode = DrawMode.None;
    private int lineWidth = 8;
    private string strokeStyle = "";
    private float cloudRadius = 60;
    private float cloudInciseDeg = 15;

    private Color selectedBorderColor = new(0, 153, 0, 255);
    public Color SelectedBorderColor
    {
        get => selectedBorderColor;
        set
        {
            selectedBorderColor = value;
            OnPropertyChanged();
        }
    }

    private Color selectedFillColor = new(202, 255, 150, 128);
    public Color SelectedFillColor
    {
        get => selectedFillColor;
        set
        {
            selectedFillColor = value;
            OnPropertyChanged();
        }
    }

    private Color selectedTextColor = new(0, 0, 0, 255);
    public Color SelectedTextColor
    {
        get => selectedTextColor;
        set
        {
            selectedTextColor = value;
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

    private bool isToolButtonsVisible = false;
    public bool IsToolButtonsVisible
    {
        get => isToolButtonsVisible;
        set
        {
            isToolButtonsVisible = value;
            OnPropertyChanged();
        }
    }

    public ImageViewPage()
    {
        InitializeComponent();
        BindingContext = this;
        fotoContainer = new TransformViewModel();

        ImageDrawingCanvas.BindingContext = fotoContainer;
        GestureContainer.BindingContext = fotoContainer;

        FotoContainer.SizeChanged += ImageViewContainer_SizeChanged;
        drawingController = new DrawingController(fotoContainer);
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

            var bytes = File.ReadAllBytes(imgPath);
            FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
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
        
        if (fotoContainer.Scale < minScale)
            ImageFit(null, null);
        
        fotoContainer.IsPanningEnabled = true;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (!fotoContainer.IsPanningEnabled) return;

        var dragScale = 1.0 / fotoContainer.Scale;
        double angle = FotoContainer.Rotation * Math.PI / 180.0;
        double deltaX = (e.DeltaDistance.X * Math.Cos(angle) + e.DeltaDistance.Y * Math.Sin(angle)) * dragScale;
        double deltaY = (-e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle)) * dragScale;

        fotoContainer.TranslationX += deltaX;
        fotoContainer.TranslationY += deltaY;
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
        if (targetScale <= minScale)
        {
            ImageFit(null, null);
            return;
        }

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
        minScale = scale;
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

        if (result.Result == null) return;

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

    private async void DrawingClicked(object sender, EventArgs e)
    => await StartDrawing();

    private async void ShapeButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupShapeSelect();
        var temporaryOptions = new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = true,
            Shape = Settings.PopupOptions.Shape
        };
        var result = await this.ShowPopupAsync<object>(popup, temporaryOptions);

        if (result.WasDismissedByTappingOutsideOfPopup || result.Result is not int selectedShape) return;

        var mode = selectedShape switch
        {
            0 => DrawMode.Rect,
            1 => DrawMode.Oval,
            2 => DrawMode.Poly,
            3 => DrawMode.Arrow,
            4 => DrawMode.Free,
            _ => DrawMode.Rect
        };

        SetDrawMode(mode);

        if (selectedShape == 5)
            TextClicked(null, null);
    }

    private void SetDrawMode(DrawMode mode)
    {
        if (mode == DrawMode.None) return;

        drawMode = mode;
        drawingController.DrawMode = mode;

        AddTextBtn.IsVisible = mode is DrawMode.Rect or DrawMode.Oval;
        AddCloudyBtn.IsVisible = mode is DrawMode.Poly or DrawMode.Rect or DrawMode.Oval;

        var combined = drawingController.CombinedDrawable;

        (ShapeBtn.Text, bool isCloud) = mode switch
        {
            DrawMode.Rect => (MaterialIcons.Activity_zone, combined?.RectDrawable?.IsCloud ?? false),
            DrawMode.Oval => (MaterialIcons.Circle, combined?.OvalDrawable?.IsCloud ?? false),
            DrawMode.Poly => (MaterialIcons.Polyline, combined?.PolyDrawable?.IsCloud ?? false),
            DrawMode.Arrow => (MaterialIcons.Arrow_shape_up, false),
            DrawMode.Free => (MaterialIcons.Gesture, false),
            _ => (ShapeBtn.Text, false)
        };

        AddCloudyBtn.Text = isCloud ? MaterialIcons.Cloud : MaterialIcons.Cloud_off;

        if (combined != null)
        {
            combined.PolyDrawable.DisplayHandles = (mode == DrawMode.Poly);
            combined.RectDrawable.DisplayHandles = (mode == DrawMode.Rect);
            combined.OvalDrawable.DisplayHandles = (mode == DrawMode.Oval);
            combined.ArrowDrawable.DisplayHandles = (mode == DrawMode.Arrow);
        }

        drawingView?.InvalidateSurface();
    }

    private async Task StartDrawing(bool setDefaultMode = true)
    {
        fotoContainer.IsPanningEnabled = false;

        if (setDefaultMode)
        {
            ShapeBtn.Text = MaterialIcons.Rectangle;
            AddCloudyBtn.Text = MaterialIcons.Cloud_off;
            SetDrawMode(DrawMode.Rect);
        }

        SettingsService.Instance.IsPinPlaceBtnManualHide = true;
        DrawBtn.IsVisible = false;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var canvasContainer = this.FindByName<Microsoft.Maui.Controls.Grid>("ImageDrawingCanvas");

                if (drawingView != null)
                {
                    drawingController.Detach();
                    if (canvasContainer.Children.Contains(drawingView))
                        canvasContainer.Children.Remove(drawingView);
                    drawingView = null;
                }

                if (setDefaultMode)
                    drawingController.Reset();

                drawingView = drawingController.CreateCanvasView();
                drawingView.Opacity = 0;
                canvasContainer.Children.Add(drawingView);

                IsToolButtonsVisible = true;

                drawingController.InitializeDrawing(
                    SelectedBorderColor.ToSKColor(),
                    SelectedFillColor.ToSKColor(),
                    SelectedTextColor.ToSKColor(),
                    lineWidth,
                    strokeStyle,
                    forceReset: true
                );

                drawingView.InvalidateSurface();
                await Task.Yield(); // Kurz warten, bis der erste Frame berechnet ist
                drawingView.Opacity = 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Starten des Drawings: {ex.Message}");
                DrawBtn.IsVisible = true;
                IsToolButtonsVisible = false;
            }
        });
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        drawingController.Reset();

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
        {
            isCleared = true;
            var imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);

            FotoContainer.Source = ImageSource.FromStream(() =>
            {
                return File.OpenRead(imgPath);
            });

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void TextClicked(object sender, EventArgs e)
    {
        if (drawingController?.CombinedDrawable == null) return;

        var rectDrawable = drawingController.CombinedDrawable.RectDrawable;
        var ovalDrawable = drawingController.CombinedDrawable.OvalDrawable;
        string currentText;
        float textSize;
        bool autoSizeText;
        int textPadding;
        RectangleTextAlignment textAlignment;
        RectangleTextStyle textStyle;

        if (drawMode == DrawMode.Rect)
        {
            currentText = rectDrawable.Text;
            textSize = rectDrawable.TextSize;
            textAlignment = rectDrawable.TextAlignment;
            textStyle = rectDrawable.TextStyle;
            autoSizeText = rectDrawable.AutoSizeText;
            textPadding = rectDrawable.TextPadding;
        }
        else if (drawMode == DrawMode.Oval)
        {
            currentText = ovalDrawable.Text;
            textSize = ovalDrawable.TextSize;
            textAlignment = ovalDrawable.TextAlignment;
            textStyle = ovalDrawable.TextStyle;
            autoSizeText = ovalDrawable.AutoSizeText;
            textPadding = ovalDrawable.TextPadding;
        }
        else
            return; // Kein unterstützter Modus für Textbearbeitung

        var popup = new PopupTextEdit(textSize, textAlignment, textStyle, autoSizeText, currentText, textPadding, okText: AppResources.ok);
        var result = await this.ShowPopupAsync<TextEditReturn>(popup, Settings.PopupOptions);

        if (result?.Result != null)
        {
            if (drawMode == DrawMode.Rect)
            {
                rectDrawable.Text = result.Result.InputTxt;
                rectDrawable.TextSize = result.Result.FontSize;
                rectDrawable.TextAlignment = result.Result.Alignment;
                rectDrawable.TextStyle = result.Result.Style;
                rectDrawable.AutoSizeText = result.Result.AutoSize;
                rectDrawable.TextPadding = result.Result.TextPadding;
            }
            else if (drawMode == DrawMode.Oval)
            {
                ovalDrawable.Text = result.Result.InputTxt;
                ovalDrawable.TextSize = result.Result.FontSize;
                ovalDrawable.TextAlignment = result.Result.Alignment;
                ovalDrawable.TextStyle = result.Result.Style;
                ovalDrawable.AutoSizeText = result.Result.AutoSize;
                ovalDrawable.TextPadding = result.Result.TextPadding;
            }
        }

        drawingView?.InvalidateSurface();
    }

    private void CloudyClicked(object sender, EventArgs e)
    {
        var combined = drawingController?.CombinedDrawable;
        if (combined == null) return;

        bool isCloud = drawingController.DrawMode switch
        {
            DrawMode.Rect => combined.RectDrawable.IsCloud = !combined.RectDrawable.IsCloud,
            DrawMode.Oval => combined.OvalDrawable.IsCloud = !combined.OvalDrawable.IsCloud,
            DrawMode.Poly => combined.PolyDrawable.IsCloud = !combined.PolyDrawable.IsCloud,
            _ => false
        };

        AddCloudyBtn.Text = isCloud ? MaterialIcons.Cloud : MaterialIcons.Cloud_off;
        drawingView?.InvalidateSurface();
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
                if (File.Exists(imgPath))
                    File.Delete(imgPath);
                File.Move(origPath, imgPath);

                var bytes = File.ReadAllBytes(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
            }
            else
            {
                if (!Directory.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals")))
                    Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals"));

                if (!File.Exists(origPath))
                    File.Copy(imgPath, origPath);

                // Save overlay: wir zeichnen die overlay auf overlayCanvas (ohne Handles)
                await SaveFotoWithOverlay(imgPath, imgPath);

                var bytes = File.ReadAllBytes(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
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

            // save data to file
            GlobalJson.SaveToFile();
        }

        // Cleanup drawing canvas
        fotoContainer.IsPanningEnabled = true;
        drawingController.Detach();
        RemoveDrawingView();
        drawMode = DrawMode.None;
        SetDrawMode(drawMode);
        IsToolButtonsVisible = false;
        DrawBtn.IsVisible = true;
        isCleared = false;
    }

    private void RemoveDrawingView()
    {
        var canvasContainer = this.FindByName<Microsoft.Maui.Controls.Grid>("ImageDrawingCanvas");
        if (drawingView != null && canvasContainer != null)
        {
            canvasContainer.Children.Remove(drawingView);
            drawingView = null;
        }
    }

    public async Task SaveFotoWithOverlay(string fotoPath, string outputPath)
    {
        SKBitmap fotoBitmap;
    
        using (var fotoStream = File.OpenRead(fotoPath))
        {
            fotoBitmap = SKBitmap.Decode(fotoStream);
        }

        using (fotoBitmap)
        {
            int width = fotoBitmap.Width;
            int height = fotoBitmap.Height;

            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.DrawBitmap(fotoBitmap, new SKPoint(0, 0), SKSamplingOptions.Default);

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
                canvas.DrawImage(overlayImage, destRect, SKSamplingOptions.Default);
            }

            canvas.Flush();

            using var finalImage = surface.Snapshot();
            using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 90);
            using var output = File.Create(outputPath);
            data.SaveTo(output);
        }
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        bool isCloud = (AddCloudyBtn.Text == MaterialIcons.Cloud);
        var popup = new PopupStyleEditor(lineWidth, SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), strokeStyle, cloudRadius, cloudInciseDeg, isCloud);
        var result = await this.ShowPopupAsync<PopupStyleReturn>(popup, Settings.PopupOptions);

        if (result.Result == null) return;

        SelectedBorderColor = Color.FromArgb(result.Result.BorderColorHex);
        SelectedFillColor = Color.FromArgb(result.Result.FillColorHex);
        SelectedTextColor = Color.FromArgb(result.Result.TextColorHex);
        lineWidth = result.Result.PenWidth;
        strokeStyle = result.Result.StrokeStyle;
        cloudRadius = result.Result.CloudRadius;
        cloudInciseDeg = result.Result.CloudInciseDeg;

        drawingController?.UpdateDrawingStyles(
            SelectedBorderColor.ToSKColor(),
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            lineWidth,
            strokeStyle,
            cloudRadius,
            cloudInciseDeg
        );
    }
}
