#nullable disable
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Messaging;
using MR.Gestures;
using SkiaSharp;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using System.ComponentModel;

#if WINDOWS
using SnapDoc.Platforms.Windows;
#endif

#if ANDROID
using Android.Views.InputMethods;
using Microsoft.Maui.Platform;
#endif

namespace SnapDoc.Views;

public partial class NewPage : IQueryAttributable, INotifyPropertyChanged
{
    public string PageTitle { get; set; } = "";
    public string PinUpdate;
    public string PlanId;
    public string PinDelete;
    public string PinZoom = null;
    private bool isPinSet = false;
    private MR.Gestures.Image activePin = null;
    private double densityX, densityY;
    private bool isFirstLoad = true;
    private Point mousePos;
    private readonly TransformViewModel planContainer;
    private DrawingView drawingView;
    private int lineWidth = 6;
    private Color selectedColor = new(255, 0, 0);
    bool isTappedHandled = false;
    SKRectI pinBound;
    private bool isPinChangedRegistered = false;
    private bool isPinDeletedRegistered = false;

#if WINDOWS
    private bool shiftKeyDown = false;
    private double shiftKeyRotationStart;
#endif

    public NewPage(string planId)
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
        PlanId = planId;
        planContainer = (TransformViewModel)PlanContainer.BindingContext;
        PageTitle = GlobalJson.Data.Plans[PlanId].Name;

