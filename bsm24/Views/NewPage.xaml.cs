#nullable disable

using bsm24.Models;
using bsm24.Services;
using bsm24.ViewModels;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Views;
using CommunityToolkit.Maui.Views;
using Mopups.Services;
using MR.Gestures;
using SkiaSharp;
using Application = Microsoft.Maui.Controls.Application;

#if WINDOWS
using bsm24.Platforms.Windows;
#endif

namespace bsm24.Views;

public partial class NewPage : IQueryAttributable
{
    public string PageTitle { get; set; } = "";
    public string PinUpdate;
    public string PlanId;
    public string PinDelete;
    public string PinZoom = null;
    private MR.Gestures.Image activePin = null;
    private double densityX, densityY;
    private bool isFirstLoad = true;
    private Point mousePos;
    private readonly TransformViewModel planContainer;
    private DrawingView drawingView;
    private int lineWidth = 15;
    private Color selectedColor = new(255, 0, 0);
    bool isTappedHandled = false;
    SKRectI pinBound;

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
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (isFirstLoad)
        {
            AddPlan();
            PlanImage.PropertyChanged += PlanImage_PropertyChanged;
        }

        if (PinZoom != null)
        {
            ZoomToPin(PinZoom);
            PinZoom = null;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("pinUpdate", out object value1))
        {
            PinUpdate = value1 as string;
            var image = PlanContainer.Children
                .OfType<MR.Gestures.Image>()
                .FirstOrDefault(i => i.AutomationId == PinUpdate);
            if (image != null)
            {
                var pinIcon = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].PinIcon;
                if (pinIcon.StartsWith("customicons", StringComparison.OrdinalIgnoreCase))
                    pinIcon = Path.Combine(Settings.DataDirectory, pinIcon);
                
                image.Source = pinIcon;
                image.AnchorX = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.X;
                image.AnchorY = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.Y;
                image.Rotation = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].IsLockRotate ?
                                 GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].PinRotation :
                                 PlanContainer.Rotation * -1 + GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].PinRotation;
                image.Scale = PinScaling(PinUpdate);

                if (GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].IsLocked == true)
                    image.Opacity = .5;
                else
                    image.Opacity = 1;
                AdjustImagePosition(image);
            }
        }
        if (query.TryGetValue("pinDelete", out object value2))
        {
            PinDelete = value2 as string;

            if (GlobalJson.Data.Plans[PlanId].Pins.TryGetValue(PinDelete, out var pin)) // check if pin exists
            {
                // remove pin-icon on plan
                var image = PlanContainer.Children
                    .OfType<MR.Gestures.Image>()
                    .FirstOrDefault(i => i.AutomationId == PinDelete);
                PlanContainer.Remove(image);

                // delete all images
                foreach (var del_image in GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos)
                {
                    string file;
                    file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos[del_image.Key].File);
                    if (File.Exists(file))
                        File.Delete(file);

                    file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos[del_image.Key].File);
                    if (File.Exists(file))
                        File.Delete(file);
                }

                // remove custom pin image
                if (GlobalJson.Data.Plans[PlanId].Pins[PinDelete].IsCustomPin)
                {
                    String file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, GlobalJson.Data.Plans[PlanId].Pins[PinDelete].PinIcon);
                    if (File.Exists(file))
                        File.Delete(file);
                }

                // remove pin from database
                var plan = GlobalJson.Data.Plans[PlanId];
                plan.Pins.Remove(PinDelete);

                // save data to file
                GlobalJson.SaveToFile();
            }
        }
        if (query.TryGetValue("pinZoom", out object value3))
        {
            PinZoom = value3 as string;
        }
    }

    private void AddPlan()
    {
        //calculate aspect-ratio, resolution and imagesize
        if (GlobalJson.Data.Plans[PlanId].ImageSize.Width > 7168 || GlobalJson.Data.Plans[PlanId].ImageSize.Height > 7168)
        {
            PlanImage.DownsampleToViewSize = true;
            PlanImage.DownsampleWidth = 7168;
            PlanImage.DownsampleHeight = 7168;

            var scaleFac = Math.Min(GlobalJson.Data.Plans[PlanId].ImageSize.Width, GlobalJson.Data.Plans[PlanId].ImageSize.Height) /
                           Math.Max(GlobalJson.Data.Plans[PlanId].ImageSize.Width, GlobalJson.Data.Plans[PlanId].ImageSize.Height);

            if (GlobalJson.Data.Plans[PlanId].ImageSize.Width > GlobalJson.Data.Plans[PlanId].ImageSize.Height)
            {
                PlanImage.WidthRequest = 7168;
                PlanImage.HeightRequest = 7168 * scaleFac;
            }
            else
            {
                PlanImage.WidthRequest = 7168 * scaleFac;
                PlanImage.HeightRequest = 7168;
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
            pinIcon = Path.Combine(Settings.DataDirectory, pinIcon);
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
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={pinId}");

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
        };

        // sort large custom pins on lower z-indexes
        // and small pins on higher z-indexes
        smallImage.ZIndex = 10000 - (int)((GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Width +
                                           GlobalJson.Data.Plans[PlanId].Pins[pinId].Size.Height) / 2);

        PlanContainer.Children.Add(smallImage);
        PlanContainer.InvalidateMeasure(); //Aktualisierung forcieren

        // set transparency
        if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true)
            smallImage.Opacity = .5;
        else
            smallImage.Opacity = 1;
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

    private void SetPinClicked(object sender, EventArgs e)
    {
        SetPin();
    }

    private void SetRegionClicked(object sender, EventArgs e)
    {
        AddDrawingView();
    }

    private void SetPin(string customName = null, int customPinSizeWidth = 0, int customPinSizeHeight = 0, double customPinX = 0, double customPinY = 0, SKColor? pinColor = null)
    {
        var currentPage = (NewPage)Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            // Icon-Daten einlesen
            var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            Settings.IconData = iconItems;

            string _newPin = "a_pin_red.png";
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
            Point _pos = new(PlanContainer.AnchorX, PlanContainer.AnchorY);
            Point _anchorPoint = iconItem.AnchorPoint;
            Size _size = iconItem.IconSize;
            bool _isRotationLocked = iconItem.IsRotationLocked;
            bool _isCustomPin = false;
            string _displayName = iconItem.DisplayName;

            if (customName != null)
            {
                _pos = new Point(customPinX, customPinY);
                _anchorPoint = new Point(0.5, 0.5);
                _size = new Size(customPinSizeWidth, customPinSizeHeight);
                _isRotationLocked = true;
                _isCustomPin = true;
                _newPin = customName;
                _displayName = "";
            }

            Pin newPinData = new()
            {
                Pos = _pos,
                Anchor = _anchorPoint,
                Size = _size,
                IsLocked = false,
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
                PinScale = iconItem.IconScale,
                PinRotation = 0,
                GeoLocation = location != null ? new GeoLocData(location) : null,
                AllowExport = true,
            };

            // Sicherstellen, dass der Plan existiert
            if (GlobalJson.Data.Plans.TryGetValue(PlanId, out Plan value))
            {
                var plan = value;
                plan.Pins ??= [];
                plan.Pins[currentDateTime] = newPinData;

                // save data to file
                GlobalJson.SaveToFile();
            }
            else
                Console.WriteLine($"Plan mit ID {PlanId} existiert nicht.");

            AddPin(currentDateTime, newPinData.PinIcon);
        }
        ;
    }

    private void OnMouseMoved(object sender, MouseEventArgs e)
    {
        mousePos = e.Center;

#if WINDOWS
        if (KeyboardHelper.IsShiftPressed())
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
    }

    private void ZoomToPin(string pinId)
    {
        var pos = GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos;
        planContainer.AnchorX = pos.X;
        planContainer.AnchorY = pos.Y;
        planContainer.TranslationX = (this.Width / 2) - (PlanContainer.Width * pos.X);
        planContainer.TranslationY = (this.Height / 2) - (PlanContainer.Height * pos.Y);
        planContainer.Scale = Settings.DefaultPinZoom;
    }

    private void ImageFit(object sender, EventArgs e)
    {
        planContainer.Rotation = 0;
        planContainer.Scale = Math.Min(this.Width / PlanContainer.Width, this.Height / PlanContainer.Height);
        planContainer.TranslationX = (this.Width - PlanContainer.Width) / 2;
        planContainer.TranslationY = (this.Height - PlanContainer.Height) / 2;
        planContainer.AnchorX = 1 / PlanContainer.Width * ((this.Width / 2) - PlanContainer.TranslationX);
        planContainer.AnchorY = 1 / PlanContainer.Height * ((this.Height / 2) - PlanContainer.TranslationY);
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
            return 1;
    }

    private void AddDrawingView()
    {
        planContainer.IsPanningEnabled = false;
        SetPinBtn.IsVisible = false;
        DrawBtn.IsVisible = false;

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

        PenSettingsBtn.IsVisible = true;
        CheckBtn.IsVisible = true;
        EraseBtn.IsVisible = true;
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
                                                new Size(pinBound.Width, pinBound.Height),
                                                Brush.Transparent));
            if (imageStream != null)
            {
                if (!Directory.Exists(customPinPath))
                    Directory.CreateDirectory(customPinPath);

                using var memoryStream = new MemoryStream();
                await imageStream.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);
                using var _skBitmap = SKBitmap.Decode(memoryStream);
                var skBitmap = CropBitmap(_skBitmap, 4);
                var resizedBitmap = new SKBitmap(pinBound.Width + (int)drawingView.LineWidth, pinBound.Height + (int)drawingView.LineWidth);
                var samplingOptions = new SKSamplingOptions(SKFilterMode.Linear);
                skBitmap.ScalePixels(resizedBitmap, samplingOptions);

                using var imageData = resizedBitmap.Encode(SKEncodedImageFormat.Png, 90); // 90 = Qualität
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    imageData.SaveTo(fileStream);
                }

                SetPin(customPinName, resizedBitmap.Width, resizedBitmap.Height,
                      (pinBound.Left + pinBound.Width / 2) / GlobalJson.Data.Plans[PlanId].ImageSize.Width * densityX,
                      (pinBound.Top + pinBound.Height / 2) / GlobalJson.Data.Plans[PlanId].ImageSize.Height * densityY,
                      new SKColor(drawingView.LineColor.ToUint()));
            }
        }

        RemoveDrawingView();
        planContainer.IsPanningEnabled = true;
        PenSettingsBtn.IsVisible = false;
        CheckBtn.IsVisible = false;
        EraseBtn.IsVisible = false;
        SetPinBtn.IsVisible = true;
        DrawBtn.IsVisible = true;
    }

    public static SKBitmap CropBitmap(SKBitmap originalBitmap, int cropWidth)
    {
        int newWidth = originalBitmap.Width - (2 * cropWidth);
        int newHeight = originalBitmap.Height - (2 * cropWidth);

        if (newWidth <= 0 || newHeight <= 0)
            throw new ArgumentException("Die neue Bildgröße ist ungültig.");

        var croppedBitmap = new SKBitmap(newWidth, newHeight);
        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.DrawBitmap(originalBitmap, new SKRect(-5, -5, originalBitmap.Width - 5, originalBitmap.Height - 5));
        }
        return croppedBitmap;
    }

    private async void PenSettingsClicked(object sender, EventArgs e)
    {
        if (MopupService.Instance.PopupStack.Any())
            return;

        var popup = new PopupColorPicker(lineWidth, selectedColor);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        selectedColor = result.Item1;
        lineWidth = result.Item2;

        drawingView.LineColor = result.Item1;
        drawingView.LineWidth = (int)(result.Item2 / densityX);
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
        SetPinBtn.IsVisible = true;
        activePin = null;
    }

    private void OnRotateSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;
        activePin.Rotation = sliderValue;
        degreesLabel.Text = $"{Math.Round(sliderValue)}°";
    }

    private void OnRotateSliderDragCompleted(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;

        GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].PinRotation = sliderValue;
        GlobalJson.Data.Plans[PlanId].Pins[activePin.AutomationId].IsLockRotate = true;

        // save data to file
        GlobalJson.SaveToFile();

        planContainer.IsPanningEnabled = true;
        PinEditBorder.IsVisible = false;
        DrawBtn.IsVisible = true;
        SetPinBtn.IsVisible = true;
        activePin = null;
    }

    private void OnResizeSliderValueChanged(object sender, EventArgs e)
    {
        var sliderValue = ((Microsoft.Maui.Controls.Slider)sender).Value;
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
        SetPinBtn.IsVisible = true;
        activePin = null;
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (MopupService.Instance.PopupStack.Any())
            return;

        var popup = new PopupPlanEdit(name: GlobalJson.Data.Plans[PlanId].Name,
                                      desc: GlobalJson.Data.Plans[PlanId].Description,
                                      gray: GlobalJson.Data.Plans[PlanId].IsGrayscale,
                                      export: GlobalJson.Data.Plans[PlanId].AllowExport);
        await MopupService.Instance.PushAsync(popup);
        var (result1, result2, result3) = await popup.PopupDismissedTask;

        switch (result1)
        {
            case "delete":
                OnDeleteClick();
                break;

            case "grayscale":
                OnGrayscaleClick();
                break;

            case null:
                break;

            default:
                (Application.Current.Windows[0].Page as AppShell).PlanItems.FirstOrDefault(i => i.PlanId == PlanId).Title = result1;
                Title = result1;

                GlobalJson.Data.Plans[PlanId].Name = result1;
                GlobalJson.Data.Plans[PlanId].Description = result2;
                GlobalJson.Data.Plans[PlanId].AllowExport = result3;

                // save data to file
                GlobalJson.SaveToFile();
                break;
        }
    }

    private async void OnDeleteClick()
    {
        var popup = new PopupDualResponse("Wollen Sie diesen Plan wirklich löschen?", okText: "Löschen", alert: true);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
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
}
