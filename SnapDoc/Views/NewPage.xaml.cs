#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;
using MR.Gestures;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
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

namespace SnapDoc.Views;

public partial class NewPage : IQueryAttributable, INotifyPropertyChanged
{
    public string PageTitle { get; set; } = "";
    public string PinUpdate;
    public string PlanId;
    public string PinDelete;
    public string PinZoom = null;
    private readonly Plan thisPlan;
    private bool isPinSet = false;
    private MR.Gestures.Image activePin = null; 
    private MR.Gestures.Image doubleTappedPin = null;
    private double densityX, densityY;
    private double oversizeScaleFac = 1;
    private bool isFirstLoad = true;
    private Point mousePos;
    private bool isTappedHandled = false;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;
    private readonly TransformViewModel planContainer;

    // --- DrawingController ---
    private readonly DrawingController drawingController;
    private SKCanvasView drawingView;
    private DrawMode drawMode = DrawMode.None;
    private int lineWidth = 3;
    private string strokeStyle = "";

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

    private readonly Dictionary<string, MR.Gestures.Image> _pinLookup = [];

#if WINDOWS
    private bool shiftKeyDown = false;
    private double shiftKeyRotationStart;
#endif

    public NewPage(string planId)
    {
        InitializeComponent();
        planContainer = new TransformViewModel();
        BindingContext = planContainer;

        drawingController = new DrawingController(planContainer);
        PlanId = planId;
        thisPlan = GlobalJson.Data.Plans[PlanId];
        PageTitle = thisPlan.Name;


        // Überwache Sichtbarkeit des SetPin-Buttons
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        SettingsService.Instance.PropertyChanged += SettingsService_PropertyChanged;

        WeakReferenceMessenger.Default.Register<PinDeletedMessage>(this, (r, m) =>
        {
            var pinId = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_pinLookup.TryGetValue(pinId, out var image))
                {
                    PlanContainer.Remove(image);
                    _pinLookup.Remove(pinId);
                }
            });
        });

        WeakReferenceMessenger.Default.Register<PinChangedMessage>(this, (r, m) =>
        {
            var pinId = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_pinLookup.TryGetValue(pinId, out var image))
                {
                    var pinData = thisPlan.Pins[pinId];
                    var pinIcon = pinData.PinIcon;

                    if (pinData.IsCustomIcon)
                        pinIcon = Path.Combine(Settings.DataDirectory, "customicons", pinIcon);

                    image.Source = pinIcon;
                    image.AnchorX = pinData.Anchor.X;
                    image.AnchorY = pinData.Anchor.Y;
                    image.Rotation = pinData.IsLockRotate
                        ? pinData.PinRotation
                        : PlanContainer.Rotation * -1 + pinData.PinRotation;
                    image.Scale = PinScaling(pinId);

                    AdjustImagePosition(image);
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

        PlanImage.PropertyChanged += PlanImage_PropertyChanged;
        PlanContainer.PropertyChanged += PlanContainer_PropertyChanged;

        if (isFirstLoad)
            await AddPlan();
        else
        {
            if (PinZoom != null)
            {
                ZoomToPin(PinZoom);
                PinZoom = null;
            }
        }

        // Setze den Titel der Seite und markiere den Plan im ShellManü
        var appShell = Application.Current.Windows[0].Page as AppShell;
        appShell?.HighlightCurrentPlan(this.PlanId);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SettingsService.Instance.PropertyChanged -= SettingsService_PropertyChanged;
        PlanImage.PropertyChanged -= PlanImage_PropertyChanged;
        PlanContainer.PropertyChanged -= PlanContainer_PropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("pinZoom", out object value1))
        {
            PinZoom = value1 as string;
        }

        if (query.TryGetValue("pinMove", out object value2))
        {
            var pinId = value2 as string;
            PinZoom = value2 as string;

            // add pin-icon on plan
            if (!_pinLookup.ContainsKey(pinId) && !isFirstLoad)
                AddPin(pinId, thisPlan.Pins[pinId].PinIcon);
        }
    }

    private void SettingsService_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.PinPlaceMode))
            SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
    }

    private void PlanContainer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Scale" || e.PropertyName == "Rotation")
        {
            double scale = 1.0 / PlanContainer.Scale;
            double scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100.0;
            foreach (MR.Gestures.Image img in PlanContainer.Children.OfType<MR.Gestures.Image>())
            {
                if (img.AutomationId != null)
                {
                    if (!thisPlan.Pins[img.AutomationId].IsLockAutoScale)
                        if (scale < scaleLimit & scale > (double)SettingsService.Instance.PinMinScaleLimit / 100.0)
                            img.Scale = scale * thisPlan.Pins[img.AutomationId].PinScale;

                    if (!thisPlan.Pins[img.AutomationId].IsLockRotate)
                        img.Rotation = PlanContainer.Rotation * -1;
                }
            }
        }
    }

    private Task AddPlan()
    {
        //calculate aspect-ratio, resolution and imagesize
        if (thisPlan.ImageSize.Width > SettingsService.Instance.MaxPdfImageSizeW || thisPlan.ImageSize.Height > SettingsService.Instance.MaxPdfImageSizeH)
        {
            PlanImage.DownsampleToViewSize = true;
            PlanImage.DownsampleWidth = SettingsService.Instance.MaxPdfImageSizeW;
            PlanImage.DownsampleHeight = SettingsService.Instance.MaxPdfImageSizeH;

            oversizeScaleFac = Math.Min(thisPlan.ImageSize.Width, thisPlan.ImageSize.Height) /
                               Math.Max(thisPlan.ImageSize.Width, thisPlan.ImageSize.Height);

            if (thisPlan.ImageSize.Width > thisPlan.ImageSize.Height)
            {
                PlanImage.WidthRequest = SettingsService.Instance.MaxPdfImageSizeW;
                PlanImage.HeightRequest = SettingsService.Instance.MaxPdfImageSizeH * oversizeScaleFac;
            }
            else
            {
                PlanImage.WidthRequest = SettingsService.Instance.MaxPdfImageSizeW * oversizeScaleFac;
                PlanImage.HeightRequest = SettingsService.Instance.MaxPdfImageSizeH;
            }
        }
        else
        {
            PlanImage.WidthRequest = thisPlan.ImageSize.Width;
            PlanImage.HeightRequest = thisPlan.ImageSize.Height;
        }

        PlanImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);
        
        return Task.CompletedTask;
    }

    private void AddPins()
    {
        // Load all Pins at first page opening
        if (thisPlan.Pins == null) return;

        foreach (var pinId in thisPlan.Pins.Keys)
        {
            var pinIcon = thisPlan.Pins[pinId].PinIcon;
            AddPin(pinId, pinIcon);
        }
    }

    private void AddPin(string pinId, string pinIcon)
    {
        Point _originAnchor = thisPlan.Pins[pinId].Anchor;
        Point _originPos = thisPlan.Pins[pinId].Pos;
        Size _planSize = thisPlan.ImageSize;
        Size _pinSize = thisPlan.Pins[pinId].Size;
        Double _rotation = PlanContainer.Rotation * -1 + thisPlan.Pins[pinId].PinRotation;

        if (thisPlan.Pins[pinId].IsCustomPin)
        {
            _rotation = thisPlan.Pins[pinId].PinRotation;
            pinIcon = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, pinIcon);
        }
        else if (thisPlan.Pins[pinId].IsCustomIcon)
        {
            var _pinIcon = Path.Combine(Settings.DataDirectory, "customicons", pinIcon);
            if (File.Exists(_pinIcon))
                pinIcon = _pinIcon;
            else
            {
                // Lade Default-Icon falls Custom-Icon nicht existiert
                string _newPin = SettingsService.Instance.DefaultPinIcon;
                var iconItem = Helper.IconLookup.Get(_newPin);
                pinIcon = iconItem.FileName;
                _originAnchor = iconItem.AnchorPoint;
                _pinSize = iconItem.IconSize;
            }
        }

        // berechne Anchor-Koordinaten
        var smallImage = new MR.Gestures.Image
        {
            Source = pinIcon,
            AutomationId = pinId,
            WidthRequest = thisPlan.Pins[pinId].Size.Width,
            HeightRequest = thisPlan.Pins[pinId].Size.Height,
            AnchorX = thisPlan.Pins[pinId].Anchor.X,
            AnchorY = thisPlan.Pins[pinId].Anchor.Y,
            TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * _pinSize.Width),
            TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * _pinSize.Height),
            Rotation = _rotation,
            Scale = PinScaling(pinId),
            InputTransparent = false,
            BindingContext = new PinContext
            {
                PlanId = PlanId,
                PinId = pinId
            }
        };

        smallImage.Down += OnPinDown;
        smallImage.Up += OnPinUp;
        smallImage.Tapped += OnPinTapped;
        smallImage.DoubleTapped += OnPinDoubleTapped;

        // sort large custom pins on lower z-indexes and small pins on higher z-indexes
        smallImage.ZIndex = 10000 - (int)((thisPlan.Pins[pinId].Size.Width +
                                           thisPlan.Pins[pinId].Size.Height) / 2);

        PlanContainer.Children.Add(smallImage);
        _pinLookup[pinId] = smallImage;

        PlanContainer.InvalidateMeasure(); //Aktualisierung forcieren
    }

    private void OnPinDown(object sender, EventArgs e)
    {
        var img = (MR.Gestures.Image)sender;
        var ctx = (PinContext)img.BindingContext;

        if (GlobalJson.Data.Plans[ctx.PlanId].Pins[ctx.PinId].IsLockPosition)
            return;

        planContainer.IsPanningEnabled = false;
        activePin = img;
    }
    private void OnPinUp(object sender, EventArgs e)
    {
        var img = (MR.Gestures.Image)sender;
        var ctx = (PinContext)img.BindingContext;

        if (GlobalJson.Data.Plans[ctx.PlanId].Pins[ctx.PinId].IsLockPosition)
            return;

        planContainer.IsPanningEnabled = true;
        activePin = null;

        var plan = GlobalJson.Data.Plans[ctx.PlanId];

        var x = img.TranslationX / plan.ImageSize.Width * densityX;
        var y = img.TranslationY / plan.ImageSize.Height * densityY;

        var dx = img.AnchorX * img.Width / plan.ImageSize.Width * densityX;
        var dy = img.AnchorY * img.Height / plan.ImageSize.Height * densityY;

        plan.Pins[ctx.PinId].Pos = new Point(x + dx, y + dy);
        GlobalJson.SaveToFile();
    }

    private async void OnPinTapped(object sender, EventArgs e)
    {
        if (isTappedHandled || isPinSet)
            return;

        isTappedHandled = true;

        var img = (MR.Gestures.Image)sender;
        var ctx = (PinContext)img.BindingContext;

        await Shell.Current.GoToAsync($"setpin?planId={ctx.PlanId}&pinId={ctx.PinId}");

        isTappedHandled = false;
    }

    private void OnPinDoubleTapped(object sender, EventArgs e)
    {
        var img = (MR.Gestures.Image)sender;
        var ctx = (PinContext)img.BindingContext;

        doubleTappedPin = img;

        var pin = GlobalJson.Data.Plans[ctx.PlanId].Pins[ctx.PinId];

        PinSizeSlider.Value = pin.PinScale * 100;
        PinRotateSlider.Value = Helper.ToSliderValue(pin.PinRotation);

        planContainer.IsPanningEnabled = false;
        DrawBtn.IsVisible = false;
        SetPinBtn.IsVisible = false;
        PinEditBorder.IsVisible = true;

        loadCustomPinBtn.IsVisible = pin.IsCustomPin;

        if (pin.IsLockRotate)
        {
            rotateModeLabel.Text = AppResources.drehung_fixiert;
            rotateModeBtn.Text = Settings.PinEditRotateModeLockIcon;
        }
        else
        {
            rotateModeLabel.Text = AppResources.automatische_drehung;
            rotateModeBtn.Text = Settings.PinEditRotateModeUnlockIcon;
        }

        if (pin.IsLockAutoScale)
        {
            sizeModeLabel.Text = AppResources.groesse_fixiert;
            sizeModeBtn.Text = Settings.PinEditSizeModeLockIcon;
        }
        else
        {
            sizeModeLabel.Text = AppResources.automatische_groessenanpassung;
            sizeModeBtn.Text = Settings.PinEditSizeModeUnlockIcon;
        }
    }

    private void AdjustImagePosition(MR.Gestures.Image image)
    {
        Point _originAnchor = thisPlan.Pins[image.AutomationId].Anchor;
        Point _originPos = thisPlan.Pins[image.AutomationId].Pos;
        Size _planSize = thisPlan.ImageSize;
        Size _pinSize = thisPlan.Pins[image.AutomationId].Size;

        image.AnchorX = _originAnchor.X;
        image.AnchorY = _originAnchor.Y;
        image.WidthRequest = _pinSize.Width;
        image.HeightRequest = _pinSize.Height;
        image.TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * image.Width);
        image.TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * image.Height);
    }

    private void PlanImage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!isFirstLoad)
            return;

        // Warte darauf, dass Width und Height gültige Werte haben
        if (e.PropertyName == nameof(PlanImage.Width) || e.PropertyName == nameof(PlanImage.Height))
        {
            if (PlanImage.Width > 0 && PlanImage.Height > 0)
            {
                // Größe des Bildes ist korrekt gesetzt
                densityX = thisPlan.ImageSize.Width / PlanImage.Width;
                densityY = thisPlan.ImageSize.Height / PlanImage.Height;

                // Entferne das Event-Handler, damit es nicht mehr ausgelöst wird
                PlanImage.PropertyChanged -= PlanImage_PropertyChanged;

                // Rufe AddPins auf, wenn die Berechnung abgeschlossen ist
                isFirstLoad = false;
                ImageFit(null, null);
                AddPins();

                if (PinZoom != null)
                {
                    ZoomToPin(PinZoom);
                    PinZoom = null;
                }
            }
        } 
    }

    private void OnPinching(object sender, PinchEventArgs e)
    {
        planContainer.IsPanningEnabled = false;
    }

    private void OnPinched(object sender, PinchEventArgs e)
    {
        planContainer.IsPanningEnabled = true;
    }

    private void SetPinClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.PinPlaceMode == 0)
            SetPin(new Point(PlanContainer.AnchorX, PlanContainer.AnchorY));

        if (SettingsService.Instance.PinPlaceMode == 1)
        {
            DrawBtn.IsVisible = false;
            SetPinFrame.IsVisible = true;
            isPinSet = true;
        }
    }

    private void OnTapped(object sender, MR.Gestures.TapEventArgs e)
    {
        if (isPinSet)
        {
            var x = 1.0 / thisPlan.ImageSize.Width * e.Center.X * densityX;
            var y = 1.0 / thisPlan.ImageSize.Height * e.Center.Y * densityY;

            SetPin(new Point(x, y));

            DrawBtn.IsVisible = true;
            SetPinFrame.IsVisible = false;
            isPinSet = false;
        }
    }

    private void OnLongPressing(object sender, LongPressEventArgs e)
    {
        if (SettingsService.Instance.PinPlaceMode == 2)
        {
            var x = 1.0 / thisPlan.ImageSize.Width * e.Center.X  * densityX;
            var y = 1.0 / thisPlan.ImageSize.Height * e.Center.Y * densityY;

            SetPin(new Point(x, y));
        }
    }

    private void OnPinSetCancelClicked(object sender, EventArgs e)
    {
        DrawBtn.IsVisible = true;
        SetPinFrame.IsVisible = false;
        isPinSet = false;
    }

    private void OnPanning(object sender, PanEventArgs e)
    {
        var scaleSpeed = 1.0 / PlanContainer.Scale;
        double angle = PlanContainer.Rotation * Math.PI / 180.0;
        double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
        double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);

        if (activePin != null && doubleTappedPin == null)
        {
            activePin.TranslationX += deltaX * scaleSpeed;
            activePin.TranslationY += deltaY * scaleSpeed;
        }
        else if (planContainer.IsPanningEnabled)
        {
            planContainer.TranslationX += deltaX * scaleSpeed;
            planContainer.TranslationY += deltaY * scaleSpeed;

            planContainer.AnchorX = 1.0 / PlanContainer.Width * ((this.Width / 2) - planContainer.TranslationX);
            planContainer.AnchorY = 1.0 / PlanContainer.Height * ((this.Height / 2) - planContainer.TranslationY);
        }
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
        var currentPage = (NewPage)Shell.Current.CurrentPage;
        if (currentPage == null) return;

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
            PinName = _displayName,
            PinDesc = "",
            PinPriority = 0,
            PinLocation = "",
            PinIcon = _newPin,
            Fotos = [],
            OnPlanId = PlanId,
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
            // Sicherstellen, dass der Plan existiert
            if (GlobalJson.Data.Plans.TryGetValue(PlanId, out Plan plan))
            {
                plan.Pins ??= [];
                plan.Pins[currentDateTime] = newPinData;

                thisPlan.PinCount += 1;

                GlobalJson.SaveToFile(); // initial speichern

                AddPin(currentDateTime, newPinData.PinIcon);

                _ = UpdatePinLocationAsync(newPinData);
            }
        }
        else
        {
            thisPlan.Pins[doubleTappedPin.AutomationId].PinIcon = _newPin;
            thisPlan.Pins[doubleTappedPin.AutomationId].Size = _size;
            thisPlan.Pins[doubleTappedPin.AutomationId].Pos = _pos;
            thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation = _rotation;
            thisPlan.Pins[doubleTappedPin.AutomationId].PinName = _displayName;
            GlobalJson.SaveToFile(); // initial speichern

            Point _originAnchor = thisPlan.Pins[doubleTappedPin.AutomationId].Anchor;
            Point _originPos = thisPlan.Pins[doubleTappedPin.AutomationId].Pos;
            Size _planSize = thisPlan.ImageSize;
            Size _pinSize = thisPlan.Pins[doubleTappedPin.AutomationId].Size;

            var pinIcon = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, _newPin);
            //doubleTappedPin.Source = pinIcon;
            doubleTappedPin.Source = ImageSource.FromStream(() =>
            {
                return File.OpenRead(pinIcon);
            });

            doubleTappedPin.WidthRequest = thisPlan.Pins[doubleTappedPin.AutomationId].Size.Width;
            doubleTappedPin.HeightRequest = thisPlan.Pins[doubleTappedPin.AutomationId].Size.Height;
            doubleTappedPin.TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * _pinSize.Width);
            doubleTappedPin.TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * _pinSize.Height);
            doubleTappedPin.Rotation = _rotation;
            doubleTappedPin.Scale = _scale;
            doubleTappedPin.IsVisible = true;
            doubleTappedPin = null;
        }
    }

    private void OnMouseMoved(object sender, MouseEventArgs e)
    {
        mousePos = e.Center;

#if WINDOWS
        if (KeyboardHelper.IsShiftPressed() && !SettingsService.Instance.IsPlanRotateLocked)
        {
            double centerX = this.Width / 2;
            double centerY = this.Height / 2;
            double deltaX = mousePos.X - centerX;
            double deltaY = mousePos.Y - centerY;
            double angleInRadians = Math.Atan2(deltaY, deltaX);
            double angleInDegrees = angleInRadians * (180 / Math.PI);

            if (shiftKeyDown == false)
            {
                shiftKeyDown = true;
                shiftKeyRotationStart = planContainer.Rotation - angleInDegrees;
            }
            planContainer.Rotation = angleInDegrees + shiftKeyRotationStart;
        }
        else
            shiftKeyDown = false;
#endif
    }

    private async Task UpdatePinLocationAsync(Pin pin)
    {
        var location = await geoViewModel.TryGetLocationAsync();
        if (location == null)
            return;

        pin.GeoLocation = new GeoLocData(location);
        GlobalJson.SaveToFile();
    }

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
#if WINDOWS
        if (mousePos.X < 0 || !planContainer.IsPanningEnabled)
            return; // Ignore scroll events when mouse is onto the flyout

        // Dynamischer Zoomfaktor basierend auf der aktuellen Skalierung
        double zoomFactor;
        if (planContainer.Scale > 2) // Sehr stark vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.05 : 0.95;  // Sehr langsame Zoom-Änderung
        else if (planContainer.Scale > 1) // Moderat vergrößert
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9;  // Langsame Zoom-Änderung
        else // Wenig vergrößert oder sehr klein
            zoomFactor = e.ScrollDelta.Y > 0 ? 1.15 : 0.85;  // Moderate Zoom-Änderung

        double targetScale = PlanContainer.Scale * zoomFactor; ;
        double newAnchorX = 1.0 / PlanContainer.Width * (mousePos.X - planContainer.TranslationX);
        double newAnchorY = 1.0 / PlanContainer.Height * (mousePos.Y - planContainer.TranslationY);
        double deltaTranslationX = (PlanContainer.Width * (newAnchorX - planContainer.AnchorX)) * (targetScale / planContainer.Scale - 1);
        double deltaTranslationY = (PlanContainer.Height * (newAnchorY - planContainer.AnchorY)) * (targetScale / planContainer.Scale - 1);

        planContainer.AnchorX = newAnchorX;
        planContainer.AnchorY = newAnchorY;
        planContainer.TranslationX -= deltaTranslationX;
        planContainer.TranslationY -= deltaTranslationY;
        planContainer.Scale = targetScale;