        // Überwache Sichtbarkeit des SetPin-Buttons
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        SettingsService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.PinPlaceMode))
                SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        };
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        if (isFirstLoad)
        {
            await AddPlan();
            PlanImage.PropertyChanged += PlanImage_PropertyChanged;
        }
        else
        {
            if (PinZoom != null)
            {
                ZoomToPin(PinZoom);
                PinZoom = null;
            }
        }

        // aktualisiere PinIcons
        if (!isPinChangedRegistered)
        {
            WeakReferenceMessenger.Default.Register<PinChangedMessage>(this, (r, m) =>
            {
                var pinId = m.Value;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var image = PlanContainer.Children
                        .OfType<MR.Gestures.Image>()
                        .FirstOrDefault(i => i.AutomationId == pinId);

                    if (image != null)
                    {
                        var pinData = GlobalJson.Data.Plans[PlanId].Pins[pinId];
                        var pinIcon = pinData.PinIcon;

                        if (pinIcon.StartsWith("customicons", StringComparison.OrdinalIgnoreCase))
                            pinIcon = Path.Combine(Settings.DataDirectory, pinIcon);

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
            isPinChangedRegistered = true;
        }

        // lösche Pins
        if (!isPinDeletedRegistered)
        {
            WeakReferenceMessenger.Default.Register<PinDeletedMessage>(this, (r, m) =>
            {
                var pinId = m.Value;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // remove pin-icon on plan
                    var image = PlanContainer.Children
                        .OfType<MR.Gestures.Image>()
                        .FirstOrDefault(i => i.AutomationId == pinId);

                    if (image != null)
                        PlanContainer.Remove(image);
                });
            });
            isPinDeletedRegistered = true;
        }

        // Setze den Titel der Seite und markiere den Plan im ShellManü
        var appShell = Application.Current.Windows[0].Page as AppShell;
        appShell?.HighlightCurrentPlan(this.PlanId);
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
            var image = PlanContainer.Children
                .OfType<MR.Gestures.Image>()
                .FirstOrDefault(i => i.AutomationId == pinId);

            if (image == null && isFirstLoad == false)
                AddPin(pinId, GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon);
        }
    }

    private Task AddPlan()
    {
        //calculate aspect-ratio, resolution and imagesize
        if (GlobalJson.Data.Plans[PlanId].ImageSize.Width > SettingsService.Instance.MaxPdfImageSizeW || GlobalJson.Data.Plans[PlanId].ImageSize.Height > SettingsService.Instance.MaxPdfImageSizeH)
        {
            PlanImage.DownsampleToViewSize = true;
            PlanImage.DownsampleWidth = SettingsService.Instance.MaxPdfImageSizeW;
            PlanImage.DownsampleHeight = SettingsService.Instance.MaxPdfImageSizeH;

            var scaleFac = Math.Min(GlobalJson.Data.Plans[PlanId].ImageSize.Width, GlobalJson.Data.Plans[PlanId].ImageSize.Height) /
                           Math.Max(GlobalJson.Data.Plans[PlanId].ImageSize.Width, GlobalJson.Data.Plans[PlanId].ImageSize.Height);

            if (GlobalJson.Data.Plans[PlanId].ImageSize.Width > GlobalJson.Data.Plans[PlanId].ImageSize.Height)
            {
                PlanImage.WidthRequest = SettingsService.Instance.MaxPdfImageSizeW;
                PlanImage.HeightRequest = SettingsService.Instance.MaxPdfImageSizeH * scaleFac;
            }
            else
            {
                PlanImage.WidthRequest = SettingsService.Instance.MaxPdfImageSizeW * scaleFac;
                PlanImage.HeightRequest = SettingsService.Instance.MaxPdfImageSizeH;
            }
        }
        else
        {
            PlanImage.WidthRequest = GlobalJson.Data.Plans[PlanId].ImageSize.Width;
            PlanImage.HeightRequest = GlobalJson.Data.Plans[PlanId].ImageSize.Height;
        }

        PlanImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);

        PlanContainer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Scale" || e.PropertyName == "Rotation")
            {
                var scale = 1 / PlanContainer.Scale;
                var scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100;
                foreach (MR.Gestures.Image img in PlanContainer.Children.OfType<MR.Gestures.Image>())
                {
                    if (img.AutomationId != null & GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].IsCustomPin != true)
                    {
                        if (scale < scaleLimit & scale > SettingsService.Instance.PinMinScaleLimit / 100)
                            img.Scale = scale * GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].PinScale;

                        if (!GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].IsLockRotate)
                            img.Rotation = PlanContainer.Rotation * -1 + GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].PinRotation;
                    }
                }
            }
        };
        return Task.CompletedTask;
    }

    private void AddPins()
    {
        // Load all Pins at first page opening
        if (GlobalJson.Data.Plans[PlanId].Pins == null) return;

        foreach (var pinId in GlobalJson.Data.Plans[PlanId].Pins.Keys)
        {
            var pinIcon = GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon;
            AddPin(pinId, pinIcon);
        }
    }

    private void AddPin(string pinId, string pinIcon)
    {
        Point _originAnchor = GlobalJson.Data.Plans[PlanId].Pins[pinId].Anchor;
        Point _originPos = GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos;
        Size _planSize = GlobalJson.Data.Plans[PlanId].ImageSize;
        Size _pinSize = GlobalJson.Data.Plans[PlanId].Pins[pinId].Size;
        Double _rotation = PlanContainer.Rotation * -1 + GlobalJson.Data.Plans[PlanId].Pins[pinId].PinRotation;

        if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsCustomPin) // Add Path for Custom Pin-Image
        {
            pinIcon = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, pinIcon);
            _rotation = 0;
        }
        else if (pinIcon.StartsWith("customicons", StringComparison.OrdinalIgnoreCase))
        {
            var _pinIcon = Path.Combine(Settings.DataDirectory, pinIcon);
            if (File.Exists(_pinIcon))
                pinIcon = _pinIcon;
            else
            {
                // Lade Default-Icon falls Custom-Icon nicht existiert
                var iconItem = Settings.IconData.First();
                GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon = iconItem.FileName;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].Size = iconItem.IconSize;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLockRotate = iconItem.IsRotationLocked;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].IsCustomPin = iconItem.IsCustomPin;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].Anchor = iconItem.AnchorPoint;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale = iconItem.IconScale;
                GlobalJson.Data.Plans[PlanId].Pins[pinId].PinColor = iconItem.PinColor;
            }
        }

        // berechne Anchor-Koordinaten
        var smallImage = new MR.Gestures.Image
        {
            Source = pinIcon,
            AutomationId = pinId,
            WidthRequest = GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Width,
            HeightRequest = GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Height,
            AnchorX = GlobalJson.Data.Plans[PlanId].Pins[pinId].Anchor.X,
            AnchorY = GlobalJson.Data.Plans[PlanId].Pins[pinId].Anchor.Y,
            TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * _pinSize.Width),
            TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * _pinSize.Height),
            Rotation = _rotation,
            Scale = PinScaling(pinId),
            InputTransparent = false
        };

        smallImage.Down += (s, e) =>
        {
            if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true) return;
            planContainer.IsPanningEnabled = false;
            activePin = smallImage;
        };

        smallImage.Up += (s, e) =>
        {
            if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true) return;
            planContainer.IsPanningEnabled = true;
            activePin = null;

            var x = smallImage.TranslationX / GlobalJson.Data.Plans[PlanId].ImageSize.Width * densityX;
            var y = smallImage.TranslationY / GlobalJson.Data.Plans[PlanId].ImageSize.Height * densityY;

            var _pin = s as MR.Gestures.Image;
            var dx = _pin.AnchorX * _pin.Width / GlobalJson.Data.Plans[PlanId].ImageSize.Width * densityX;
            var dy = _pin.AnchorY * _pin.Height / GlobalJson.Data.Plans[PlanId].ImageSize.Height * densityY;

            GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos = new Point(x + dx, y + dy);
            GlobalJson.SaveToFile();
        };

        smallImage.Tapped += async (s, e) =>
        {
            if (isTappedHandled)
                return;

            isTappedHandled = true;

            var _pinIcon = GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon;
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={pinId}&sender=///{PlanId}");

            isTappedHandled = false;
        };

        smallImage.DoubleTapped += (s, e) =>
        {
            activePin = smallImage;
            PinSizeSlider.Value = GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinScale * 100;
            PinRotateSlider.Value = GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinRotation;
            planContainer.IsPanningEnabled = false;
            DrawBtn.IsVisible = false;
            SetPinBtn.IsVisible = false;
            PinEditBorder.IsVisible = true;

            if (GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].IsLockRotate)
            {
                rotateLockIcon.IsVisible = false;
                rotateAutoIcon.IsVisible = true;
                rotateModeIcon.IsVisible = false;
                degreesLabel.IsVisible = true;
                degreesModeLabel.Text = Settings.PinEditSliderRotateLock;
            }
            else
            {
                rotateLockIcon.IsVisible = true;
                rotateAutoIcon.IsVisible = false;
                rotateModeIcon.IsVisible = true;
                degreesLabel.IsVisible = false;
                degreesModeLabel.Text = Settings.PinEditSliderRotateUnlock;
            }
        };

        // sort large custom pins on lower z-indexes
        // and small pins on higher z-indexes
        smallImage.ZIndex = 10000 - (int)((GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Width +
                                           GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Height) / 2);

        PlanContainer.Children.Add(smallImage);
        PlanContainer.InvalidateMeasure(); //Aktualisierung forcieren
    }

    private void AdjustImagePosition(MR.Gestures.Image image)
    {
        Point _originAnchor = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Anchor;
        Point _originPos = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Pos;
        Size _planSize = GlobalJson.Data.Plans[PlanId].ImageSize;
        Size _pinSize = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Size;

        image.AnchorX = _originAnchor.X;
        image.AnchorY = _originAnchor.Y;
        image.WidthRequest = _pinSize.Width;
        image.HeightRequest = _pinSize.Height;
        image.TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * image.Width);
        image.TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * image.Height);
    }

    private void PlanImage_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isFirstLoad)
        {
            // Warte darauf, dass Width und Height gültige Werte haben
            if (e.PropertyName == nameof(PlanImage.Width) || e.PropertyName == nameof(PlanImage.Height))
            {
                if (PlanImage.Width > 0 && PlanImage.Height > 0)
                {
                    // Größe des Bildes ist korrekt gesetzt
                    densityX = GlobalJson.Data.Plans[PlanId].ImageSize.Width / PlanImage.Width;
                    densityY = GlobalJson.Data.Plans[PlanId].ImageSize.Height / PlanImage.Height;

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
    }

    public void OnPinching(object sender, PinchEventArgs e)
    {
        planContainer.IsPanningEnabled = false;
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        planContainer.IsPanningEnabled = true;
    }

    private void SetPinClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.PinPlaceMode == 0)
            SetPin(new Point(PlanContainer.AnchorX, PlanContainer.AnchorY));

        if (SettingsService.Instance.PinPlaceMode == 1)
        {
            ToolBtns.IsVisible = false;
            SetPinFrame.IsVisible = true;
            isPinSet = true;
        }
    }

    public void OnTapped(object sender, MR.Gestures.TapEventArgs e)
    {
        if (isPinSet)
        {
            var x = 1 / GlobalJson.Data.Plans[PlanId].ImageSize.Width * e.Center.X * densityX;
            var y = 1 / GlobalJson.Data.Plans[PlanId].ImageSize.Height * e.Center.Y * densityY;

            SetPin(new Point(x, y));

            ToolBtns.IsVisible = true;
            SetPinFrame.IsVisible = false;
            isPinSet = false;
        }
    }

    public void OnLongPressing(object sender, LongPressEventArgs e)
    {
        if (SettingsService.Instance.PinPlaceMode == 2)
        {
            var x = 1 / GlobalJson.Data.Plans[PlanId].ImageSize.Width * e.Center.X  * densityX;
            var y = 1 / GlobalJson.Data.Plans[PlanId].ImageSize.Height * e.Center.Y * densityY;

            SetPin(new Point(x, y));
        }
    }

    public void OnPinSetCancelClicked(object sender, EventArgs e)
    {
        ToolBtns.IsVisible = true;
        SetPinFrame.IsVisible = false;
        isPinSet = false;
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        var scaleSpeed = 1 / PlanContainer.Scale;
        double angle = PlanContainer.Rotation * Math.PI / 180.0;
        double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
        double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);

        if (activePin != null && PinEditBorder.IsVisible == false)
        {
            activePin.TranslationX += deltaX * scaleSpeed;
            activePin.TranslationY += deltaY * scaleSpeed;
        }
        else if (planContainer.IsPanningEnabled)
        {
            planContainer.TranslationX += deltaX * scaleSpeed;
            planContainer.TranslationY += deltaY * scaleSpeed;

            planContainer.AnchorX = 1 / PlanContainer.Width * ((this.Width / 2) - planContainer.TranslationX);
            planContainer.AnchorY = 1 / PlanContainer.Height * ((this.Height / 2) - planContainer.TranslationY);
        }
    }

    private void DrawingClicked(object sender, EventArgs e)
    {
        planContainer.IsPanningEnabled = false;
        SetPinBtn.IsVisible = false;
        DrawBtn.IsVisible = false;
        PenSettingsBtn.IsVisible = true;
        CheckBtn.IsVisible = true;
        EraseBtn.IsVisible = true;

        AddDrawingView();
    }

    private void SetPin(Point _pos,
                        string customName = null,
                        int customPinSizeWidth = 0,
                        int customPinSizeHeight = 0,
                        SKColor? pinColor = null,
                        double customScale = 1)
    {
        var currentPage = (NewPage)Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            // Icon-Daten einlesen
            var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            Settings.IconData = iconItems;

            string _newPin = Settings.IconData.First().FileName;
            string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var iconItem = Settings.IconData.FirstOrDefault(item => item.FileName.Equals(_newPin, StringComparison.OrdinalIgnoreCase));

            Location location = new();
            if (GPSViewModel.Instance.IsRunning)
            {
                location.Longitude = GPSViewModel.Instance.Lon;
                location.Latitude = GPSViewModel.Instance.Lat;
                location.Accuracy = GPSViewModel.Instance.Acc;
            }
            else
                location = null;

            pinColor ??= SKColors.Red;
            Point _anchorPoint = iconItem.AnchorPoint;
            Size _size = iconItem.IconSize;
            bool _isRotationLocked = iconItem.IsRotationLocked;
            bool _isPosLocked = false;
            bool _isCustomPin = false;
            bool _isAllowExport = true;
            string _displayName = iconItem.DisplayName;
            double _scale = iconItem.IconScale;

            if (customName != null)
            {
                _anchorPoint = new Point(0.5, 0.5);
                _size = new Size(customPinSizeWidth, customPinSizeHeight);
                _isRotationLocked = true;
                _isPosLocked = true;
                _isCustomPin = true;
                _newPin = customName;
                _displayName = "";
                _isAllowExport = true;
                _scale = customScale;
            }

            Pin newPinData = new()
            {
                Pos = _pos,
                Anchor = _anchorPoint,
                Size = _size,
                IsLocked = _isPosLocked,
                IsLockRotate = _isRotationLocked,
                IsCustomPin = _isCustomPin,
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
                PinRotation = 0,
                GeoLocation = location != null ? new GeoLocData(location) : null,
                AllowExport = _isAllowExport,
            };

            // Sicherstellen, dass der Plan existiert
            if (GlobalJson.Data.Plans.TryGetValue(PlanId, out Plan value))
            {
                var plan = value;
                plan.Pins ??= [];
                plan.Pins[currentDateTime] = newPinData;

                GlobalJson.Data.Plans[PlanId].PinCount += 1;

                // save data to file
                GlobalJson.SaveToFile();
            }
            else
                Console.WriteLine($"Plan mit ID {PlanId} existiert nicht.");

            AddPin(currentDateTime, newPinData.PinIcon);
        };
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

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
#if WINDOWS
        if (mousePos.X <0 )
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
        double newAnchorX = 1 / PlanContainer.Width * (mousePos.X - planContainer.TranslationX);
        double newAnchorY = 1 / PlanContainer.Height * (mousePos.Y - planContainer.TranslationY);
        double deltaTranslationX = (PlanContainer.Width * (newAnchorX - planContainer.AnchorX)) * (targetScale / planContainer.Scale - 1);
        double deltaTranslationY = (PlanContainer.Height * (newAnchorY - planContainer.AnchorY)) * (targetScale / planContainer.Scale - 1);

        planContainer.AnchorX = newAnchorX;
        planContainer.AnchorY = newAnchorY;
        planContainer.TranslationX -= deltaTranslationX;
        planContainer.TranslationY -= deltaTranslationY;
        planContainer.Scale = targetScale;
