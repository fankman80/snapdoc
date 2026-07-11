#nullable disable
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using SnapDoc.Controls;
using SnapDoc.DrawingTool;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using System.ComponentModel;

#if WINDOWS
using SnapDoc.Platforms.Windows;
#endif

#if IOS
using UIKit;
using CoreAnimation;
using CommunityToolkit.Maui.Core.Extensions;
#endif

namespace SnapDoc.Views;

public partial class NewPageExperimental : IQueryAttributable, INotifyPropertyChanged
{
    private readonly string planId;
    private string pinZoom = null;
    private readonly Plan thisPlan;
    private bool isPinSet = false;
    private MapPin tappedPin = null;
    private bool isFirstLoad = true;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;
    private readonly System.Collections.ObjectModel.ObservableCollection<MapPin> pinList = [];

    // DrawingController
    private readonly DrawingController drawingController;
    private SKCanvasView drawingView;
    private DrawMode drawMode = DrawMode.None;
    private int lineWidth = 3;
    private string strokeStyle = "";
    private float cloudRadius = 20;
    private float cloudInciseDeg = 15;
    private bool _isShowingPopup = false;
    private string planImageSource = "";
    private Color selectedBorderColor = new(0, 153, 0, 255);
    private Color selectedFillColor = new(202, 255, 150, 128);
    private Color selectedTextColor = new(0, 0, 0, 255);
    private bool isToolButtonsVisible = false;

    public string PlanImageSource { get => planImageSource; set { planImageSource = value; OnPropertyChanged(); }}
    public Color SelectedBorderColor { get => selectedBorderColor; set { selectedBorderColor = value; OnPropertyChanged(); }}
    public Color SelectedFillColor { get => selectedFillColor; set { selectedFillColor = value; OnPropertyChanged(); }}
    public Color SelectedTextColor { get => selectedTextColor; set { selectedTextColor = value; OnPropertyChanged(); }}
    public bool IsToolButtonsVisible { get => isToolButtonsVisible; set { isToolButtonsVisible = value; OnPropertyChanged(); }}