#endif
    }

    private void ZoomToPin(string pinId, double? factor = null)
    {
        double zoom = factor ?? SettingsService.Instance.DefaultPinZoom;

        if (!thisPlan.Pins.TryGetValue(pinId, out var pin))
            return;

        planContainer.AnchorX = pin.Pos.X;
        planContainer.AnchorY = pin.Pos.Y;
        planContainer.TranslationX = (this.Width / 2) - (PlanContainer.Width * pin.Pos.X);
        planContainer.TranslationY = (this.Height / 2) - (PlanContainer.Height * pin.Pos.Y);
        planContainer.Scale = zoom;
    }

    private void ImageFit(object sender, EventArgs e)
    {
        planContainer.Rotation = 0;
        planContainer.Scale = Math.Min(this.Width / PlanContainer.Width, this.Height / PlanContainer.Height);
        planContainer.TranslationX = (this.Width - PlanContainer.Width) / 2;
        planContainer.TranslationY = (this.Height - PlanContainer.Height) / 2;
        planContainer.AnchorX = 1.0 / PlanContainer.Width * ((this.Width / 2) - planContainer.TranslationX);
        planContainer.AnchorY = 1.0 / PlanContainer.Height * ((this.Height / 2) - planContainer.TranslationY);
    }

    private double PinScaling(string pinId)
    {
        if (thisPlan.Pins[pinId].IsCustomPin != true)
        {
            double scale = 1.0 / planContainer.Scale;
            double scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100.0;
            if (scale < scaleLimit & scale > (double)SettingsService.Instance.PinMinScaleLimit / 100.0)
                return 1 / planContainer.Scale * thisPlan.Pins[pinId].PinScale;
            else
                return scaleLimit * thisPlan.Pins[pinId].PinScale;
        }
        else
            return thisPlan.Pins[pinId].PinScale;
    }

    private void DrawingClicked(object sender, EventArgs e)
    {
        SetPinBtn.IsVisible = false;
        DrawBtn.IsVisible = false;
        ToolBtns.IsVisible = true;

        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PlanView");

        // 1) Canvas erzeugen und anhängen
        drawingView = drawingController.CreateCanvasView();
        absoluteLayout.Children.Add(drawingView);
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutBounds(drawingView, new Rect(0, 0, 1, 1));
        Microsoft.Maui.Controls.AbsoluteLayout.SetLayoutFlags(drawingView, AbsoluteLayoutFlags.All);

        // 2) DrawingController initialisieren
        drawingController.InitializeDrawing(
            SelectedBorderColor.ToSKColor(),
            lineWidth,
            strokeStyle,
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            (float)SettingsService.Instance.PolyLineHandleTouchRadius,
            (float)SettingsService.Instance.PolyLineHandleRadius,
            SKColor.Parse(SettingsService.Instance.PolyLineHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
            SKColor.Parse(SettingsService.Instance.PolyLineStartHandleColor).WithAlpha(SettingsService.Instance.PolyLineHandleAlpha),
            false,
            (float)planContainer.Rotation
        );

        drawingController.InitialRotation = (float)planContainer.Rotation;

        // 3) initialer Modus
        drawingController.DrawMode = DrawMode.None;
        drawMode = DrawMode.None;
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
                    AddTextBtn.IsVisible = false;
                    DrawFreeBtn.CornerRadius = 10;
                    break;

                case DrawMode.Poly:
                    AddTextBtn.IsVisible = false;
                    DrawPolyBtn.CornerRadius = 10;
                    break;

                case DrawMode.Rect:
                    AddTextBtn.IsVisible= true;
                    DrawRectBtn.CornerRadius = 10;
                    break;

                case DrawMode.None:
                    AddTextBtn.IsVisible = false;
                    DrawPolyBtn.CornerRadius = 30;
                    DrawFreeBtn.CornerRadius = 30;
                    DrawRectBtn.CornerRadius = 30;
                    break;
            }
        }

        // Handles
        var combined = drawingController.CombinedDrawable;
        if (combined != null)
        {
            combined.PolyDrawable?.DisplayHandles = activate && mode == DrawMode.Poly;
            combined.RectDrawable?.DisplayHandles = activate && mode == DrawMode.Rect;
        }

        drawingView?.InvalidateSurface();
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        SetDrawMode(DrawMode.None);
        drawingController.Reset();
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (drawingView == null || drawingController.IsEmpty())
            goto Cleanup;

        var plan = thisPlan;
        var customPinPath = Path.Combine(
            Settings.DataDirectory,
            GlobalJson.Data.ProjectPath,
            GlobalJson.Data.CustomPinsPath);

        Directory.CreateDirectory(customPinPath);

        bool overwrite =
            doubleTappedPin != null &&
            plan.Pins.TryGetValue(doubleTappedPin.AutomationId, out var pin) &&
            File.Exists(Path.Combine(
                customPinPath,
                Path.GetFileName(pin.PinIcon)));

        var fileType = ".png";
        var customPinName = overwrite
            ? Path.GetFileNameWithoutExtension(plan.Pins[doubleTappedPin.AutomationId].PinIcon)
            : $"custompin_{DateTime.Now:yyyyMMdd_HHmmss}";

        var pngPath = Path.Combine(customPinPath, customPinName + fileType);

        // PNG erzeugen
        SKRect imageRect = await SaveCanvasAsCroppedPng(pngPath);

        // Mittelpunkt (Canvas → Plan)
        var cx = imageRect.MidX / Settings.DisplayDensity;
        var cy = imageRect.MidY / Settings.DisplayDensity;

        var rotatedOffset = RotateOffset(
            SettingsService.Instance.CustomPinOffset,
            -planContainer.Rotation);

        double fx = cx + rotatedOffset.X;
        double fy = cy + rotatedOffset.Y;

        var ox = ((fx - drawingView.Width / 2) / planContainer.Scale) / plan.ImageSize.Width;
        var oy = ((fy - drawingView.Height / 2) / planContainer.Scale) / plan.ImageSize.Height;

        SetPin(
            new Point(PlanContainer.AnchorX + ox, PlanContainer.AnchorY + oy),
            customPinName + fileType,
            (int)imageRect.Width,
            (int)imageRect.Height,
            new SKColor(SelectedBorderColor.ToUint()),
            1 / planContainer.Scale / Settings.DisplayDensity,
            drawingController.InitialRotation - planContainer.Rotation,
            drawingController.CombinedDrawable.RectDrawable.Text,
            overwrite
        );

        drawingController.SaveToFile(Path.Combine(customPinPath, customPinName + ".data"));

    Cleanup:
        drawingController.Detach();
        RemoveDrawingView();
        drawMode = DrawMode.None;
        SetDrawMode(drawMode);
        ToolBtns.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;

        doubleTappedPin?.IsVisible = true;
        doubleTappedPin = null;

        drawingView?.InvalidateSurface();
    }

    static Point RotateOffset(Point offset, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        return new Point(
            offset.X * cos - offset.Y * sin,
            offset.X * sin + offset.Y * cos
        );
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
        int width = (int)drawingView.CanvasSize.Width;
        int height = (int)drawingView.CanvasSize.Height;
        var info = new SKImageInfo(width, height);

        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // --- Rotation ermitteln ---
        float rotation = (float)-planContainer.Rotation;

        // --- BoundingBox VOR dem Zeichnen berechnen ---
        var boundingBox = drawingController.CalculateBoundingBox((float)-drawingController.InitialRotation);
        if (boundingBox == null)
            return new SKRectI(0, 0, 0, 0);

        // --- Offset für Strichdicke hinzufügen ---
        var offset = (lineWidth * Settings.DisplayDensity) / 2;
        var cropRect = new SKRectI(
            (int)Math.Floor(boundingBox.Value.Left - offset),
            (int)Math.Floor(boundingBox.Value.Top - offset),
            (int)Math.Ceiling(boundingBox.Value.Right + offset),
            (int)Math.Ceiling(boundingBox.Value.Bottom + offset)
        );

        // --- Zeichne final auf Canvas ---
        canvas.Clear(SKColors.Transparent);
        drawingController.RenderFinal(canvas, (float)-drawingController.InitialRotation);
        canvas.Flush();

        // --- Ganze Zeichnung als Bitmap holen ---
        using var fullImage = surface.Snapshot();
        using var fullBitmap = SKBitmap.FromImage(fullImage);

        // --- Croppen ---
        var croppedBitmap = new SKBitmap(cropRect.Width, cropRect.Height);
        fullBitmap.ExtractSubset(croppedBitmap, cropRect);

        // --- PNG speichern ---
        using var image = SKImage.FromBitmap(croppedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);

        return cropRect;
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupStyleEditor(lineWidth, SelectedBorderColor.ToArgbHex(), SelectedFillColor.ToArgbHex(), SelectedTextColor.ToArgbHex(), strokeStyle);
        var result = await this.ShowPopupAsync<PopupStyleReturn>(popup, Settings.PopupOptions);

        if (result.Result == null) return;

        SelectedBorderColor = Color.FromArgb(result.Result.BorderColorHex);
        SelectedFillColor = Color.FromArgb(result.Result.FillColorHex);
        SelectedTextColor = Color.FromArgb(result.Result.TextColorHex);
        lineWidth = result.Result.PenWidth;
        strokeStyle = result.Result.StrokeStyle;

        drawingController?.UpdateDrawingStyles(
            SelectedBorderColor.ToSKColor(),
            SelectedFillColor.ToSKColor(),
            SelectedTextColor.ToSKColor(),
            lineWidth,
            strokeStyle
        );
    }

    private async void TextClicked(object sender, EventArgs e)
    {
        var combined = drawingController.CombinedDrawable;

        var popup = new PopupTextEdit(combined.RectDrawable.TextSize, combined.RectDrawable.TextAlignment, combined.RectDrawable.TextStyle, combined.RectDrawable.AutoSizeText, combined.RectDrawable.Text, combined.RectDrawable.TextPadding, okText: AppResources.ok);
        var result = await this.ShowPopupAsync<TextEditReturn>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
            combined.RectDrawable?.Text = result.Result.InputTxt;
            combined.RectDrawable?.TextSize = result.Result.FontSize;
            combined.RectDrawable?.TextAlignment = result.Result.Alignment;
            combined.RectDrawable?.TextStyle = result.Result.Style;
            combined.RectDrawable?.AutoSizeText = result.Result.AutoSize;
            combined.RectDrawable?.TextPadding = result.Result.TextPadding;
        }

        drawingView?.InvalidateSurface();
    }

    private void OnFullScreenButtonClicked(object sender, EventArgs e)
    {
        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        doubleTappedPin = null;
    }

    private void LoadCustomPinClicked(object sender, EventArgs e)
    {
        if (!thisPlan.Pins[doubleTappedPin.AutomationId].IsCustomPin)
            return;

        // Activate CustomPin Edit Mode
        var file = Path.GetFileNameWithoutExtension(thisPlan.Pins[doubleTappedPin.AutomationId].PinIcon) + ".data";
        var filePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, file);
        if (File.Exists(filePath))
        {
            doubleTappedPin.IsVisible = false;
            planContainer.IsPanningEnabled = true;
            PinEditBorder.IsVisible = false;
            SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
            DrawingClicked(null, null);
            ZoomToPin(doubleTappedPin.AutomationId, 1 / thisPlan.Pins[doubleTappedPin.AutomationId].PinScale / Settings.DisplayDensity);
            drawingController.LoadFromFile(filePath, new SKPoint((float)(this.Width / 2 * Settings.DisplayDensity), (float)(this.Height / 2 * Settings.DisplayDensity)));
            planContainer.Rotation = -thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation + drawingController.InitialRotation;  
            
            var style = drawingController.LoadedStyle;
            if (style != null)
            {
                SelectedBorderColor = SKColor.Parse(style.LineColor).ToMauiColor();
                SelectedFillColor = SKColor.Parse(style.FillColor).ToMauiColor();
                SelectedTextColor = SKColor.Parse(style.TextColor).ToMauiColor();
                lineWidth = (int)style.LineThickness;
            }
        }
    }

    private void OnSizeModeClicked(object sender, EventArgs e)
    {
        if (thisPlan.Pins[doubleTappedPin.AutomationId].IsLockAutoScale)
        {
            thisPlan.Pins[doubleTappedPin.AutomationId].IsLockAutoScale = false;
            sizeModeLabel.Text = AppResources.automatische_groessenanpassung;
            sizeModeBtn.Text = Settings.PinEditSizeModeUnlockIcon;
        }
        else
        {
            thisPlan.Pins[doubleTappedPin.AutomationId].IsLockAutoScale = true;
            sizeModeLabel.Text = AppResources.groesse_fixiert;
            sizeModeBtn.Text = Settings.PinEditSizeModeLockIcon;
        }

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnRotateModeClicked(object sender, EventArgs e)
    {
        if (thisPlan.Pins[doubleTappedPin.AutomationId].IsLockRotate)
        {
            thisPlan.Pins[doubleTappedPin.AutomationId].IsLockRotate = false;
            thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation = 0;
            rotateModeLabel.Text = AppResources.automatische_drehung;
            rotateModeBtn.Text = Settings.PinEditRotateModeUnlockIcon;
            PinRotateSlider.Value = 0;

            doubleTappedPin.Rotation = planContainer.Rotation * -1;
        }
        else
        {
            thisPlan.Pins[doubleTappedPin.AutomationId].IsLockRotate = true;
            thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation = Helper.NormalizeAngle360(-planContainer.Rotation);
            rotateModeLabel.Text = AppResources.drehung_fixiert;
            rotateModeBtn.Text = Settings.PinEditRotateModeLockIcon;
            PinRotateSlider.Value = Helper.ToSliderValue(-planContainer.Rotation);
        }

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnRotateSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = Math.Round(((Microsoft.Maui.Controls.Slider)sender).Value, 0);

        degreesLabel.Text = $"{sliderValue}°";
        doubleTappedPin.Rotation = Helper.SliderToRotation(sliderValue);
    }

    private void OnRotateSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = Helper.SliderToRotation(Math.Round(((Microsoft.Maui.Controls.Slider)sender).Value, 0));

        if (sliderValue != 0)
            thisPlan.Pins[doubleTappedPin.AutomationId].IsLockRotate = true;

        if (thisPlan.Pins[doubleTappedPin.AutomationId].IsLockRotate)
            thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation = sliderValue;
        else
            thisPlan.Pins[doubleTappedPin.AutomationId].PinRotation = 0;

        // save data to file
        GlobalJson.SaveToFile();

        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        doubleTappedPin = null;
    }

    private void OnResizeSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = Math.Round(((Microsoft.Maui.Controls.Slider)sender).Value, 0);

        double scale = 1.0 / PlanContainer.Scale;
        double scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100.0;
        if (scale < scaleLimit & scale > (double)SettingsService.Instance.PinMinScaleLimit / 100.0)
            doubleTappedPin.Scale = scale * sliderValue / 100.0;
        else
            doubleTappedPin.Scale = sliderValue / 100.0;

        percentLabel.Text = $"{sliderValue}%";
    }

    private void OnResizeSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = Math.Round(((Microsoft.Maui.Controls.Slider)sender).Value, 0);

        thisPlan.Pins[doubleTappedPin.AutomationId].PinScale = sliderValue / 100.0;

        // save data to file
        GlobalJson.SaveToFile();

        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        doubleTappedPin = null;
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
                    (Application.Current.Windows[0].Page as AppShell).AllPlanItems.FirstOrDefault(i => i.PlanId == PlanId).Title = result.Result.NameEntry;
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
        if (result.Result == null)
            return;

        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        // Shell-Navigation entfernen
        var shellContent = shell
            .FindByName<ShellContent>(PlanId);

        if (shellContent?.Parent is ShellSection section)
            section.Items.Remove(shellContent);

        // Masterliste bereinigen
        var masterItem = shell.AllPlanItems
            .FirstOrDefault(p => p.PlanId == PlanId);

        if (masterItem != null)
            shell.AllPlanItems.Remove(masterItem);

        if (!GlobalJson.Data.Plans.TryGetValue(PlanId, out var plan))
            return;

        // JSON + Files löschen
        plan = thisPlan;

        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "gs_" + plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", plan.File));

        GlobalJson.Data.Plans.Remove(PlanId);

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

        PlanImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);
    }

    private async void PlanRotate(int angle)
    {
        var imagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, thisPlan.File);

        // Dateiname ändern, damit das Bild als neue Source erkannt wird
        var imagefile = Path.GetFileNameWithoutExtension(imagePath);
        if (imagefile.EndsWith("_r"))
            imagefile = imagefile.Replace("_r", "");
        else
            imagefile += "_r";
        imagefile += Path.GetExtension(imagePath);
        var outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, imagefile);

        using var inputStream = File.OpenRead(imagePath);
        using var originalBitmap = SKBitmap.Decode(inputStream);

        int width = originalBitmap.Width;
        int height = originalBitmap.Height;

        // Zielgröße: nur bei 90/270 Breite/Höhe tauschen
        int rotatedWidth = angle % 180 == 0 ? width : height;
        int rotatedHeight = angle % 180 == 0 ? height : width;

        using var rotatedBitmap = new SKBitmap(rotatedWidth, rotatedHeight);

        using var canvas = new SKCanvas(rotatedBitmap);
        canvas.Clear(SKColors.White); // optional: Hintergrundfarbe setzen

        // Mittelpunkt berechnen
        float cx = width / 2f;
        float cy = height / 2f;

        // Zielmitte
        float dx = rotatedWidth / 2f;
        float dy = rotatedHeight / 2f;

        // Transformation: Zielmitte → Ursprung → Rotation → Originalbild
        canvas.Translate(dx, dy);
        canvas.RotateDegrees(angle);
        canvas.Translate(-cx, -cy);

        canvas.DrawBitmap(originalBitmap, 0, 0);

        using var image = SKImage.FromBitmap(rotatedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        using var outputStream = File.Open(outputPath, FileMode.Create);
        data.SaveTo(outputStream);
        outputStream.Close();

        thisPlan.ImageSize = new Size(rotatedBitmap.Width, rotatedBitmap.Height);
        thisPlan.File = imagefile;

        PlanContainer.SizeChanged += OnPlanContainerReady;
        await AddPlan();

        // Umpositionierung der Pins
        if (thisPlan.Pins != null)
        {
            foreach (var pinId in thisPlan.Pins.Keys)
            {
                if (_pinLookup.TryGetValue(pinId, out var delPin))
                {
                    PlanContainer.Remove(delPin);
                    _pinLookup.Remove(pinId);
                }

                thisPlan.Pins[pinId].Pos = RotatePin(thisPlan.Pins[pinId].Pos, angle);

                if (thisPlan.Pins[pinId].IsLockRotate)
                    thisPlan.Pins[pinId].PinRotation = (thisPlan.Pins[pinId].PinRotation + angle) % 360;

                var pinIcon = thisPlan.Pins[pinId].PinIcon;
                AddPin(pinId, pinIcon);
            }
        }

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnPlanContainerReady(object sender, EventArgs e)
    {
        PlanContainer.SizeChanged -= OnPlanContainerReady;
        ImageFit(null, null);
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
        if (sender is not Microsoft.Maui.Controls.Entry entry)
            return;

        // Titel speichern
        (Application.Current.Windows[0].Page as AppShell)
            ?.AllPlanItems.FirstOrDefault(i => i.PlanId == PlanId)!.Title = Title;

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
            UIKit.UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                if (entry.Handler?.PlatformView is UIKit.UITextField textField)
                {
                    textField.ResignFirstResponder(); // Tastatur schließen
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iOS keyboard hide failed: {ex.Message}");
        }
#endif
    }
}

public static class ColorExtensions
{
    public static SKColor ToSKColor(this Color color)
    {
        byte a = (byte)(color.Alpha * 255);
        byte r = (byte)(color.Red * 255);
        byte g = (byte)(color.Green * 255);
        byte b = (byte)(color.Blue * 255);

        return new SKColor(r, g, b, a);
    }
}

public sealed class PinContext
{
    public string PlanId { get; init; } = "";
    public string PinId { get; init; } = "";
}