#endif
    }

    private void ZoomToPin(string pinId)
    {
        if (GlobalJson.Data.Plans[PlanId].Pins.TryGetValue(pinId, out var pin) && pin != null)
        {
            var pos = GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos;
            planContainer.AnchorX = pos.X;
            planContainer.AnchorY = pos.Y;
            planContainer.TranslationX = (this.Width / 2) - (PlanContainer.Width * pos.X);
            planContainer.TranslationY = (this.Height / 2) - (PlanContainer.Height * pos.Y);
            planContainer.Scale = SettingsService.Instance.DefaultPinZoom;
        }
    }

    private void ImageFit(object sender, EventArgs e)
    {
        planContainer.Rotation = 0;
        planContainer.Scale = Math.Min(this.Width / PlanContainer.Width, this.Height / PlanContainer.Height);
        planContainer.TranslationX = (this.Width - PlanContainer.Width) / 2;
        planContainer.TranslationY = (this.Height - PlanContainer.Height) / 2;
        planContainer.AnchorX = 1 / PlanContainer.Width * ((this.Width / 2) - planContainer.TranslationX);
        planContainer.AnchorY = 1 / PlanContainer.Height * ((this.Height / 2) - planContainer.TranslationY);
    }

    private double PinScaling(string pinId)
    {
        if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsCustomPin != true)
        {
            var scale = 1 / planContainer.Scale;
            var scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100;
            if (scale < scaleLimit & scale > SettingsService.Instance.PinMinScaleLimit / 100)
                return 1 / planContainer.Scale * GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale;
            else
                return scaleLimit * GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale;
        }
        else
            return GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale;
    }

    private void AddDrawingView()
    {
        drawingView = new DrawingView
        {
            BackgroundColor = Colors.Transparent,
            IsMultiLineModeEnabled = true,
            LineWidth = (int)(lineWidth / densityX),
            LineColor = selectedColor,
            InputTransparent = false,
            Scale = planContainer.Scale,
            AnchorX = planContainer.AnchorX,
            AnchorY = planContainer.AnchorY,
            TranslationX = planContainer.TranslationX,
            TranslationY = planContainer.TranslationY,
            Rotation = planContainer.Rotation,
            WidthRequest = PlanImage.Width,
            HeightRequest = PlanImage.Height
        };

        pinBound.Left = int.MaxValue;
        pinBound.Right = int.MinValue;
        pinBound.Top = int.MaxValue;
        pinBound.Bottom = int.MinValue;

        // Füge die EventHandler hinzu
        drawingView.PointDrawn += OnDrawingLineUpdated;

        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PlanView");
        absoluteLayout.Children.Add(drawingView);
    }

    private void RemoveDrawingView()
    {
        var absoluteLayout = this.FindByName<Microsoft.Maui.Controls.AbsoluteLayout>("PlanView");
        if (drawingView != null && absoluteLayout != null)
            absoluteLayout.Children.Remove(drawingView);
    }

    private void OnDrawingLineUpdated(object sender, PointDrawnEventArgs e)
    {
        if (e.Point.X < pinBound.Left)
            pinBound.Left = (int)e.Point.X;
        if (e.Point.X > pinBound.Right)
            pinBound.Right = (int)e.Point.X;
        if (e.Point.Y < pinBound.Top)
            pinBound.Top = (int)e.Point.Y;
        if (e.Point.Y > pinBound.Bottom)
            pinBound.Bottom = (int)e.Point.Y;
    }

    private async void CheckClicked(object sender, EventArgs e)
    {
        if (drawingView.Lines.Count > 0)
        {
            var customPinPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath);
            var customPinName = "custompin_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string filePath = Path.Combine(customPinPath, customPinName);

            await using var imageStream = await DrawingViewService.GetImageStream(
                ImageLineOptions.JustLines(drawingView.Lines,
                new Size(pinBound.Width + (2*lineWidth), pinBound.Height + (2*lineWidth)),
                Brush.Transparent));

            if (imageStream != null)
            {
                if (!Directory.Exists(customPinPath))
                    Directory.CreateDirectory(customPinPath);

                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                using var skBitmap = SKBitmap.Decode(memoryStream);
                using var imageData = skBitmap.Encode(SKEncodedImageFormat.Png, 90);
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    imageData.SaveTo(fileStream);
                }

                SetPin(
                    new Point(
                        (pinBound.Left + pinBound.Width / 2) / GlobalJson.Data.Plans[PlanId].ImageSize.Width * densityX,
                        (pinBound.Top + pinBound.Height / 2) / GlobalJson.Data.Plans[PlanId].ImageSize.Height * densityY),
                    customPinName,
                    skBitmap.Width,
                    skBitmap.Height,
                    new SKColor(drawingView.LineColor.ToUint()),
                    1
                );
            }
        }

        RemoveDrawingView();

        planContainer.IsPanningEnabled = true;
        PenSettingsBtn.IsVisible = false;
        CheckBtn.IsVisible = false;
        EraseBtn.IsVisible = false;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        DrawBtn.IsVisible = true;
    }

    public static SKBitmap CropBitmap(SKBitmap originalBitmap)
    {
        int newWidth = originalBitmap.Width;
        int newHeight = originalBitmap.Height;

        if (newWidth <= 0 || newHeight <= 0)
            throw new ArgumentException("Die neue Bildgröße ist ungültig.");

        var croppedBitmap = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.DrawBitmap(originalBitmap, new SKRect(0, 0, originalBitmap.Width, originalBitmap.Height));
        }
        return croppedBitmap;
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupColorPicker(lineWidth, selectedColor, lineWidthVisibility: true);
        var result = await this.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            selectedColor = Color.FromArgb(result.Result.PenColorHex);
            lineWidth = result.Result.PenWidth;
            if (drawingView != null)
            {
                drawingView.LineColor = selectedColor;
                drawingView.LineWidth = (int)(lineWidth / densityX);
            }
        }
    }

    private void EraseClicked(object sender, EventArgs e)
    {
        drawingView.Clear();
    }

    private void OnFullScreenButtonClicked(object sender, EventArgs e)
    {
        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        activePin = null;
    }

    private void OnRotateSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;
        activePin.Rotation = sliderValue;
        degreesLabel.Text = $"{Math.Round(sliderValue)}°";

        if (sliderValue == 360)
        {
            rotateLockIcon.IsVisible = true;
            rotateAutoIcon.IsVisible = false;
            rotateModeIcon.IsVisible = true;
            degreesLabel.IsVisible = false;
            degreesModeLabel.Text = Settings.PinEditSliderRotateUnlock;
        }
        else
        {
            rotateLockIcon.IsVisible = false;
            rotateAutoIcon.IsVisible = true;
            rotateModeIcon.IsVisible = false;
            degreesLabel.IsVisible = true;
            degreesModeLabel.Text = Settings.PinEditSliderRotateLock;
        }
    }

    private void OnRotateSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;

        if (sliderValue == 360)
        {
            GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinRotation = 0;
            GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].IsLockRotate = false;
        }
        else
        {
            GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinRotation = sliderValue;
            GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].IsLockRotate = true;
        }

        // save data to file
        GlobalJson.SaveToFile();

        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        activePin = null;
    }

    private void OnResizeSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;
        var scale = 1 / PlanContainer.Scale;
        var scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100;

        if (scale < scaleLimit & scale > SettingsService.Instance.PinMinScaleLimit / 100)
            activePin.Scale = scale * sliderValue / 100;
        else
            activePin.Scale = sliderValue / 100;

        percentLabel.Text = $"{Math.Round(sliderValue)}%";
    }

    private void OnResizeSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;

        GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinScale = sliderValue / 100;

        // save data to file
        GlobalJson.SaveToFile();

        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = SettingsService.Instance.PinPlaceMode != 2;
        activePin = null;
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var popup = new PopupPlanEdit(name: GlobalJson.Data.Plans[PlanId].Name,
                                      desc: GlobalJson.Data.Plans[PlanId].Description,
                                      gray: GlobalJson.Data.Plans[PlanId].IsGrayscale,
                                      export: GlobalJson.Data.Plans[PlanId].AllowExport,
                                      planColor: GlobalJson.Data.Plans[PlanId].PlanColor);
        var result = await this.ShowPopupAsync<PlanEditReturn>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            switch (result.Result.NameEntry)
            {
                case "delete":
                    OnDeleteClick();
                    break;

                case "grayscale":
                    OnGrayscaleClick();
                    break;

                default:
                    (Application.Current.Windows[0].Page as AppShell).PlanItems.FirstOrDefault(i => i.PlanId == PlanId).Title = result.Result.NameEntry;
                    Title = result.Result.NameEntry;

                    GlobalJson.Data.Plans[PlanId].Name = result.Result.NameEntry;
                    GlobalJson.Data.Plans[PlanId].Description = result.Result.DescEntry;
                    GlobalJson.Data.Plans[PlanId].AllowExport = result.Result.AllowExport;
                    GlobalJson.Data.Plans[PlanId].PlanColor = result.Result.PlanColor;

                    // Rotate Plan
                    if (result.Result.PlanRotate != 0)
                    {
                        PlanRotate(result.Result.PlanRotate);
                    }

                    // save data to file
                    GlobalJson.SaveToFile();
                    break;
            }
        }
    }

    private async void OnDeleteClick()
    {
        var popup = new PopupDualResponse("Wollen Sie diesen Plan wirklich löschen?", okText: "Löschen", alert: true);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
            var menuitem = (Application.Current.Windows[0].Page as AppShell).PlanItems.FirstOrDefault(i => i.PlanId == PlanId);
            if (menuitem != null)
                (Application.Current.Windows[0].Page as AppShell).PlanItems.Remove(menuitem);

            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "gs_" + GlobalJson.Data.Plans[PlanId].File);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.Plans.Remove(PlanId);

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private void OnGrayscaleClick()
    {
        if (GlobalJson.Data.Plans[PlanId].IsGrayscale)
        {
            string colorImageFile = GlobalJson.Data.Plans[PlanId].File.Replace("gs_", "");
            GlobalJson.Data.Plans[PlanId].File = colorImageFile;
            GlobalJson.Data.Plans[PlanId].IsGrayscale = false;
        }
        else
        {
            string grayImageFile = "gs_" + GlobalJson.Data.Plans[PlanId].File;
            string grayImagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, grayImageFile);
            if (!File.Exists(grayImagePath))
            {
                using var originalStream = File.OpenRead(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File));
                using var originalBitmap = SKBitmap.Decode(originalStream);
                var grayBitmap = Helper.ConvertToGrayscale(originalBitmap);
                using SKImage image = SKImage.FromBitmap(grayBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                using var fileStream = File.OpenWrite(grayImagePath);
                data.SaveTo(fileStream);
            }
            GlobalJson.Data.Plans[PlanId].File = grayImageFile;
            GlobalJson.Data.Plans[PlanId].IsGrayscale = true;
        }

        // save data to file
        GlobalJson.SaveToFile();

        isFirstLoad = true;

        PlanImage.Source = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);
    }

    private async void PlanRotate(int angle)
    {
        var imagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);

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

        GlobalJson.Data.Plans[PlanId].ImageSize = new Size(rotatedBitmap.Width, rotatedBitmap.Height);
        GlobalJson.Data.Plans[PlanId].File = imagefile;

        PlanContainer.SizeChanged += OnPlanContainerReady;
        await AddPlan();

        // Umpositionierung der Pins
        if (GlobalJson.Data.Plans[PlanId].Pins != null)
        {
            foreach (var pinId in GlobalJson.Data.Plans[PlanId].Pins.Keys)
            {
                var delPin = PlanContainer.Children
                    .OfType<MR.Gestures.Image>()
                    .FirstOrDefault(i => i.AutomationId == pinId);
                PlanContainer.Remove(delPin);

                GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos = RotatePin(GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos, angle);

                if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLockRotate)
                    GlobalJson.Data.Plans[PlanId].Pins[pinId].PinRotation = (GlobalJson.Data.Plans[PlanId].Pins[pinId].PinRotation + angle) % 360;

                var pinIcon = GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon;
                AddPin(pinId, pinIcon);
            }
        }

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnPlanContainerReady(object sender, EventArgs e)
    {
        PlanContainer.SizeChanged -= OnPlanContainerReady; // einmalig
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
            ?.PlanItems.FirstOrDefault(i => i.PlanId == PlanId)!.Title = Title;

        GlobalJson.Data.Plans[PlanId].Name = Title;

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