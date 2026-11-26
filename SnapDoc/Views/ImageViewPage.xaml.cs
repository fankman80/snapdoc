#nullable disable
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SnapDoc.DrawingTool;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.Services.Cloud;
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
    private bool canDraw = true;
    private double panStartX;
    private double panStartY;
    private bool isPanningActive = false;
    private double totalPanX = 0;
    private readonly TransformViewModel fotoContainer;
    private readonly BackButtonBehavior backBehavior;
    private const double MaxDisplayEdge = 2048;
    private bool drawingRedrawPending;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private bool drawingOwnsTouch;
    private const float PhotoDrawingStyleScale = 10f;

    private bool isGotoPinBtnVisible = false;
    public bool IsGotoPinBtnVisible
    {
        get => isGotoPinBtnVisible;
        set { isGotoPinBtnVisible = value; OnPropertyChanged(); }
    }

    private bool isEditMode = false;
    public bool IsEditMode
    {
        get => isEditMode;
        set
        {
            if (isEditMode == value) return;
            isEditMode = value;
            backBehavior.IsVisible = !value;   // im Bearbeitungsmodus ersetzt ✕ den Zurück-Pfeil
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsViewMode));
        }
    }

    public bool IsViewMode => !IsEditMode;

    public ImageViewPage()
    {
        InitializeComponent();
        BindingContext = this;

        backBehavior = new BackButtonBehavior { Command = new Command(async () => await OnBackRequestedAsync()) };
        Shell.SetBackButtonBehavior(this, backBehavior);

        fotoContainer = new TransformViewModel();
        ImageDrawingCanvas.BindingContext = fotoContainer;
        GestureContainer.BindingContext = fotoContainer;

        // Die Zeichenfläche ist nicht mehr transformiert → bei Zoom/Pan selbst neu zeichnen
        fotoContainer.PropertyChanged += (_, _) =>
        {
            if (drawingController?.IsActive == true)
                RequestDrawingRedraw();
        };

        FotoContainer.SizeChanged += ImageViewContainer_SizeChanged;
        GestureContainer.SizeChanged += GestureContainer_SizeChanged;

        drawingController = new DrawingController(fotoContainer);
        drawingController.SetStyleScale(PhotoDrawingStyleScale);
        drawingController.SetHandleBaseScale(1f);
        drawingController.Invalidated += (_, _) => RequestDrawingRedraw();

        // Gemeinsame Zeichen-UI
        DrawToolbar.Attach(drawingController);
        DrawToolbar.RedrawRequested += (_, _) => RequestDrawingRedraw();
        DrawToolbar.Erased += OnDrawingErased;
        DrawToolbar.BarsHeightChanged += (_, h) => UpdateImageArea(h);

        SetupDesktopScrollWheel();
    }

    private async Task OnBackRequestedAsync()
    {
        if (IsEditMode)
        {
            await CancelDrawingAsync();
            return;
        }
        await Shell.Current.GoToAsync("..");
    }

    protected override bool OnBackButtonPressed()
    {
        if (DrawToolbar.CloseOpenSheet()) return true;
        if (IsEditMode) { _ = CancelDrawingAsync(); return true; }
        return base.OnBackButtonPressed();
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
                Title = "Titelbild";
                canDraw = false;
                DrawBtn.IsVisible = false;
            }
            else
            {
                imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
                var dateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].DateTime;
                Title = dateTime.ToString("d") + " / " + dateTime.ToString("HH:mm");
            }

            // Asynchrones Laden mit Cloud-Download-Fallback
            Dispatcher.Dispatch(async () =>
            {
                if (!File.Exists(imgPath) && ImgSource != "showTitle")
                    await SaveManager.DownloadMediaOnDemandAsync(ImgSource, isThumbnail: false);

                if (File.Exists(imgPath))
                    await LoadImageAsync(imgPath, fit: false);
            });
        }

        if (query.TryGetValue("gotoBtn", out var value5))
            IsGotoPinBtnVisible = bool.TryParse(value5?.ToString(), out var result) && result;

        UpdateNavigationButtons();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}");
    }

    private async Task LoadImageAsync(string imgPath, bool fit)
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

                if (fit)
                    FitImageToDimensions(w, h);
            }
        }

        FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
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

    private double lastGestureWidth, lastGestureHeight;

    private void GestureContainer_SizeChanged(object sender, EventArgs e)
    {
        if (!hasFittedImage || GestureContainer.Width < 10 || GestureContainer.Height < 10)
            return;

        if (Math.Abs(GestureContainer.Width - lastGestureWidth) < 1 &&
            Math.Abs(GestureContainer.Height - lastGestureHeight) < 1)
            return;

        lastGestureWidth = GestureContainer.Width;
        lastGestureHeight = GestureContainer.Height;

        bool wasFitted = Math.Abs(fotoContainer.Scale - minScale) < 0.001;
        if (wasFitted)
            Dispatcher.Dispatch(() => ImageFit(null, null));
    }

    private void UpdateImageArea(double barsHeight)
    {
        double bottom = IsEditMode ? Math.Round(barsHeight) : 0;
        if (Math.Abs(GestureContainer.Margin.Bottom - bottom) < 1)
            return;   // nichts geändert → kein Layout

        GestureContainer.Margin = new Thickness(0, 0, 0, bottom);
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
                double targetScale = Math.Clamp(currentScale * e.Scale, minScale * 0.5, minScale * 15.0);
                double scaleFactor = targetScale / currentScale;

                double focusX = e.ScaleOrigin.X * GestureContainer.Width;
                double focusY = e.ScaleOrigin.Y * GestureContainer.Height;

                double imgWidth = FotoContainer.WidthRequest > 0 ? FotoContainer.WidthRequest : (FotoContainer.Width > 0 ? FotoContainer.Width : GestureContainer.Width);
                double imgHeight = FotoContainer.HeightRequest > 0 ? FotoContainer.HeightRequest : (FotoContainer.Height > 0 ? FotoContainer.Height : GestureContainer.Height);

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
                // Im Zeichenmodus bleibt Panning deaktiviert
                fotoContainer.IsPanningEnabled = !IsEditMode;

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
                    fotoContainer.TranslationX = panStartX + e.TotalX;
                    fotoContainer.TranslationY = panStartY + e.TotalY;
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (isAtMinScale)
                {
                    const double threshold = 60;
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
                    HandleScrollWheel(mousePos, point.Properties.MouseWheelDelta);
                    args.Handled = true;
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

    private void ImageFit(object sender, EventArgs e)
    {
        double w = FotoContainer.WidthRequest > 0 ? FotoContainer.WidthRequest : FotoContainer.Width;
        double h = FotoContainer.HeightRequest > 0 ? FotoContainer.HeightRequest : FotoContainer.Height;
        FitImageToDimensions(w, h);
    }

    private void FitImageToDimensions(double imgWidth, double imgHeight)
    {
        double areaW = GestureContainer.Width;
        double areaH = GestureContainer.Height;
        if (imgWidth <= 0 || imgHeight <= 0 || areaW <= 0 || areaH <= 0)
            return;

        var scale = Math.Min(areaW / imgWidth, areaH / imgHeight);
        minScale = scale;

        fotoContainer.AnchorX = 0.5;
        fotoContainer.AnchorY = 0.5;
        fotoContainer.Scale = scale;
        fotoContainer.TranslationX = (areaW - imgWidth) / 2;
        fotoContainer.TranslationY = (areaH - imgHeight) / 2;

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
                if (File.Exists(file1)) File.Delete(file1);

                string file2 = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, oldTitleImage);
                if (File.Exists(file2)) File.Delete(file2);

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

        SaveManager.NotifyDataChanged();
        await Shell.Current.GoToAsync("..");
    }

    // ===================== Zeichnen =====================

    private async void DrawingClicked(object sender, EventArgs e) => await StartDrawing();

    private Task StartDrawing(bool setDefaultMode = true)
    {
        fotoContainer.IsPanningEnabled = false;

        if (setDefaultMode)
            drawingController.Reset();

        drawingController.SetStyleScale(PhotoDrawingStyleScale);

        SettingsService.Instance.IsPinPlaceBtnManualHide = true;
        DrawBtn.IsVisible = false;
        IsEditMode = true;
        UpdateNavigationButtons();

        DrawToolbar.InitializeDrawing(setDefaultMode);

        drawingController.IsMeasureActive = false;
        drawingController.IsActive = true;

        UpdateDrawingViewScale();
        DrawingView.IsVisible = true;
        DrawingView.InvalidateSurface();

        Dispatcher.Dispatch(() => UpdateImageArea(DrawToolbar.BarsHeight));
        return Task.CompletedTask;
    }

    /// <summary>Foto-spezifischer Teil von "Löschen": Original ohne Overlay anzeigen.</summary>
    private async void OnDrawingErased(object sender, EventArgs e)
    {
        if (!GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
            return;

        string origPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath,
                                       GlobalJson.Data.ImagePath, "originals", ImgSource);

        if (!File.Exists(origPath))
            await SaveManager.DownloadMediaOnDemandAsync(ImgSource, $"{GlobalJson.Data.ImagePath}/originals");

        if (!File.Exists(origPath))
        {
            await this.DisplaySnackbar(AppResources.original_nicht_verfuegbar, actionButtonText: AppResources.ok);
            return;
        }

        isCleared = true;
        var bytes = await File.ReadAllBytesAsync(origPath);
        FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    private async void CancelClicked(object sender, EventArgs e) => await CancelDrawingAsync();

    private async Task CancelDrawingAsync()
    {
        drawingController.Reset();
        await DrawToolbar.CloseSheetsAsync();

        if (isCleared)
        {
            var imgPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, ImgSource);
            if (File.Exists(imgPath))
            {
                var bytes = await File.ReadAllBytesAsync(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            }
        }

        ExitDrawingMode();
    }

    private void ExitDrawingMode()
    {
        fotoContainer.IsPanningEnabled = true;
        drawingController.IsActive = false;
        drawingController.OnCancel();

        DrawingView.IsVisible = false;
        DrawingView.InvalidateSurface();
        drawingOwnsTouch = false;
        activeTouches.Clear();
        tapCandidate = false;

        DrawToolbar.Reset();
        isCleared = false;
        IsEditMode = false;
        UpdateImageArea(0);

        DrawBtn.IsVisible = canDraw;
        UpdateNavigationButtons();
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        await DrawToolbar.CloseSheetsAsync();

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

                var bytes = await File.ReadAllBytesAsync(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = false;
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
                        await this.DisplaySnackbar(AppResources.original_nicht_verfuegbar, actionButtonText: AppResources.ok);
                        return;
                    }
                    File.Copy(imgPath, origPath);
                }

                bool saved = await SaveFotoWithOverlay(origPath, imgPath);
                if (!saved)
                {
                    await this.DisplaySnackbar(AppResources.zeichnung_konnte_nicht_gespeichert_werden, actionButtonText: AppResources.ok);
                    return;
                }

                var bytes = await File.ReadAllBytesAsync(imgPath);
                FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));

                await Thumbnail.Generate(imgPath, thumbPath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay = true;

                uploads.Add((origPath, $"{GlobalJson.Data.ImagePath}/originals"));
            }

            var foto = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource];
            foto.File = Path.GetFileName(imgPath);
            foto.Touch();

            SaveManager.NotifyDataChanged([.. uploads]);
        }

        ExitDrawingMode();
    }

    private async Task<bool> SaveFotoWithOverlay(string fotoPath, string outputPath)
    {
        if (!File.Exists(fotoPath))
            return false;

        string tempPath = outputPath + ".drawing.tmp";

        try
        {
            using var fotoStream = File.OpenRead(fotoPath);
            using var fotoBitmap = SKBitmap.Decode(fotoStream);
            if (fotoBitmap == null)
                return false;

            int originalWidth = fotoBitmap.Width;
            int originalHeight = fotoBitmap.Height;

            var imgSize = ImageSizeDp;
            float drawingWidth = imgSize.Width;
            float drawingHeight = imgSize.Height;
            if (drawingWidth <= 0f || drawingHeight <= 0f)
                return false;

            var info = new SKImageInfo(originalWidth, originalHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            if (surface == null)
                return false;

            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(fotoBitmap, new SKRect(0, 0, originalWidth, originalHeight), SKSamplingOptions.Default);

            if (!drawingController.IsEmpty())
            {
                canvas.Save();
                canvas.Scale(originalWidth / drawingWidth, originalHeight / drawingHeight);
                drawingController.RenderForExport(canvas);
                canvas.Restore();
            }

            canvas.Flush();

            using var finalImage = surface.Snapshot();
            SKEncodedImageFormat format = Path.GetExtension(outputPath).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? SKEncodedImageFormat.Png
                : SKEncodedImageFormat.Jpeg;

            using var data = finalImage.Encode(format, 90);
            if (data == null)
                return false;

            await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                data.SaveTo(output);
                await output.FlushAsync();
            }

            if (!File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                return false;

            File.Move(tempPath, outputPath, overwrite: true);
            return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern des Foto-Overlays: {ex}");
            return false;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* beim nächsten Start bereinigen */ }
            }
        }
    }

    // ---------- Bild ↔ Bildschirm ----------

    /// <summary>Angezeigte Fotogrösse in dp (= Koordinatensystem der Zeichnung).</summary>
    private SKSize ImageSizeDp => new(
        (float)(FotoContainer.WidthRequest > 0 ? FotoContainer.WidthRequest : FotoContainer.Width),
        (float)(FotoContainer.HeightRequest > 0 ? FotoContainer.HeightRequest : FotoContainer.Height));

    /// <summary>
    /// Bildet die View-Transformation von ImageDrawingCanvas nach
    /// (Anchor → Scale → Translation), damit die Zeichnung exakt über dem Foto liegt.
    /// </summary>
    private SKMatrix GetImageToScreenMatrix()
    {
        var size = ImageSizeDp;
        float s = (float)fotoContainer.Scale;
        if (!float.IsFinite(s) || s <= 0f) s = 1f;

        float ax = size.Width * (float)fotoContainer.AnchorX;
        float ay = size.Height * (float)fotoContainer.AnchorY;
        float tx = (float)fotoContainer.TranslationX;
        float ty = (float)fotoContainer.TranslationY;
        if (!float.IsFinite(ax)) ax = 0f;
        if (!float.IsFinite(ay)) ay = 0f;
        if (!float.IsFinite(tx)) tx = 0f;
        if (!float.IsFinite(ty)) ty = 0f;

        return SKMatrix.CreateTranslation(-ax, -ay)
            .PostConcat(SKMatrix.CreateScale(s, s))
            .PostConcat(SKMatrix.CreateTranslation(ax + tx, ay + ty));
    }

    private SKPoint ScreenToImage(SKPoint p) =>
        GetImageToScreenMatrix().TryInvert(out var inv) ? inv.MapPoint(p) : p;

    private void OnDrawingPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (!drawingController.IsActive)
            return;

        var size = ImageSizeDp;
        if (size.Width <= 0f || size.Height <= 0f)
            return;

        canvas.Save();
        canvas.Concat(GetImageToScreenMatrix());   // Concat statt SetMatrix: Density-Skalierung bleibt erhalten
        canvas.ClipRect(new SKRect(0, 0, size.Width, size.Height));
        drawingController.Draw(canvas);
        canvas.Restore();
    }

    // ---------- Touch im Zeichenmodus: Zeichnen, Pan und Pinch in einem Handler ----------

    private readonly Dictionary<long, SKPoint> activeTouches = [];
    private bool tapCandidate;
    private SKPoint tapStart;
    private SKPoint panLast;
    private float pinchLastDist;
    private SKPoint pinchLastCenter;
    private const float TapSlop = 8f;   // dp: darunter gilt es als Tipp, nicht als Pan

    private void OnDrawingTouch(object sender, SKTouchEventArgs e)
    {
        if (!drawingController.IsActive)
            return;

        var p = e.Location;   // Bildschirm-dp (IgnorePixelScaling)

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                activeTouches[e.Id] = p;

                if (activeTouches.Count == 1)
                {
                    drawingOwnsTouch = drawingController.OnPressed(ScreenToImage(p));
                    if (!drawingOwnsTouch)
                    {
                        tapCandidate = true;
                        tapStart = p;
                        panLast = p;
                    }
                }
                else if (!drawingOwnsTouch)
                {
                    // Zweiter Finger → Pinch
                    tapCandidate = false;
                    StartPinch();
                }
                e.Handled = true;
                break;

            case SKTouchAction.Moved:
                if (!activeTouches.ContainsKey(e.Id))
                    break;   // z. B. Maus-Hover unter Windows

                activeTouches[e.Id] = p;

                if (drawingOwnsTouch)
                    drawingController.OnMoved(ScreenToImage(p));
                else if (activeTouches.Count >= 2)
                    UpdatePinch();
                else
                {
                    if (tapCandidate && SKPoint.Distance(p, tapStart) > TapSlop)
                        tapCandidate = false;

                    if (!tapCandidate)
                        PanBy(p.X - panLast.X, p.Y - panLast.Y);

                    panLast = p;
                }
                e.Handled = true;
                break;

            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                bool wasKnown = activeTouches.Remove(e.Id);

                if (drawingOwnsTouch)
                {
                    if (e.ActionType == SKTouchAction.Released)
                        drawingController.OnReleased();
                    else
                        drawingController.OnCancel();
                    drawingOwnsTouch = false;
                }
                else if (wasKnown && e.ActionType == SKTouchAction.Released &&
                         tapCandidate && activeTouches.Count == 0)
                {
                    drawingController.OnTap(ScreenToImage(p));
                }

                if (activeTouches.Count == 1)
                {
                    // Nach dem Pinch mit dem verbleibenden Finger weiter pannen
                    panLast = activeTouches.Values.First();
                    tapCandidate = false;
                }
                else if (activeTouches.Count >= 2)
                    StartPinch();

                if (activeTouches.Count == 0)
                {
                    tapCandidate = false;
                    if (fotoContainer.Scale < minScale - 0.001)
                        ImageFit(null, null);
                }
                e.Handled = true;
                break;
        }
    }

    private (SKPoint A, SKPoint B) FirstTwoTouches()
    {
        using var it = activeTouches.Values.GetEnumerator();
        it.MoveNext(); var a = it.Current;
        it.MoveNext(); var b = it.Current;
        return (a, b);
    }

    private void StartPinch()
    {
        var (a, b) = FirstTwoTouches();
        pinchLastDist = SKPoint.Distance(a, b);
        pinchLastCenter = new SKPoint((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
    }

    private void UpdatePinch()
    {
        var (a, b) = FirstTwoTouches();
        float dist = SKPoint.Distance(a, b);
        var center = new SKPoint((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);

        // Mitverschieben mit beiden Fingern
        PanBy(center.X - pinchLastCenter.X, center.Y - pinchLastCenter.Y);

        if (pinchLastDist > 1f && dist > 1f)
            ZoomAt(center, dist / pinchLastDist);

        pinchLastDist = dist;
        pinchLastCenter = center;
    }

    private void PanBy(float dx, float dy)
    {
        if (!float.IsFinite(dx) || !float.IsFinite(dy))
            return;

        fotoContainer.TranslationX += dx;
        fotoContainer.TranslationY += dy;
    }

    /// <summary>Zoomt so, dass der Bildschirmpunkt <paramref name="c"/> fix bleibt.</summary>
    private void ZoomAt(SKPoint c, float factor)
    {
        if (!float.IsFinite(factor) || factor <= 0f)
            return;

        double s = fotoContainer.Scale;
        double target = Math.Clamp(s * factor, minScale * 0.5, minScale * 15.0);
        if (Math.Abs(target - s) < 1e-6)
            return;

        var size = ImageSizeDp;
        double ax = size.Width * fotoContainer.AnchorX;
        double ay = size.Height * fotoContainer.AnchorY;
        double ratio = target / s;

        // Bildschirm = (Bild - a) * s + a + t  →  t' = c - a - (c - a - t) * s'/s
        fotoContainer.TranslationX = c.X - ax - (c.X - ax - fotoContainer.TranslationX) * ratio;
        fotoContainer.TranslationY = c.Y - ay - (c.Y - ay - fotoContainer.TranslationY) * ratio;
        fotoContainer.Scale = target;

        UpdateDrawingViewScale();
    }

    private void UpdateDrawingViewScale()
    {
        if (!drawingController.IsActive)
            return;

        float scale = (float)fotoContainer.Scale;
        if (!float.IsFinite(scale) || scale <= 0f)
            scale = 1f;

        drawingController.SetViewTransform(scale, 0f);
        RequestDrawingRedraw();
    }

    // ===================== Navigation =====================

    private void OnPreviousImageClicked(object sender, EventArgs e) => NavigateImage(-1);
    private void OnNextImageClicked(object sender, EventArgs e) => NavigateImage(1);

    private async void NavigateImage(int direction)
    {
        if (isNavigating || IsEditMode || ImgSource == "showTitle")
            return;

        try
        {
            isNavigating = true;

            var sortedFotos = GetSortedFotos();
            int currentIndex = FindCurrentIndex(sortedFotos);
            if (currentIndex == -1) return;

            int newIndex = currentIndex + direction;
            if (newIndex < 0 || newIndex >= sortedFotos.Count) return;

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
                await LoadImageAsync(imgPath, fit: true);
                await Task.Delay(20);
                await FotoContainer.FadeToAsync(1, 100, Easing.CubicOut);
            }

            UpdateNavigationButtons();
        }
        finally
        {
            isNavigating = false;
        }
    }

    private void UpdateNavigationButtons()
    {
        if (ImgSource == "showTitle" || IsEditMode)
        {
            PrevImgBtn.IsVisible = false;
            NextImgBtn.IsVisible = false;
            return;
        }

        var sortedFotos = GetSortedFotos();
        int currentIndex = FindCurrentIndex(sortedFotos);

        PrevImgBtn.IsVisible = currentIndex > 0;
        NextImgBtn.IsVisible = currentIndex >= 0 && currentIndex < sortedFotos.Count - 1;
    }

    private int FindCurrentIndex(List<(string PlanId, string PinId, string FotoKey, DateTime DateTime)> sortedFotos)
    {
        bool SameFile(string key) =>
            string.Equals(key, ImgSource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(key), Path.GetFileName(ImgSource), StringComparison.OrdinalIgnoreCase);

        int index = sortedFotos.FindIndex(f => f.PlanId == PlanId && f.PinId == PinId && SameFile(f.FotoKey));
        if (index == -1)
            index = sortedFotos.FindIndex(f => SameFile(f.FotoKey));

        return index;
    }

    private static List<(string PlanId, string PinId, string FotoKey, DateTime DateTime)> GetSortedFotos()
    {
        var result = new List<(string PlanId, string PinId, string FotoKey, DateTime DateTime)>();
        if (GlobalJson.Data?.Plans == null)
            return result;

        // Nur lebende Objekte; ToList() als Momentaufnahme gegen Hintergrund-Merges
        foreach (var (planId, plan) in SyncOps.LivePlans(GlobalJson.Data).ToList())
            foreach (var (pinId, pin) in SyncOps.LivePins(plan).ToList())
                foreach (var (fotoKey, foto) in SyncOps.LiveFotos(pin).ToList())
                    result.Add((planId, pinId, fotoKey, foto.DateTime));

        return [.. result.OrderBy(f => f.DateTime)];
    }

    private static (double W, double H) ClampSize(double w, double h)
    {
        var f = Math.Min(1.0, MaxDisplayEdge / Math.Max(w, h));
        return (Math.Round(w * f), Math.Round(h * f));
    }

    private void RequestDrawingRedraw()
    {
        if (drawingRedrawPending)
            return;

        drawingRedrawPending = true;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), () =>
        {
            drawingRedrawPending = false;
            DrawingView?.InvalidateSurface();
        });
    }
}
