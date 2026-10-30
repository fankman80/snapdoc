#nullable disable
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SnapDoc.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Models;
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
    private bool needsImageFit = false;
    private bool isNavigating = false;
    private double panStartX;
    private double panStartY;
    private bool isPanningActive = false;
    private double totalPanX = 0;
    private readonly TransformViewModel fotoContainer;
    private const double MaxDisplayEdge = 2048;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private DrawMode drawMode = DrawMode.None;
    private readonly bool isHatchEffect = false;
    private readonly float hatchStrokeWitdh = 2f;
    private readonly float hatchStrokeSpace = 8f;
    private readonly float hatchRotation = 45f;
    private int lineWidth = 3;
    private string strokeStyle = "";
    private float cloudRadius = 20;
    private float cloudInciseDeg = 15;
    private bool drawingOwnsTouch;

    private Color selectedBorderColor = new(0, 153, 0, 255);
    public Color SelectedBorderColor {
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
        drawingController.Invalidated += (_, _) => { DrawingView?.InvalidateSurface(); };

        SetupDesktopScrollWheel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }

    private void ImageViewContainer_SizeChanged(object sender, EventArgs e)
    {
        if (FotoContainer.Width < 10 || FotoContainer.Height < 10) return;

        if (!hasFittedImage || needsImageFit)
        {
            hasFittedImage = true;
            needsImageFit = false;
            Dispatcher.Dispatch(() => ImageFit(null, null));
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (ImgSource != null)
            return;

        if (query.TryGetValue("planId", out object value1)) PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2)) PinId = value2 as string;
        if (query.TryGetValue("pinIcon", out object value3)) PinIcon = value3 as string;
        if (query.TryGetValue("imgSource", out object value4))
        {
            ImgSource ??= value4 as string;
            string imgPath;
            if (ImgSource == "showTitle")
            {
                imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                this.Title = "Titelbild";
                DrawBtn.IsVisible = false;
            }
            else
            {
                imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
                var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
                string formattedDate = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
                this.Title = formattedDate;
            }

            // Asynchrones Laden mit Cloud-Download-Fallback
            Dispatcher.Dispatch(async () =>
            {
                if (!File.Exists(imgPath) && ImgSource != "showTitle")
                    await SaveManager.DownloadMediaOnDemandAsync(ImgSource, isThumbnail: false);

                if (File.Exists(imgPath))
                {
                    var bytes = await File.ReadAllBytesAsync(imgPath);

                    using (var ms = new MemoryStream(bytes))
                    using (var codec = SKCodec.Create(ms))
                    {
                        if (codec != null)
                        {
                            var (w, h) = ClampSize(codec.Info.Width, codec.Info.Height);
                            FotoContainer.WidthRequest = w;
                            FotoContainer.HeightRequest = h;
                        }
                    }

                    FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            });
        }
        if (query.TryGetValue("gotoBtn", out var value5))
            IsGotoPinBtnVisible = bool.TryParse(value5?.ToString(), out var result) && result;

        UpdateNavigationButtons();
    }

    public void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                isPanningActive = false;
                fotoContainer.IsPanningEnabled = false;
                break;

            case GestureStatus.Running:
                if (e.Scale <= 0 || double.IsNaN(e.Scale) || double.IsInfinity(e.Scale)) return;
                if (double.IsNaN(e.ScaleOrigin.X) || double.IsNaN(e.ScaleOrigin.Y)) return;

                double currentScale = fotoContainer.Scale;
                double targetScale = currentScale * e.Scale;
                targetScale = Math.Clamp(targetScale, minScale * 0.5, minScale * 15.0);

                double scaleFactor = targetScale / currentScale;
                double focusX = e.ScaleOrigin.X * this.Width;
                double focusY = e.ScaleOrigin.Y * this.Height;
                double imgWidth = FotoContainer.WidthRequest > 0 ? FotoContainer.WidthRequest : (FotoContainer.Width > 0 ? FotoContainer.Width : this.Width);
                double imgHeight = FotoContainer.HeightRequest > 0 ? FotoContainer.HeightRequest : (FotoContainer.Height > 0 ? FotoContainer.Height : this.Height);
                double centerX = (imgWidth / 2.0) + fotoContainer.TranslationX;
                double centerY = (imgHeight / 2.0) + fotoContainer.TranslationY;

                fotoContainer.TranslationX += (centerX - focusX) * (scaleFactor - 1.0);
                fotoContainer.TranslationY += (centerY - focusY) * (scaleFactor - 1.0);
                fotoContainer.Scale = targetScale;
                UpdateDrawingViewScale();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                isPanningActive = false;
                fotoContainer.IsPanningEnabled = true;

                if (fotoContainer.Scale < minScale - 0.001)
                    ImageFit(null, null);
                break;
        }
    }

    public void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        if (!fotoContainer.IsPanningEnabled) return;

        bool isAtMinScale = fotoContainer.Scale <= minScale + 0.01;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                isPanningActive = true;
                panStartX = fotoContainer.TranslationX;
                panStartY = fotoContainer.TranslationY;
                totalPanX = 0;
                break;

            case GestureStatus.Running:
                if (!isPanningActive) return;

                if (isAtMinScale)
                    totalPanX = e.TotalX;
                else
                {
                    if (!isPanningActive) return;

                    fotoContainer.TranslationX = panStartX + e.TotalX;
                    fotoContainer.TranslationY = panStartY + e.TotalY;
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (isAtMinScale)
                {
                    double threshold = 60;

                    if (totalPanX < -threshold)
                        NavigateImage(1);
                    else if (totalPanX > threshold)
                        NavigateImage(-1);
                }

                isPanningActive = false;
                totalPanX = 0;
                break;
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e) => ImageFit(null, null);

    private void SetupDesktopScrollWheel()
    {
        GestureContainer.HandlerChanged += (s, e) =>
        {
            if (GestureContainer.Handler?.PlatformView == null) return;

#if WINDOWS
        if (GestureContainer.Handler.PlatformView is Microsoft.UI.Xaml.FrameworkElement winView)
        {
            winView.PointerWheelChanged += (sender, args) =>
            {
                var point = args.GetCurrentPoint(winView);
                var mousePos = new Point(point.Position.X, point.Position.Y);
                int delta = point.Properties.MouseWheelDelta;

                HandleScrollWheel(mousePos, delta);
                args.Handled = true; // Event als verarbeitet markieren
            };
        }
#endif
        };
    }

    private void HandleScrollWheel(Point mousePos, int scrollDelta)
    {
        double zoomFactor;
        if (fotoContainer.Scale > 2)
            zoomFactor = scrollDelta > 0 ? 1.05 : 0.95;
        else if (fotoContainer.Scale > 1)
            zoomFactor = scrollDelta > 0 ? 1.1 : 0.9;
        else
            zoomFactor = scrollDelta > 0 ? 1.15 : 0.85;

        double targetScale = fotoContainer.Scale * zoomFactor;

        if (targetScale <= minScale + 0.01)
        {
            ImageFit(null, null);
            return;
        }

        targetScale = Math.Min(targetScale, minScale * 15.0);
        double scaleRatio = targetScale / fotoContainer.Scale;
        double originX = mousePos.X - (fotoContainer.TranslationX + FotoContainer.Width / 2);
        double originY = mousePos.Y - (fotoContainer.TranslationY + FotoContainer.Height / 2);

        fotoContainer.TranslationX -= originX * (scaleRatio - 1);
        fotoContainer.TranslationY -= originY * (scaleRatio - 1);

        fotoContainer.Scale = targetScale;
        UpdateDrawingViewScale();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}");
    }

    private void ImageFit(object sender, EventArgs e)
    {
        double w = FotoContainer.WidthRequest > 0 ? FotoContainer.WidthRequest : FotoContainer.Width;
        double h = FotoContainer.HeightRequest > 0 ? FotoContainer.HeightRequest : FotoContainer.Height;
        FitImageToDimensions(w, h);
    }

    private void FitImageToDimensions(double imgWidth, double imgHeight)
    {
        if (imgWidth <= 0 || imgHeight <= 0 || this.Width <= 0 || this.Height <= 0)
            return;

        var scale = Math.Min(this.Width / imgWidth, this.Height / imgHeight);
        minScale = scale;

        fotoContainer.AnchorX = 0.5;
        fotoContainer.AnchorY = 0.5;
        fotoContainer.Scale = scale;

        fotoContainer.TranslationX = (this.Width - imgWidth) / 2;
        fotoContainer.TranslationY = (this.Height - imgHeight) / 2;

        UpdateDrawingViewScale();
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_dieses_bild_wirklich_loeschen);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result?.Result is not DualPopupResult.Ok) return;

        if (ImgSource == "showTitle")
        {
            string oldTitleImage = GlobalJson.Data.TitleImage;

            if (!string.IsNullOrEmpty(oldTitleImage) && oldTitleImage != "banner_thumbnail.png")
            {
                string file1 = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, oldTitleImage);
                if (File.Exists(file1))
                    File.Delete(file1);

                string file2 = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, oldTitleImage);
                if (File.Exists(file2))
                    File.Delete(file2);

                // Cloud Cleanup
                _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ThumbnailPath}/{oldTitleImage}");
                _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ImagePath}/{oldTitleImage}");
            }
            ProjectItem.Current.TitleImage = "banner_thumbnail.png";
        }
        else
        {
            if (GlobalJson.Data.Plans.TryGetValue(PlanId, out var plan) &&
                plan.Pins.TryGetValue(PinId, out var pin) &&
                pin.Fotos.TryGetValue(ImgSource, out var fotoToDelete))
            {
                string fileName = string.IsNullOrEmpty(fotoToDelete.File) ? ImgSource : fotoToDelete.File;

                // Tombstone statt Remove – Cloud-Dateien folgen automatisch nach dem Sync
                SyncOps.DeleteFoto(PlanId, PinId, ImgSource);

                string projectDir = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath);
                foreach (var f in new[]
                {
                    Path.Combine(projectDir, GlobalJson.Data.ImagePath, "originals", fileName),
                    Path.Combine(projectDir, GlobalJson.Data.ImagePath, fileName),
                    Path.Combine(projectDir, GlobalJson.Data.ThumbnailPath, fileName)
                })
                {
                    if (File.Exists(f)) File.Delete(f);
                }
            }
        }

        // Daten lokal speichern und Sync-Trigger ausloesen
        SaveManager.NotifyDataChanged();

        await Shell.Current.GoToAsync($"..");
    }

    private async void DrawingClicked(object sender, EventArgs e) => await StartDrawing();

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
        DrawingView?.InvalidateSurface();
    }

    private Task StartDrawing(bool setDefaultMode = true)
    {
        fotoContainer.IsPanningEnabled = false;

        if (setDefaultMode)
        {
            drawingController.Reset();

            ShapeBtn.Text = MaterialIcons.Rectangle;
            AddCloudyBtn.Text = MaterialIcons.Cloud_off;
            SetDrawMode(DrawMode.Rect);
        }

        SettingsService.Instance.IsPinPlaceBtnManualHide = true;

        DrawBtn.IsVisible = false;
        IsToolButtonsVisible = true;

        drawingController.InitializeDrawing(
            SelectedBorderColor.ToSKColor(),
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            lineWidth,
            strokeStyle,
            isHatchEffect,
            hatchStrokeWitdh,
            hatchStrokeSpace,
            hatchRotation,
            rotationAngle: 0f,
            forceReset: setDefaultMode);

        // Fotos haben keinen realen Massstab
        drawingController.IsMeasureActive = false;
        drawingController.IsActive = true;
        UpdateDrawingViewScale();
        DrawingView.IsVisible = true;
        DrawingView.InvalidateSurface();

        return Task.CompletedTask;
    }

    private async void EraseClicked(object sender, EventArgs e)
    {
        drawingController.Reset();

        if (!GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
            return;

        string origPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath,
                                       GlobalJson.Data.ImagePath, "originals", ImgSource);

        if (!File.Exists(origPath))
            await SaveManager.DownloadMediaOnDemandAsync(ImgSource, $"{GlobalJson.Data.ImagePath}/originals");

        if (!File.Exists(origPath))
        {
            await SnackbarExtensions.ShowSafeAsync("Original nicht verfügbar – bitte online gehen.", includeDelay: true);
            return;
        }

        isCleared = true;
        var bytes = await File.ReadAllBytesAsync(origPath);
        FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
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
        if (result?.Result == null) return;

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
        DrawingView?.InvalidateSurface();
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

        AddCloudyBtn.Text =
            isCloud
                ? MaterialIcons.Cloud
                : MaterialIcons.Cloud_off;

        DrawingView.InvalidateSurface();
    }

    private void OnDrawingPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear(SKColors.Transparent);

        if (!drawingController.IsActive)
            return;

        drawingController.Draw(e.Surface.Canvas);
    }

    private void OnDrawingTouch(object sender, SKTouchEventArgs e)
    {
        if (!drawingController.IsActive)
            return;

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                drawingOwnsTouch =
                    drawingController.OnPressed(e.Location);

                e.Handled = drawingOwnsTouch;
                break;

            case SKTouchAction.Moved:
                if (drawingOwnsTouch)
                {
                    drawingController.OnMoved(e.Location);
                    e.Handled = true;
                }
                break;

            case SKTouchAction.Released:
                if (drawingOwnsTouch)
                {
                    drawingController.OnReleased();
                    drawingOwnsTouch = false;
                    e.Handled = true;
                }
                else
                {
                    drawingController.OnTap(e.Location);
                    e.Handled = true;
                }
                break;

            case SKTouchAction.Cancelled:
                drawingController.OnCancel();
                drawingOwnsTouch = false;
                break;
        }
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (!drawingController.IsEmpty() || isCleared)
        {
            var imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
            var origPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, "originals", ImgSource);
            var thumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, ImgSource);
            var uploads = new List<(string, string)>
            {
                (imgPath, GlobalJson.Data.ImagePath),
                (thumbPath, GlobalJson.Data.ThumbnailPath)
            };

            if (isCleared)
            {
                if (File.Exists(imgPath))
                    File.Delete(imgPath);
                File.Move(origPath, imgPath);

                var bytes = File.ReadAllBytes(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;

                // Original in der Cloud entfernen (wird nach dem JSON-Sync ausgefuehrt)
                SaveManager.QueueCloudDelete($"{GlobalJson.Data.ImagePath}/originals/{ImgSource}");
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(origPath)!);

                bool hadOverlay = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay;

                if (!File.Exists(origPath) && hadOverlay)
                    await SaveManager.DownloadMediaOnDemandAsync(ImgSource, $"{GlobalJson.Data.ImagePath}/originals");

                if (!File.Exists(origPath))
                {
                    if (hadOverlay)
                    {
                        // Sonst würde das bemalte Bild zum "Original"
                        await SnackbarExtensions.ShowSafeAsync("Original nicht verfügbar – bitte online gehen.", includeDelay: true);
                        return; // Zeichenmodus bleibt offen, später erneut versuchen
                    }
                    File.Copy(imgPath, origPath);
                }
                
                await SaveFotoWithOverlay(origPath, imgPath);

                var bytes = File.ReadAllBytes(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = true;

                uploads.Add((origPath, $"{GlobalJson.Data.ImagePath}/originals"));
            }

            var foto = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource];
            foto.File = Path.GetFileName(imgPath);
            foto.Touch();

            SaveManager.NotifyDataChanged(uploads);
        }

        // Cleanup drawing canvas
        fotoContainer.IsPanningEnabled = true;

        drawingController.IsActive = false;
        drawingController.OnCancel();

        DrawingView.IsVisible = false;
        DrawingView.InvalidateSurface();

        drawingOwnsTouch = false;
        drawMode = DrawMode.None;

        IsToolButtonsVisible = false;
        DrawBtn.IsVisible = true;
        isCleared = false;
    }

    public async Task SaveFotoWithOverlay(string fotoPath, string outputPath)
    {
        using var fotoStream = File.OpenRead(fotoPath);
        using var fotoBitmap = SKBitmap.Decode(fotoStream);

        if (fotoBitmap == null)
            return;

        int originalWidth = fotoBitmap.Width;
        int originalHeight = fotoBitmap.Height;

        var info = new SKImageInfo(
            originalWidth,
            originalHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);

        using var surface = SKSurface.Create(info);

        if (surface == null)
            return;

        var canvas = surface.Canvas;

        // Originalfoto
        canvas.DrawBitmap(fotoBitmap, new SKRect(0, 0, originalWidth, originalHeight), SKSamplingOptions.Default);

        if (!drawingController.IsEmpty())
        {
            float drawingWidth = (float)Math.Max(1, FotoContainer.Width);
            float drawingHeight = (float)Math.Max(1, FotoContainer.Height);
            float scaleX = originalWidth / drawingWidth;
            float scaleY = originalHeight / drawingHeight;

            canvas.Save();
            canvas.Scale(scaleX, scaleY);
            drawingController.RenderForExport(canvas);
            canvas.Restore();
        }

        canvas.Flush();

        using var finalImage = surface.Snapshot();
        using var data = finalImage.Encode(SKEncodedImageFormat.Jpeg, 90);
        await using var output = File.Create(outputPath);

        data.SaveTo(output);
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        bool isCloud = (AddCloudyBtn.Text == MaterialIcons.Cloud);
        var popup = new PopupStyleEditor(lineWidth, SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), strokeStyle, false, 2, 8, 45, cloudRadius, cloudInciseDeg, isCloud);
        var result = await this.ShowPopupAsync<PopupStyleReturn>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

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
            isHatchEffect,
            hatchStrokeWitdh,
            hatchStrokeSpace,
            hatchRotation,
            cloudRadius,
            cloudInciseDeg
        );
    }

    private void OnPreviousImageClicked(object sender, EventArgs e) => NavigateImage(-1);
    private void OnNextImageClicked(object sender, EventArgs e) => NavigateImage(1);

    private async void NavigateImage(int direction)
    {
        if (isNavigating || ImgSource == "showTitle")
            return;

        try
        {
            isNavigating = true;

            var sortedFotos = GetSortedFotos();
            if (sortedFotos.Count == 0) return;

            int currentIndex = sortedFotos.FindIndex(f =>
                f.PlanId == PlanId &&
                f.PinId == PinId &&
                (string.Equals(f.FotoKey, ImgSource, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetFileName(f.FotoKey), Path.GetFileName(ImgSource), StringComparison.OrdinalIgnoreCase)));

            if (currentIndex == -1)
            {
                currentIndex = sortedFotos.FindIndex(f =>
                    string.Equals(f.FotoKey, ImgSource, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileName(f.FotoKey), Path.GetFileName(ImgSource), StringComparison.OrdinalIgnoreCase));
            }

            if (currentIndex == -1) return;

            int newIndex = currentIndex + direction;
            if (newIndex >= 0 && newIndex < sortedFotos.Count)
            {
                var target = sortedFotos[newIndex];

                FotoContainer.Opacity = 0;
                FotoContainer.Source = null;

                PlanId = target.PlanId;
                PinId = target.PinId;
                ImgSource = target.FotoKey;

                string imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);

                Title = target.DateTime.ToString("d") + " / " + target.DateTime.ToString("HH:mm");

                if (!File.Exists(imgPath))
                    await SaveManager.DownloadMediaOnDemandAsync(ImgSource, isThumbnail: false);

                if (File.Exists(imgPath))
                {
                    var bytes = File.ReadAllBytes(imgPath);
                    using (var ms = new MemoryStream(bytes))
                    using (var codec = SKCodec.Create(ms))
                    {
                        if (codec != null)
                        {
                            double imgWidth = codec.Info.Width;
                            double imgHeight = codec.Info.Height;

                            var (w, h) = ClampSize(codec.Info.Width, codec.Info.Height);
                            FotoContainer.WidthRequest = w;
                            FotoContainer.HeightRequest = h;
                            FitImageToDimensions(w, h);
                        }
                    }
                    FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                    await Task.Delay(20);
                    await FotoContainer.FadeToAsync(1, 100, Easing.CubicOut);
                }
                UpdateNavigationButtons();
            }
        }
        finally
        {
            isNavigating = false;
        }
    }

    private void UpdateNavigationButtons()
    {
        if (ImgSource == "showTitle")
        {
            PrevImgBtn.IsVisible = false;
            NextImgBtn.IsVisible = false;
            return;
        }

        var sortedFotos = GetSortedFotos();

        int currentIndex = sortedFotos.FindIndex(f =>
            f.PlanId == PlanId &&
            f.PinId == PinId &&
            (string.Equals(f.FotoKey, ImgSource, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Path.GetFileName(f.FotoKey), Path.GetFileName(ImgSource), StringComparison.OrdinalIgnoreCase)));

        if (currentIndex == -1)
        {
            currentIndex = sortedFotos.FindIndex(f =>
                string.Equals(f.FotoKey, ImgSource, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(f.FotoKey), Path.GetFileName(ImgSource), StringComparison.OrdinalIgnoreCase));
        }

        PrevImgBtn.IsVisible = currentIndex > 0;
        NextImgBtn.IsVisible = currentIndex >= 0 && currentIndex < sortedFotos.Count - 1;
    }

    private void UpdateDrawingViewScale()
    {
        if (!drawingController.IsActive)
            return;

        float scale = (float)fotoContainer.Scale;

        if (!float.IsFinite(scale) || scale <= 0f)
            scale = 1f;

        drawingController.SetViewTransform(scale, 0f);
        DrawingView.InvalidateSurface();
    }

    private static List<(string PlanId, string PinId, string FotoKey, DateTime DateTime)> GetSortedFotos()
    {
        var result = new List<(string PlanId, string PinId, string FotoKey, DateTime DateTime)>();

        if (GlobalJson.Data?.Plans == null)
            return result;

        // Nur lebende Objekte: gelöschte Pläne, Pins und Fotos ausblenden.
        // ToList() erstellt eine Momentaufnahme, damit ein Merge im Hintergrund keine Exception auslöst.
        foreach (var (planId, plan) in SyncOps.LivePlans(GlobalJson.Data).ToList())
        {
            foreach (var (pinId, pin) in SyncOps.LivePins(plan).ToList())
            {
                foreach (var (fotoKey, foto) in SyncOps.LiveFotos(pin).ToList())
                    result.Add((planId, pinId, fotoKey, foto.DateTime));
            }
        }

        // Nach Aufnahmedatum sortieren
        return [.. result.OrderBy(f => f.DateTime)];
    }

    private static (double W, double H) ClampSize(double w, double h)
    {
        var f = Math.Min(1.0, MaxDisplayEdge / Math.Max(w, h));
        return (Math.Round(w * f), Math.Round(h * f));
    }
}