    public NewPageExperimental(string _planId)
    {
        InitializeComponent();
        planId = _planId;
        drawingController = new DrawingController(new TransformViewModel());
        thisPlan = GlobalJson.Data.Plans[planId];

        WeakReferenceMessenger.Default.Register<PinPropertyChangedMessage>(this, (r, m) =>
        {
            var (pinId, isLockPosition) = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var pin = pinList.FirstOrDefault(p => p.Id == pinId);
                if (pin != null)
                    pin?.IsLockPosition = isLockPosition;
            });
        });

        WeakReferenceMessenger.Default.Register<PinDeletedMessage>(this, (r, m) =>
        {
            var pinId = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var pin = pinList.FirstOrDefault(p => p.Id == pinId);
                if (pin != null)
                {
                    pinList.Remove(pin);
                    PlanImage.InvalidateSurface();
                }
            });
        });

        WeakReferenceMessenger.Default.Register<PinChangedMessage>(this, (r, m) =>
        {
            var pinId = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var pin = pinList.FirstOrDefault(p => p.Id == pinId);
                if (pin != null && thisPlan.Pins.TryGetValue(pinId, out var pinData))
                {
                    string pinIcon = pinData.PinIcon;
                    string resolvedPath = null;
                    var currentAnchor = pinData.Anchor;

                    if (pinData.IsCustomPin)
                    {
                        resolvedPath = Path.Combine(
                            Settings.DataDirectory,
                            GlobalJson.Data.ProjectPath,
                            GlobalJson.Data.CustomPinsPath,
                            pinIcon);
                    }
                    else if (pinData.IsCustomIcon)
                    {
                        var customIconPath = Path.Combine(Settings.DataDirectory, "customicons", pinIcon);
                        if (File.Exists(customIconPath))
                            resolvedPath = customIconPath;
                        else
                        {
                            string defaultPin = SettingsService.Instance.DefaultPinIcon;
                            var iconItem = Helper.IconLookup.Get(defaultPin);
                            if (iconItem != null)
                            {
                                resolvedPath = iconItem.FileName;
                                currentAnchor = iconItem.AnchorPoint;
                            }
                        }
                    }
                    else
                        resolvedPath = pinIcon;

                    pin.Icon?.Dispose();
                    pin.Icon = null;
                    pin.IconPath = resolvedPath;
                    pin.Anchor = currentAnchor;
                    pin.IsLockAutoScale = pinData.IsLockAutoScale;
                    pin.IsLockRotate = pinData.IsLockRotate;
                    pin.Rotation = pinData.IsLockRotate
                        ? (float)pinData.PinRotation
                        : (float)PlanImage.CurrentRotation * -1 + (float)pinData.PinRotation;
                    pin.PinScale = (float)pinData.PinScale;

                    var index = pinList.IndexOf(pin);
                    if (index != -1)
                        pinList[index] = pin;

                    PlanImage.InvalidateSurface();
                }
            });
        });
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        PlanImage.PinTapped += OnPinTapped;
        PlanImage.PinDoubleTapped += OnPinDoubleTapped;
        PlanImage.CanvasLongPressed += OnCanvasLongPressed;
        PlanImage.PinMoved += OnPinMoved;

        if (isFirstLoad)
        {
            await AddPlan();
            isFirstLoad = false;
            ImageFit(null, null);
        }
        else
            if (pinZoom != null)
                ZoomToPin(pinZoom);

        var appShell = Shell.Current as AppShell;
        appShell?.HighlightCurrentPlan(planId);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        PlanImage.PinTapped -= OnPinTapped;
        PlanImage.PinDoubleTapped -= OnPinDoubleTapped;
        PlanImage.CanvasLongPressed -= OnCanvasLongPressed;
        PlanImage.PinMoved -= OnPinMoved;

        if (!_isShowingPopup)
            Cleanup();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("pinZoom", out object value1))
            pinZoom = value1 as string;

        if (query.TryGetValue("pinMove", out object value2))
        {
            var pinId = value2 as string;
            pinZoom = value2 as string;

            if (!isFirstLoad)
            {
                AddPin(pinId);
                PlanImage.InvalidateSurface();
            }
        }
        query.Clear();
    }

    private Task AddPlan()
    {
        PlanImageSource = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);

        pinList.Clear();

        foreach (var pinId in thisPlan.Pins.Keys)
            AddPin(pinId);

        PlanImage.Pins = pinList;

        return Task.CompletedTask;
    }

    private MapPin AddPin(string pinId)
    {
        var pin = CreateMapPin(pinId);

        if (pin != null)
            pinList.Add(pin);

        return pin;
    }

    private MapPin CreateMapPin(string pinId)
    {
        if (!thisPlan.Pins.TryGetValue(pinId, out var pinData))
            return null;

        string pinIcon = pinData.PinIcon;
        string resolvedPath = null;
        var currentAnchor = pinData.Anchor;

        if (pinData.IsCustomPin)
        {
            resolvedPath = Path.Combine(
                Settings.DataDirectory,
                GlobalJson.Data.ProjectPath,
                GlobalJson.Data.CustomPinsPath,
                pinIcon);
        }
        else if (pinData.IsCustomIcon)
        {
            var customIconPath = Path.Combine(Settings.DataDirectory, "customicons", pinIcon);
            if (File.Exists(customIconPath))
            {
                resolvedPath = customIconPath;
            }
            else
            {
                string defaultPin = SettingsService.Instance.DefaultPinIcon;
                var iconItem = Helper.IconLookup.Get(defaultPin);
                if (iconItem != null)
                {
                    resolvedPath = iconItem.FileName;
                    currentAnchor = iconItem.AnchorPoint;
                }
            }
        }
        else
            resolvedPath = pinIcon;

        return new MapPin
        {
            Id = pinData.SelfId,
            RelativeX = (float)pinData.Pos.X,
            RelativeY = (float)pinData.Pos.Y,
            IconPath = resolvedPath,
            IsLockRotate = pinData.IsLockRotate,
            IsLockPosition = pinData.IsLockPosition,
            IsCustomPin = pinData.IsCustomPin,
            IsLockAutoScale = pinData.IsLockAutoScale,
            Rotation = pinData.IsLockRotate
                ? (float)pinData.PinRotation
                : (float)PlanImage.CurrentRotation * -1 + (float)pinData.PinRotation,
            PinScale = (float)pinData.PinScale,
            Anchor = currentAnchor
        };
    }

    private async void OnPinTapped(object sender, MapPin pin)
    {
        if (pin == null) return;
        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var plan) || !plan.Pins.ContainsKey(pin.Id)) return;
        if (isPinSet) return;

        tappedPin = pin;

        await Shell.Current.GoToAsync($"setpin?planId={planId}&pinId={pin.Id}");
    }

    private void OnPinDoubleTapped(object sender, MapPin pin)
    {
        if (pin == null) return;
        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var plan) || !plan.Pins.ContainsKey(pin.Id)) return;
        if (isPinSet) return;

        tappedPin = pin;

        PinSizeSlider.LowerValue = pin.PinScale * 100;
        PercentLabel.Text = $"{PinSizeSlider.LowerValue:0}%";

        PinRotateSlider.LowerValue = Helper.ToSliderValue(pin.Rotation);
        DegreesLabel.Text = $"{Helper.ToSliderValue(pin.Rotation):0}°";

        DrawBtn.IsVisible = false;
        SettingsService.Instance.IsPinPlaceBtnManualHide = true;
        PinEditBorder.IsVisible = true;

        LoadCustomPinBtn.IsVisible = pin.IsCustomPin;

        if (pin.IsLockRotate)
        {
            RotateModeLabel.Text = AppResources.drehung_fixiert;
            RotateModeBtn.Text = Settings.PinEditRotateModeLockIcon;
        }
        else
        {
            RotateModeLabel.Text = AppResources.automatische_drehung;
            RotateModeBtn.Text = Settings.PinEditRotateModeUnlockIcon;
        }

        if (pin.IsLockAutoScale)
        {
            SizeModeLabel.Text = AppResources.groesse_fixiert;
            SizeModeBtn.Text = Settings.PinEditSizeModeLockIcon;
        }
        else
        {
            SizeModeLabel.Text = AppResources.automatische_groessenanpassung;
            SizeModeBtn.Text = Settings.PinEditSizeModeUnlockIcon;
        }
    }

    private void OnCanvasLongPressed(object sender, SKPoint e)
    {
        if (SettingsService.Instance.PinPlaceMode == 2)
        {
            Point relativePoint = PlanImage.ConvertScreenToRelativePoint(e);
            SetPin(relativePoint);
        }
    }

    private void OnPinMoved(object sender, MapPin movedPin)
    {
        if (movedPin == null) return;
        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var plan) || !plan.Pins.TryGetValue(movedPin.Id, out var pin)) return;
        if (pin.IsLockPosition) return;

        pin.Pos = new Point(movedPin.RelativeX, movedPin.RelativeY);

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void SetPinClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.PinPlaceMode == 0)
        {
            Point centerFactor = PlanImage.GetPlanFactorAtControlCenter();
            SetPin(new Point(centerFactor.X, centerFactor.Y));
        }


        if (SettingsService.Instance.PinPlaceMode == 1)
        {
            DrawBtn.IsVisible = false;
            SetPinFrame.IsVisible = true;
            isPinSet = true;
        }
    }

    private void OnPinSetCancelClicked(object sender, EventArgs e)
    {
        DrawBtn.IsVisible = true;
        SetPinFrame.IsVisible = false;
        isPinSet = false;
    }

    private void SetPin(Point _pos,
                        string customName = null,
                        int customPinSizeWidth = 0,
                        int customPinSizeHeight = 0,
                        SKColor? pinColor = null,
                        double customScale = 1,
                        double _rotation = 0,
                        string customDisplayName = "",
                        bool overwrite = false)
    {

        string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string _newPin = SettingsService.Instance.DefaultPinIcon;
        var iconItem = Helper.IconLookup.Get(_newPin);

        pinColor ??= SKColors.Red;
        Point _anchorPoint = iconItem.AnchorPoint;
        Size _size = iconItem.IconSize;
        bool _isRotationLocked = iconItem.IsRotationLocked;
        bool _isAutoScaleLocked = iconItem.IsAutoScaleLocked;
        bool _isPosLocked = false;
        bool _isCustomPin = false;
        bool _isCustomIcon = iconItem.IsCustomIcon;
        bool _isAllowExport = true;
        string _displayName = iconItem.DisplayName;
        double _scale = iconItem.IconScale;

        if (customName != null)
        {
            _anchorPoint = new Point(0.5, 0.5);
            _size = new Size(customPinSizeWidth, customPinSizeHeight);
            _isRotationLocked = true;
            _isAutoScaleLocked = true;
            _isPosLocked = true;
            _isCustomPin = true;
            _isCustomIcon = false;
            _newPin = customName;
            _displayName = customDisplayName;
            _isAllowExport = true;
            _scale = customScale;
        }

        Pin newPinData = new()
        {
            Pos = _pos,
            Anchor = _anchorPoint,
            Size = _size,
            IsLockPosition = _isPosLocked,
            IsLockRotate = _isRotationLocked,
            IsLockAutoScale = _isAutoScaleLocked,
            IsCustomPin = _isCustomPin,
            IsCustomIcon = _isCustomIcon,
            IsWebMapPin = false,
            PinName = _displayName,
            PinDesc = "",
            PinPriority = 0,
            PinLocation = "",
            PinIcon = _newPin,
            Fotos = [],
            OnPlanId = planId,
            SelfId = currentDateTime,
            DateTime = DateTime.Now,
            PinColor = (SKColor)pinColor,
            PinScale = _scale,
            PinRotation = _rotation,
            GeoLocation = null, // noch nicht bekannt
            IsAllowExport = _isAllowExport,
        };

        if (!overwrite)
        {
            if (GlobalJson.Data.Plans.TryGetValue(planId, out Plan plan))
            {
                plan.Pins ??= [];
                plan.Pins[currentDateTime] = newPinData;

                thisPlan.PinCount += 1;

                // save data to file
                GlobalJson.SaveToFile();

                AddPin(currentDateTime);

                _ = UpdatePinLocationAsync(newPinData);
            }
        }
        else
        {
            var pinData = thisPlan.Pins[tappedPin.Id];
            pinData.PinIcon = _newPin;
            pinData.Size = _size;
            pinData.Pos = _pos;
            pinData.PinRotation = _rotation;
            pinData.PinName = _displayName;

            // save data to file
            GlobalJson.SaveToFile();

            var pinPath = Path.Combine(
                Settings.DataDirectory,
                GlobalJson.Data.ProjectPath,
                GlobalJson.Data.CustomPinsPath,
                _newPin);

            var customPin = new MapPin
            {
                Id = pinData.SelfId,
                RelativeX = (float)pinData.Pos.X,
                RelativeY = (float)pinData.Pos.Y,
                IconPath = pinPath,
                IsLockRotate = pinData.IsLockRotate,
                IsLockPosition = pinData.IsLockPosition,
                IsCustomPin = pinData.IsCustomPin,
                IsLockAutoScale = pinData.IsLockAutoScale,
                Rotation = (float)_rotation,
                PinScale = (float)_scale,
                Anchor = pinData.Anchor
            };

            pinList.Add(customPin);

            tappedPin = null;
        }
        PlanImage.InvalidateSurface();
    }

    private async Task UpdatePinLocationAsync(Pin pin)
    {
        if (!SettingsService.Instance.IsGpsActive) return;

        var location = await geoViewModel.TryGetLocationAsync();

        if (location == null) return;

        pin.GeoLocation = new GeoLocData(location);

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void ZoomToPin(string pinId, double? factor = null)
    {
        double zoom = factor ?? SettingsService.Instance.DefaultPinZoom;

        PlanImage.ZoomToPin(pinId, zoom);

        pinZoom = null;
    }

    private void ImageFit(object sender, EventArgs e) => PlanImage.ImageFit();

    private async Task StartDrawing(bool setDefaultMode = true)
    {
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
                var absoluteLayout = this.FindByName<AbsoluteLayout>("PlanView");

                if (drawingView != null)
                {
                    drawingController.Detach();
                    if (absoluteLayout.Children.Contains(drawingView))
                        absoluteLayout.Children.Remove(drawingView);
                    drawingView = null;
                }

                if (setDefaultMode)
                    drawingController.Reset();

                drawingView = drawingController.CreateCanvasView();
                drawingView.Opacity = 0;
                absoluteLayout.Children.Add(drawingView);

                AbsoluteLayout.SetLayoutBounds(drawingView, new Rect(0, 0, 1, 1));
                AbsoluteLayout.SetLayoutFlags(drawingView, AbsoluteLayoutFlags.All);

                IsToolButtonsVisible = true;

                drawingController.InitializeDrawing(
                    SelectedBorderColor.ToSKColor(),
                    SelectedFillColor.ToSKColor(),
                    SelectedTextColor.ToSKColor(),
                    lineWidth,
                    strokeStyle,
                    false,
                    (float)PlanImage.CurrentRotation,
                    setDefaultMode
                );

                drawingController.InitialRotation = (float)PlanImage.CurrentRotation;

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

    private async void DrawingClicked(object sender, EventArgs e) => await StartDrawing();

    private async void ShapeButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupShapeSelect();
        var temporaryOptions = new PopupOptions
        {
            CanBeDismissedByTappingOutsideOfPopup = true,
            Shape = Settings.PopupOptions.Shape
        };

        _isShowingPopup = true;
        var result = await this.ShowPopupAsync<object>(popup, temporaryOptions);
        _isShowingPopup = false;

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

    private void EraseClicked(object sender, EventArgs e) => drawingController.Reset();

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (drawingView == null || drawingController.IsEmpty())
        {
            Cleanup();
            return;
        }

        var plan = thisPlan;
        var customPinPath = Path.Combine(
            Settings.DataDirectory,
            GlobalJson.Data.ProjectPath,
            GlobalJson.Data.CustomPinsPath);

        Directory.CreateDirectory(customPinPath);

        string newBaseName = $"custompin_{DateTime.Now.Ticks}";
        string pngFileName = newBaseName + ".png";
        string dataFileName = newBaseName + ".data";
        string pngPath = Path.Combine(customPinPath, pngFileName);
        string dataPath = Path.Combine(customPinPath, dataFileName);
        bool isOverwrite = false;
        string oldPngPath = null;
        string oldDataPath = null;

        if (tappedPin != null && plan.Pins.TryGetValue(tappedPin.Id, out var oldPin))
        {
            isOverwrite = true;
            var oldFileName = Path.GetFileName(oldPin.PinIcon);
            oldPngPath = Path.Combine(customPinPath, oldFileName);
            oldDataPath = Path.Combine(customPinPath, Path.ChangeExtension(oldFileName, ".data"));
        }

        SKRect imageRect = await SaveCanvasAsCroppedPng(pngPath);
        drawingController.SaveToFile(dataPath);

        float centerX = (float)(drawingView.Width * Settings.DisplayDensity) / 2f;
        float centerY = (float)(drawingView.Height * Settings.DisplayDensity) / 2f;
        float panDx = centerX - PlanImage.CurrentPan.X;
        float panDy = centerY - PlanImage.CurrentPan.Y;
        float negRad = (float)(-PlanImage.CurrentRotation * Math.PI / 180.0);
        float cosNeg = (float)Math.Cos(negRad);
        float sinNeg = (float)Math.Sin(negRad);
        float centerPixelX = (panDx * cosNeg - panDy * sinNeg) / PlanImage.CurrentScale;
        float centerPixelY = (panDx * sinNeg + panDy * cosNeg) / PlanImage.CurrentScale;
        double centerFactorX = centerPixelX / PlanImage.OriginalImageSize.Width;
        double centerFactorY = centerPixelY / PlanImage.OriginalImageSize.Height;
        float fx = (float)imageRect.MidX - centerX;
        float fy = (float)imageRect.MidY - centerY;
        double ox = (fx / PlanImage.CurrentScale) / PlanImage.OriginalImageSize.Width;
        double oy = (fy / PlanImage.CurrentScale) / PlanImage.OriginalImageSize.Height;
        Point relativePos = new(centerFactorX + ox, centerFactorY + oy);

        SetPin(
            relativePos,
            pngFileName,
            (int)imageRect.Width,
            (int)imageRect.Height,
            new SKColor(SelectedBorderColor.ToUint()),
            1 / PlanImage.CurrentScale,
            0,
            drawingController.CombinedDrawable.RectDrawable.Text,
            isOverwrite
        );

        if (isOverwrite)
        {
            try
            {
                if (!string.IsNullOrEmpty(oldPngPath) && File.Exists(oldPngPath)) File.Delete(oldPngPath);
                if (!string.IsNullOrEmpty(oldDataPath) && File.Exists(oldDataPath)) File.Delete(oldDataPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Löschfehler: {ex.Message}");
            }
        }
        
        Cleanup();
    }

    private void Cleanup()
    {
        drawingController.Detach();
        RemoveDrawingView();
        drawMode = DrawMode.None;
        SetDrawMode(drawMode);
        IsToolButtonsVisible = false;
        DrawBtn.IsVisible = true;
        SettingsService.Instance.IsPinPlaceBtnManualHide = false;
        tappedPin = null;
        drawingView?.InvalidateSurface();
    }

    private void RemoveDrawingView()
    {
        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PlanView");
        if (drawingView != null && absoluteLayout != null)
        {
            absoluteLayout.Children.Remove(drawingView);
            drawingView = null;
        }
    }

    public async Task<SKRectI> SaveCanvasAsCroppedPng(string filePath)
    {
        var boundingBox = drawingController.CalculateBoundingBox((float)-drawingController.InitialRotation);
        if (boundingBox == null)
            return new SKRectI(0, 0, 0, 0);

        float padding = (float)(lineWidth * Settings.DisplayDensity) / 2;

        SKRect cropRect = new(
            boundingBox.Value.Left - padding,
            boundingBox.Value.Top - padding,
            boundingBox.Value.Right + padding,
            boundingBox.Value.Bottom + padding
        );

        var info = new SKImageInfo((int)Math.Ceiling(cropRect.Width), (int)Math.Ceiling(cropRect.Height));
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-cropRect.Left, -cropRect.Top);

        drawingController.RenderFinal(canvas, (float)-drawingController.InitialRotation);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using (var stream = File.Create(filePath))
        {
            data.SaveTo(stream);
        }

        return new SKRectI((int)cropRect.Left, (int)cropRect.Top, (int)cropRect.Right, (int)cropRect.Bottom);
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        bool isCloud = (AddCloudyBtn.Text == MaterialIcons.Cloud);
        var popup = new PopupStyleEditor(lineWidth, SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), strokeStyle, cloudRadius, cloudInciseDeg, isCloud);
        
        _isShowingPopup = true;        
        var result = await this.ShowPopupAsync<PopupStyleReturn>(popup, Settings.PopupOptions);
        _isShowingPopup = false;

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
        
        _isShowingPopup = true;        
        var result = await this.ShowPopupAsync<TextEditReturn>(popup, Settings.PopupOptions);
        _isShowingPopup = false;

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

    private void OnFullScreenButtonClicked(object sender, EventArgs e)
    {
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SettingsService.Instance.IsPinPlaceBtnManualHide = false;
        tappedPin = null;
    }

    private async void LoadCustomPinClicked(object sender, EventArgs e)
    {
        if (!thisPlan.Pins[tappedPin.Id].IsCustomPin) return;

        var file = Path.GetFileNameWithoutExtension(thisPlan.Pins[tappedPin.Id].PinIcon) + ".data";
        var filePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, file);

        if (File.Exists(filePath))
        {
            ZoomToPin(tappedPin.Id, 1 / thisPlan.Pins[tappedPin.Id].PinScale);
            pinList.Remove(tappedPin);
            PinEditBorder.IsVisible = false;
            SettingsService.Instance.IsPinPlaceBtnManualHide = false;

            await StartDrawing(false);

            drawingController.LoadFromFile(filePath, new SKPoint(
                (float)(this.Width / 2 * Settings.DisplayDensity),
                (float)(this.Height / 2 * Settings.DisplayDensity)));

            drawingController.ViewRotation = thisPlan.Pins[tappedPin.Id].PinRotation - drawingController.InitialRotation;

            var style = drawingController.LoadedStyle;
            if (style != null)
            {
                SelectedBorderColor = SKColor.Parse(style.LineColor).ToMauiColor();
                SelectedFillColor = SKColor.Parse(style.FillColor).ToMauiColor();

                var textColor = SKColors.Black;
                bool isCloud = false;

                if (drawingController.CombinedDrawable != null)
                {
                    if (drawingController.DrawMode == DrawMode.Rect)
                    {
                        textColor = drawingController.CombinedDrawable.RectDrawable.TextColor;
                        isCloud = drawingController.CombinedDrawable.RectDrawable.IsCloud;
                    }
                    else if (drawingController.DrawMode == DrawMode.Oval)
                    {
                        textColor = drawingController.CombinedDrawable.OvalDrawable.TextColor;
                        isCloud = drawingController.CombinedDrawable.OvalDrawable.IsCloud;
                    }
                    else if (drawingController.DrawMode == DrawMode.Poly)
                    {
                        isCloud = drawingController.CombinedDrawable.PolyDrawable.IsCloud;
                    }
                }
                AddCloudyBtn.Text = isCloud ? MaterialIcons.Cloud : MaterialIcons.Cloud_off;

                SelectedTextColor = textColor.ToMauiColor();

                lineWidth = (int)style.LineThickness;
                strokeStyle = style.StrokeStyle;
            }

            if (drawingController.DrawMode != DrawMode.None)
                SetDrawMode(drawingController.DrawMode);
        }
    }

    private void OnSizeModeClicked(object sender, EventArgs e)
    {
        if (tappedPin == null) return;
        var pinData = thisPlan.Pins[tappedPin.Id];

        if (pinData.IsLockAutoScale)
        {
            pinData.IsLockAutoScale = false;
            SizeModeLabel.Text = AppResources.automatische_groessenanpassung;
            SizeModeBtn.Text = Settings.PinEditSizeModeUnlockIcon;
        }
        else
        {
            pinData.IsLockAutoScale = true;
            SizeModeLabel.Text = AppResources.groesse_fixiert;
            SizeModeBtn.Text = Settings.PinEditSizeModeLockIcon;
        }

        var mapPin = pinList.FirstOrDefault(p => p.Id == tappedPin.Id);
        mapPin?.IsLockAutoScale = pinData.IsLockAutoScale;

        // save data to file
        GlobalJson.SaveToFile();

        PlanImage.InvalidateSurface();
    }

    private void OnRotateModeClicked(object sender, EventArgs e)
    {
        if (thisPlan.Pins[tappedPin.Id].IsLockRotate)
        {
            thisPlan.Pins[tappedPin.Id].IsLockRotate = false;
            thisPlan.Pins[tappedPin.Id].PinRotation = 0;
            RotateModeLabel.Text = AppResources.automatische_drehung;
            RotateModeBtn.Text = Settings.PinEditRotateModeUnlockIcon;
            PinRotateSlider.LowerValue = 0;

            tappedPin.Rotation = PlanImage.CurrentRotation * -1;
        }
        else
        {
            thisPlan.Pins[tappedPin.Id].IsLockRotate = true;
            thisPlan.Pins[tappedPin.Id].PinRotation = Helper.NormalizeAngle360(-PlanImage.CurrentRotation);
            tappedPin.IsLockRotate = true;
            tappedPin.Rotation = (float)Helper.NormalizeAngle360(-PlanImage.CurrentRotation);
            RotateModeLabel.Text = AppResources.drehung_fixiert;
            RotateModeBtn.Text = Settings.PinEditRotateModeLockIcon;
            PinRotateSlider.LowerValue = Helper.ToSliderValue(-PlanImage.CurrentRotation);
        }

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnRotateSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = Math.Round(((RangeSlider)sender).LowerValue, 0);

        DegreesLabel.Text = $"{sliderValue}°";
        tappedPin.Rotation = (float)Helper.SliderToRotation(sliderValue);

        if (!tappedPin.IsLockRotate)
            tappedPin.IsLockRotate = true;

        PlanImage.InvalidateSurface();
    }

    private void OnRotateSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = Helper.SliderToRotation(Math.Round(((RangeSlider)sender).LowerValue, 0));

        if (sliderValue != 0)
            thisPlan.Pins[tappedPin.Id].IsLockRotate = true;

        if (thisPlan.Pins[tappedPin.Id].IsLockRotate)
            thisPlan.Pins[tappedPin.Id].PinRotation = sliderValue;
        else
            thisPlan.Pins[tappedPin.Id].PinRotation = 0;

        // save data to file
        GlobalJson.SaveToFile();

        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SettingsService.Instance.IsPinPlaceBtnManualHide = false;
        tappedPin = null;
    }

    private void OnResizeSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = Math.Round(((RangeSlider)sender).LowerValue, 0);
        double scaleValue = sliderValue / 100.0;

        if (tappedPin != null && thisPlan.Pins.TryGetValue(tappedPin.Id, out var pinData))
        {
            pinData.PinScale = scaleValue;

            var mapPin = pinList.FirstOrDefault(p => p.Id == tappedPin.Id);
            mapPin?.PinScale = (float)scaleValue;

            PlanImage.InvalidateSurface();
        }

        PercentLabel.Text = $"{sliderValue}%";
    }

    private void OnResizeSliderDragCompleted(object sender, EventArgs e)
    {
        // save data to file
        GlobalJson.SaveToFile();

        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SettingsService.Instance.IsPinPlaceBtnManualHide = false;
        tappedPin = null;
    }

    private async void OnRotateSnapCklicked(object sender, EventArgs e)
    {
        var snapValue = 0;
        if ((PinRotateSlider.LowerValue * 4 / 360) % 1 == 0)
        {
            if (PinRotateSlider.LowerValue == 0)
                snapValue = 90;
            else if (PinRotateSlider.LowerValue == 90)
                snapValue = 180;
            else if (PinRotateSlider.LowerValue == 180)
                snapValue = -90;
            else if (PinRotateSlider.LowerValue == -90)
                snapValue = 0;
        }
        else
        {
            snapValue = (int)Math.Round(PinRotateSlider.LowerValue * 4 / 360, 0) * 90;
        }
        
        PinRotateSlider.LowerValue = snapValue;
        DegreesLabel.Text = $"{snapValue}°";
        tappedPin.Rotation = (float)Helper.SliderToRotation(snapValue);
        thisPlan.Pins[tappedPin.Id].PinRotation = snapValue;

        // save data to file
        GlobalJson.SaveToFile();

        PlanImage.InvalidateSurface();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var popup = new PopupPlanEdit(name: thisPlan.Name,
                                      desc: thisPlan.Description,
                                      gray: thisPlan.IsGrayscale,
                                      export: thisPlan.AllowExport,
                                      planColor: thisPlan.PlanColor);
        var result = await this.ShowPopupAsync<PlanEditReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            switch (result.Result.NameEntry)
            {
                case "Delete":
                    OnDeleteClick();
                    break;

                case "Grayscale":
                    OnGrayscaleClick();
                    break;

                default:
                    (Shell.Current as AppShell).AllPlanItems.FirstOrDefault(i => i.PlanId == planId).Title = result.Result.NameEntry;
                    Title = result.Result.NameEntry;

                    thisPlan.Name = result.Result.NameEntry;
                    thisPlan.Description = result.Result.DescEntry;
                    thisPlan.AllowExport = result.Result.AllowExport;
                    thisPlan.PlanColor = result.Result.PlanColor;

                    // Rotate Plan
                    if (result.Result.PlanRotate != 0)
                        PlanRotate(result.Result.PlanRotate);

                    // save data to file
                    GlobalJson.SaveToFile();

                    break;
            }
        }
    }

    private async void OnDeleteClick()
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diesen_plan_wirklich_loeschen, okText: AppResources.loeschen, alert: true);

        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result == null) return;
        if (Shell.Current is not AppShell shell) return;

        await Shell.Current.GoToAsync("//homescreen");

        // Shell-Navigation entfernen
        var shellContent = shell
            .FindByName<ShellContent>(planId);

        if (shellContent?.Parent is ShellSection section)
            section.Items.Remove(shellContent);

        // Masterliste bereinigen
        var masterItem = shell.AllPlanItems
            .FirstOrDefault(p => p.PlanId == planId);

        if (masterItem != null)
            shell.AllPlanItems.Remove(masterItem);

        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var plan)) return;

        // JSON + Files löschen
        plan = thisPlan;

        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "gs_" + plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", plan.File));

        GlobalJson.Data.Plans.Remove(planId);

        // save data to file
        GlobalJson.SaveToFile();

        // Anzeige neu aufbauen
        shell.ApplyFilterAndSorting();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private void OnGrayscaleClick()
    {
        if (thisPlan.IsGrayscale)
        {
            string colorImageFile = thisPlan.File.Replace("gs_", "");
            thisPlan.File = colorImageFile;
            thisPlan.IsGrayscale = false;
        }
        else
        {
            string grayImageFile = "gs_" + thisPlan.File;
            string grayImagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, grayImageFile);
            if (!File.Exists(grayImagePath))
            {
                using var originalStream = File.OpenRead(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File));
                using var originalBitmap = SKBitmap.Decode(originalStream);
                var grayBitmap = Helper.ConvertToGrayscale(originalBitmap);
                using SKImage image = SKImage.FromBitmap(grayBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                using var fileStream = File.OpenWrite(grayImagePath);
                data.SaveTo(fileStream);
            }
            thisPlan.File = grayImageFile;
            thisPlan.IsGrayscale = true;
        }

        // save data to file
        GlobalJson.SaveToFile();

        isFirstLoad = true;

        PlanImageSource = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);
    }

    private async void PlanRotate(int angle)
    {
        var imagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);
        var thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", thisPlan.File);
        var imagefile = Path.GetFileNameWithoutExtension(imagePath);

        if (imagefile.EndsWith("_r"))
            imagefile = imagefile.Replace("_r", "");
        else
            imagefile += "_r";
        imagefile += Path.GetExtension(imagePath);

        // Ziel-Pfade definieren
        var outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, imagefile);
        var thumbOutputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", imagefile);

        Helper.RotateImageFile(imagePath, outputPath, angle);
        Helper.RotateImageFile(thumbPath, thumbOutputPath, angle);

        // Grösse für die UI-Aktualisierung auslesen
        using (var stream = File.OpenRead(outputPath))
        using (var bmp = SKBitmap.Decode(stream))
        {
            if (bmp != null)
                thisPlan.ImageSize = new Size(bmp.Width, bmp.Height);
        }

        thisPlan.File = imagefile;

        await AddPlan();

        // Thumbnail-Pfad in der Shell-CollectionView aktualisieren
        if (Shell.Current is AppShell shell)
        {
            var newThumbPath = Path.Combine(
                Settings.DataDirectory,
                GlobalJson.Data.ProjectPath,
                GlobalJson.Data.PlanPath,
                "thumbnails",
                imagefile);

            if (!string.IsNullOrEmpty(planId))
            {
                var masterItem = shell.AllPlanItems.FirstOrDefault(p => p.PlanId == planId);
                masterItem?.Thumbnail = newThumbPath;
            }

            shell.ApplyFilterAndSorting();
        }

        // Umpositionierung der Pins
        if (thisPlan.Pins != null)
        {
            foreach (var pinId in thisPlan.Pins.Keys)
            {
                var pin = pinList.FirstOrDefault(p => p.Id == pinId);
                if (pin != null)
                    pinList.Remove(pin);

                thisPlan.Pins[pinId].Pos = RotatePin(thisPlan.Pins[pinId].Pos, angle);

                if (thisPlan.Pins[pinId].IsLockRotate)
                    thisPlan.Pins[pinId].PinRotation = (thisPlan.Pins[pinId].PinRotation + angle) % 360;

                AddPin(pinId);
            }
        }

        // Daten speichern
        GlobalJson.SaveToFile();
    }

    static Point RotatePin(Point oldPos, int angle)
    {
        angle = ((angle % 360) + 360) % 360; // Normalisiere auf 0..359

        return angle switch
        {
            0 => new Point(oldPos.X, oldPos.Y),
            90 => new Point(1 - oldPos.Y, oldPos.X),
            180 => new Point(1 - oldPos.X, 1 - oldPos.Y),
            270 => new Point(oldPos.Y, 1 - oldPos.X),
            _ => throw new NotSupportedException($"Nur 0/90/180/270 Grad werden unterstützt (nicht: {angle})."),
        };
    }

    private void OnTitleChanged(object sender, EventArgs e)
    {
        if (sender is not Entry entry) return;

        // Titel speichern
        (Shell.Current as AppShell)
            ?.AllPlanItems.FirstOrDefault(i => i.PlanId == planId)!.Title = Title;

        thisPlan.Name = Title;

        // save data to file
        GlobalJson.SaveToFile();

        // Fokus entfernen
        entry.Unfocus();

#if ANDROID
        try
        {
            if (entry.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var inputMethodManager = nativeView.Context?.GetSystemService(
                    Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;

                // Tastatur schließen
                inputMethodManager?.HideSoftInputFromWindow(nativeView.WindowToken, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Android keyboard hide failed: {ex.Message}");
        }
#endif

#if IOS
        try
        {
            UIKit.UIApplication.SharedApplication.SendAction(
                new ObjCRuntime.Selector("resignFirstResponder"), null, null, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iOS keyboard hide failed: {ex.Message}");
        }
#endif
    }
}
