#nullable disable
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Input;
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
    private bool canDraw = true;
    private double panStartX;
    private double panStartY;
    private bool isPanningActive = false;
    private double totalPanX = 0;
    private readonly TransformViewModel fotoContainer;
    private readonly BackButtonBehavior backBehavior;
    private const double MaxDisplayEdge = 2048;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private DrawMode drawMode = DrawMode.None;
    private bool isHatchEffect = false;
    private float hatchStrokeWitdh = 2f;
    private float hatchStrokeSpace = 8f;
    private float hatchRotation = 45f;
    private int lineWidth = 3;
    private string strokeStyle = "";
    private bool drawingOwnsTouch;
    private bool syncingCloud;

    private Color selectedBorderColor = new(0, 153, 0, 255);
    public Color SelectedBorderColor
    {
        get => selectedBorderColor;
        set { selectedBorderColor = value; OnPropertyChanged(); }
    }

    private Color selectedFillColor = new(202, 255, 150, 128);
    public Color SelectedFillColor
    {
        get => selectedFillColor;
        set { selectedFillColor = value; OnPropertyChanged(); }
    }

    private Color selectedTextColor = new(0, 0, 0, 255);
    public Color SelectedTextColor
    {
        get => selectedTextColor;
        set { selectedTextColor = value; OnPropertyChanged(); }
    }

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

    private string activeTool = "";
    public string ActiveTool
    {
        get => activeTool;
        set { if (activeTool != value) { activeTool = value; OnPropertyChanged(); } }
    }

    private string activeSheet = "";
    public string ActiveSheet
    {
        get => activeSheet;
        set { if (activeSheet != value) { activeSheet = value; OnPropertyChanged(); } }
    }

    private bool isFillOptionVisible;
    public bool IsFillOptionVisible
    {
        get => isFillOptionVisible;
        set { if (isFillOptionVisible != value) { isFillOptionVisible = value; OnPropertyChanged(); } }
    }

    private bool isTextOptionVisible;
    public bool IsTextOptionVisible
    {
        get => isTextOptionVisible;
        set { if (isTextOptionVisible != value) { isTextOptionVisible = value; OnPropertyChanged(); } }
    }

    private bool isCloudOptionVisible;
    public bool IsCloudOptionVisible
    {
        get => isCloudOptionVisible;
        set { if (isCloudOptionVisible != value) { isCloudOptionVisible = value; OnPropertyChanged(); } }
    }

    private bool isCloudActive;
    public bool IsCloudActive
    {
        get => isCloudActive;
        set
        {
            if (isCloudActive == value) return;
            isCloudActive = value;
            OnPropertyChanged();
            if (!syncingCloud)
                ApplyCloudToDrawable(value);
        }
    }

    private float cloudRadius = 20;
    public float CloudRadius
    {
        get => cloudRadius;
        set { if (cloudRadius != value) { cloudRadius = value; OnPropertyChanged(); ApplyStyles(); } }
    }

    private float cloudInciseDeg = 15;
    public float CloudInciseDeg
    {
        get => cloudInciseDeg;
        set { if (cloudInciseDeg != value) { cloudInciseDeg = value; OnPropertyChanged(); ApplyStyles(); } }
    }

    public ImageViewPage()
    {
        InitializeComponent();
        BindingContext = this;

        backBehavior = new BackButtonBehavior { Command = new Command(async () => await OnBackRequestedAsync()) };
        Shell.SetBackButtonBehavior(this, backBehavior);

        fotoContainer = new TransformViewModel();
        ImageDrawingCanvas.BindingContext = fotoContainer;
        GestureContainer.BindingContext = fotoContainer;

        FotoContainer.SizeChanged += ImageViewContainer_SizeChanged;
        GestureContainer.SizeChanged += GestureContainer_SizeChanged;

        drawingController = new DrawingController(fotoContainer);
        drawingController.Invalidated += (_, _) => { DrawingView?.InvalidateSurface(); };

        // Chip-Markierung zurücksetzen, wenn ein Sheet per Wischen geschlossen wird
        foreach (var sheet in AllSheets)
            sheet.Closed += (_, _) => { if (!AllSheets.Any(s => s.IsOpen)) ActiveSheet = ""; };

        // Leisten: zentriert, solange alles Platz hat – sonst seitwärts scrollbar
        OptionBarScroll.SizeChanged += (_, _) => OptionBarHost.MinimumWidthRequest = OptionBarScroll.Width;
        ToolBarScroll.SizeChanged += (_, _) => ToolBarHost.MinimumWidthRequest = ToolBarScroll.Width;

        SetupDesktopScrollWheel();
    }

    private BottomSheet[] AllSheets => [PenSheet, FillSheet, CloudSheet, TextSheet];

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
        var open = AllSheets.FirstOrDefault(s => s.IsOpen);
        if (open != null) { _ = open.CloseAsync(); return true; }
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

    private void GestureContainer_SizeChanged(object sender, EventArgs e)
    {
        if (!hasFittedImage || GestureContainer.Width < 10 || GestureContainer.Height < 10) return;

        bool wasFitted = Math.Abs(fotoContainer.Scale - minScale) < 0.001;
        if (wasFitted)
            Dispatcher.Dispatch(() => ImageFit(null, null));
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

    private async void DrawingClicked(object sender, EventArgs e) => await StartDrawing();

    private Task StartDrawing(bool setDefaultMode = true)
    {
        fotoContainer.IsPanningEnabled = false;

        if (setDefaultMode)
            drawingController.Reset();

        SettingsService.Instance.IsPinPlaceBtnManualHide = true;
        DrawBtn.IsVisible = false;
        IsEditMode = true;
        UpdateNavigationButtons();

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

        if (setDefaultMode)
            SetDrawMode(DrawMode.Rect);

        UpdateDrawingViewScale();
        DrawingView.IsVisible = true;
        DrawingView.InvalidateSurface();

        return Task.CompletedTask;
    }

    [RelayCommand] private void SelectTool(string key)
    {
        if (Enum.TryParse(key, out DrawMode mode))
            SetDrawMode(mode);
    }

    private void SetDrawMode(DrawMode mode)
    {
        if (mode == DrawMode.None) return;

        drawMode = mode;
        drawingController.DrawMode = mode;
        ActiveTool = mode.ToString();

        IsFillOptionVisible = mode is DrawMode.Rect or DrawMode.Oval or DrawMode.Poly or DrawMode.Arrow;
        IsTextOptionVisible = mode is DrawMode.Rect or DrawMode.Oval;
        IsCloudOptionVisible = mode is DrawMode.Poly or DrawMode.Rect or DrawMode.Oval;

        var combined = drawingController.CombinedDrawable;

        // Zustand der Form übernehmen, ohne ihn zurückzuschreiben
        syncingCloud = true;
        IsCloudActive = mode switch
        {
            DrawMode.Rect => combined?.RectDrawable?.IsCloud ?? false,
            DrawMode.Oval => combined?.OvalDrawable?.IsCloud ?? false,
            DrawMode.Poly => combined?.PolyDrawable?.IsCloud ?? false,
            _ => false
        };
        syncingCloud = false;

        if (combined != null)
        {
            combined.PolyDrawable.DisplayHandles = mode == DrawMode.Poly;
            combined.RectDrawable.DisplayHandles = mode == DrawMode.Rect;
            combined.OvalDrawable.DisplayHandles = mode == DrawMode.Oval;
            combined.ArrowDrawable.DisplayHandles = mode == DrawMode.Arrow;
        }

        DrawingView?.InvalidateSurface();

        // Sheets schliessen, die zum neuen Werkzeug nicht passen
        if (!IsFillOptionVisible && FillSheet.IsOpen) _ = FillSheet.CloseAsync();
        if (!IsTextOptionVisible && TextSheet.IsOpen) _ = TextSheet.CloseAsync();
        if (!IsCloudOptionVisible && CloudSheet.IsOpen) _ = CloudSheet.CloseAsync();
    }

    private async void EraseClicked(object sender, EventArgs e)
    {
        drawingController.Reset();
        SetDrawMode(drawMode == DrawMode.None ? DrawMode.Rect : drawMode);

        if (!GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[ImgSource].HasOverlay)
            return;

        string origPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath,
                                       GlobalJson.Data.ImagePath, "originals", ImgSource);

        if (!File.Exists(origPath))
            await SaveManager.DownloadMediaOnDemandAsync(ImgSource, $"{GlobalJson.Data.ImagePath}/originals");

        if (!File.Exists(origPath))
        {
            await this.DisplaySnackbar("Original nicht verfügbar – bitte online gehen.", actionButtonText: AppResources.ok);
            return;
        }

        isCleared = true;
        var bytes = await File.ReadAllBytesAsync(origPath);
        FotoContainer.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    private async Task ToggleSheetAsync(BottomSheet sheet, string key, Action load)
    {
        if (sheet.IsOpen)
        {
            await sheet.CloseAsync();
            return;
        }

        await CloseSheetsAsync(except: sheet);
        load?.Invoke();
        ActiveSheet = key;
        await sheet.OpenAsync();
    }

    private async Task CloseSheetsAsync(BottomSheet except = null)
    {
        foreach (var s in AllSheets)
            if (s != except && s.IsOpen)
                await s.CloseAsync();
    }

    private async void PenClicked(object sender, EventArgs e) => await ToggleSheetAsync(PenSheet, "Pen", () =>
            PenEditor.Load(SelectedBorderColor, lineWidth, strokeStyle));

    private async void FillClicked(object sender, EventArgs e) => await ToggleSheetAsync(FillSheet, "Fill", () =>
            FillEditor.Load(SelectedFillColor, isHatchEffect, hatchStrokeWitdh, hatchStrokeSpace, hatchRotation));

    private async void CloudClicked(object sender, EventArgs e) => await ToggleSheetAsync(CloudSheet, "Cloud", null);

    private async void TextClicked(object sender, EventArgs e)
    {
        var combined = drawingController?.CombinedDrawable;
        if (combined == null) return;
        if (drawMode is not (DrawMode.Rect or DrawMode.Oval)) return;

        await ToggleSheetAsync(TextSheet, "Text", () =>
        {
            if (drawMode == DrawMode.Rect)
            {
                var d = combined.RectDrawable;
                TextEditor.Load(d.TextSize, d.TextAlignment, d.TextStyle, d.AutoSizeText, d.Text, d.TextPadding);
            }
            else
            {
                var d = combined.OvalDrawable;
                TextEditor.Load(d.TextSize, d.TextAlignment, d.TextStyle, d.AutoSizeText, d.Text, d.TextPadding);
            }
        });

        if (TextSheet.IsOpen)
            TextEditor.FocusInput();
    }

    // Live: Stift-Sheet
    private void OnPenChanged(object sender, EventArgs e)
    {
        var p = PenEditor;
        SelectedBorderColor = p.LineColor;
        lineWidth = p.LineWidth;
        strokeStyle = p.StrokeStyle;
        ApplyStyles();
    }

    // Live: Füllungs-Sheet
    private void OnFillChanged(object sender, EventArgs e)
    {
        var f = FillEditor;
        SelectedFillColor = f.FillColor;
        isHatchEffect = f.IsHatchEffect;
        hatchStrokeWitdh = f.HatchStrokeWitdh;
        hatchStrokeSpace = f.HatchStrokeSpace;
        hatchRotation = f.HatchRotation;
        ApplyStyles();
    }

    // Live: Textfarbe im Text-Sheet (SelectedTextColor ist bereits per Binding gesetzt)
    private void OnTextColorChanged(object sender, EventArgs e) => ApplyStyles();

    // Live: Textinhalt / Formatierung im Text-Sheet
    private void OnTextEditChanged(object sender, EventArgs e)
    {
        var combined = drawingController?.CombinedDrawable;
        if (combined == null) return;

        var t = TextEditor;
        if (drawMode == DrawMode.Rect)
        {
            var d = combined.RectDrawable;
            d.Text = t.InputTxt;
            d.TextSize = t.FontSize;
            d.TextAlignment = t.FontAlignment;
            d.TextStyle = t.TextStyle;
            d.AutoSizeText = t.AutoSize;
            d.TextPadding = t.TextPadding;
        }
        else if (drawMode == DrawMode.Oval)
        {
            var d = combined.OvalDrawable;
            d.Text = t.InputTxt;
            d.TextSize = t.FontSize;
            d.TextAlignment = t.FontAlignment;
            d.TextStyle = t.TextStyle;
            d.AutoSizeText = t.AutoSize;
            d.TextPadding = t.TextPadding;
        }

        DrawingView?.InvalidateSurface();
    }

    private void ApplyStyles()
    {
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
            cloudInciseDeg);

        DrawingView?.InvalidateSurface();
    }

    private void ApplyCloudToDrawable(bool isCloud)
    {
        var combined = drawingController?.CombinedDrawable;
        if (combined == null) return;

        switch (drawingController.DrawMode)
        {
            case DrawMode.Rect: combined.RectDrawable.IsCloud = isCloud; break;
            case DrawMode.Oval: combined.OvalDrawable.IsCloud = isCloud; break;
            case DrawMode.Poly: combined.PolyDrawable.IsCloud = isCloud; break;
        }

        DrawingView?.InvalidateSurface();
    }

    private async void CancelClicked(object sender, EventArgs e) => await CancelDrawingAsync();

    private async Task CancelDrawingAsync()
    {
        drawingController.Reset();
        await CloseSheetsAsync();

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
        drawMode = DrawMode.None;
        ActiveTool = "";
        ActiveSheet = "";
        isCleared = false;
        IsEditMode = false;
        DrawBtn.IsVisible = canDraw;
        UpdateNavigationButtons();
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        await CloseSheetsAsync();

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
                        await this.DisplaySnackbar("Original nicht verfügbar – bitte online gehen.", actionButtonText: AppResources.ok);
                        return; // Bearbeitungsmodus bleibt offen
                    }
                    File.Copy(imgPath, origPath);
                }

                bool saved = await SaveFotoWithOverlay(origPath, imgPath);
                if (!saved)
                {
                    await this.DisplaySnackbar("Das Bild mit der Zeichnung konnte nicht gespeichert werden.", actionButtonText: AppResources.ok);
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

            float drawingWidth = DrawingView.CanvasSize.Width;
            float drawingHeight = DrawingView.CanvasSize.Height;
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
                drawingOwnsTouch = drawingController.OnPressed(e.Location);
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
                }
                else
                    drawingController.OnTap(e.Location);
                e.Handled = true;
                break;

            case SKTouchAction.Cancelled:
                drawingController.OnCancel();
                drawingOwnsTouch = false;
                break;
        }
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
}