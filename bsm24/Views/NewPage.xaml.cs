#nullable disable

using bsm24.Models;
using bsm24.Services;
using bsm24.ViewModels;
using Mopups.Services;
using MR.Gestures;
using SkiaSharp;

namespace bsm24.Views;

public partial class NewPage : IQueryAttributable
{
    public string PinUpdate { get; set; }
    public string PlanId { get; set; }
    public string PinDelete { get; set; }
    public string PageTitle { get; set; } = "";
    public string PinZoom;
    private MR.Gestures.Image activePin;
    private double densityX, densityY;
    private bool isFirstLoad = true;
    private Point mousePos;
    private readonly TransformViewModel planContainer;

    public NewPage(string planId, string zoomToPin = null)
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
        PlanId = planId;
        PinZoom = zoomToPin;
        PageTitle = GlobalJson.Data.Plans[PlanId].Name;
        planContainer = (TransformViewModel)PlanContainer.BindingContext;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (isFirstLoad)
        {
            AddPlan();
            PlanImage.PropertyChanged += PlanImage_PropertyChanged;
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
                image.Source = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].PinIcon;
                image.AnchorX = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.X;
                image.AnchorY = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.Y;
                image.Scale = PinScaling(PinUpdate);

                if (GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].IsLocked == true)
                    image.Opacity = .3;
                else
                    image.Opacity = 1;
                AdjustImagePosition(image);
            }
        }
        if (query.TryGetValue("pinDelete", out object value2))
        {
            // remove pin-icon on plan
            PinDelete = value2 as string;
            var image = PlanContainer.Children
                .OfType<MR.Gestures.Image>()
                .FirstOrDefault(i => i.AutomationId == PinDelete);
            PlanContainer.Remove(image);

            // delete all images
            foreach (var del_image in GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos)
            {
                string file;

                file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos[del_image.Key].File);
                if (File.Exists(file))
                    File.Delete(file);

                file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[PinDelete].Fotos[del_image.Key].File);
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

    private void AddPlan()
    {
        PlanImage.Source = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);

        PlanContainer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Scale")
            {
                var scale = 1 / PlanContainer.Scale;
                var scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100;
                foreach (MR.Gestures.Image img in PlanContainer.Children.Cast<MR.Gestures.Image>())
                {
                    if (img.AutomationId != null)
                    {
                        // this may cause performance issues !!!
                        if (scale < scaleLimit & scale > SettingsService.Instance.PinMinScaleLimit / 100)
                            img.Scale = scale * GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].PinScale;

                        if (!GlobalJson.Data.Plans[PlanId].Pins[img.AutomationId].IsLockRotate)
                            img.Rotation = PlanContainer.Rotation * -1;
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
            Rotation = PlanContainer.Rotation * -1,
            Scale = PinScaling(pinId)
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

            var _pin = (s as MR.Gestures.Image);
            var dx = (_pin.AnchorX * _pin.Width) / GlobalJson.Data.Plans[PlanId].ImageSize.Width * densityX;
            var dy = (_pin.AnchorY * _pin.Height) / GlobalJson.Data.Plans[PlanId].ImageSize.Height * densityY;

            GlobalJson.Data.Plans[PlanId].Pins[pinId].Pos = new Point(x + dx, y + dy);
            GlobalJson.SaveToFile();
        };

        smallImage.Tapping += async (s, e) =>
        {
            var _pinIcon = GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon;
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={pinId}&pinIcon={_pinIcon}");
        };

        PlanContainer.Children.Add(smallImage);
        PlanContainer.InvalidateMeasure(); //Aktualisierung forcieren

        // set transparency
        if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true)
            smallImage.Opacity = .3;
        else
            smallImage.Opacity = 1;
    }

    private void AdjustImagePosition(MR.Gestures.Image image)
    {
        // Hole die Ankerpunkte, Positionen und Bildgröße wie in deinem Loaded-Handler
        Point _originAnchor = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Anchor;
        Point _originPos = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Pos;
        Size _planSize = GlobalJson.Data.Plans[PlanId].ImageSize;
        Size _pinSize = GlobalJson.Data.Plans[PlanId].Pins[image.AutomationId].Size;

        // Setze die Ankerpunkte und die Übersetzungen (Translation)
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

                    if (PinZoom != null)
                        ZoomToPin(PinZoom);
                    else
                        ImageFit();

                    AddPins();
                }
            }
        }
    }

    public void OnDoubleTapped(object sender, EventArgs e)
    {
        ImageFit();
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

        if (activePin != null)
        {
            activePin.TranslationX += deltaX * scaleSpeed;
            activePin.TranslationY += deltaY * scaleSpeed;
        }
        else
        {
            planContainer.TranslationX += deltaX * scaleSpeed;
            planContainer.TranslationY += deltaY * scaleSpeed;

            planContainer.AnchorX = 1 / PlanContainer.Width * ((this.Width / 2) - PlanContainer.TranslationX);
            planContainer.AnchorY = 1 / PlanContainer.Height * ((this.Height / 2) - PlanContainer.TranslationY);
        }
    }

    private void SetPinClicked(object sender, EventArgs e)
    {
        var currentPage = (NewPage)Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string newPin = "a_pin_red.png";
            var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(newPin, StringComparison.OrdinalIgnoreCase));
            Pin newPinData = new()
            {
                Pos = new(PlanContainer.AnchorX, PlanContainer.AnchorY),
                Anchor = iconItem.AnchorPoint,
                Size = iconItem.IconSize,
                IsLocked = false,
                IsLockRotate = iconItem.IsRotationLocked,
                PinName = iconItem.DisplayName,
                PinDesc = "",
                PinLocation = "",
                PinIcon = newPin,
                Fotos = [],
                PinColor = SKColors.Red,
                PinScale = iconItem.IconScale
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
        };
    }

    private void SetRegionClicked(object sender, EventArgs e)
    {

    }

    private void OnMouseMoved(object sender, MouseEventArgs e)
    {
        mousePos = e.Center;
    }

    private async void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        double zoomFactor = e.ScrollDelta.Y > 0 ? 1.4 : 0.6;
        double targetScale = PlanContainer.Scale * zoomFactor;

        planContainer.AnchorX = 1 / PlanContainer.Width * (mousePos.X - PlanContainer.TranslationX);
        planContainer.AnchorY = 1 / PlanContainer.Height * (mousePos.Y - PlanContainer.TranslationY);

        await PlanContainer.ScaleTo(targetScale, 60, Easing.Linear);

        planContainer.Scale = targetScale;
    }

    private async void OnEditClick(object sender, EventArgs e)
    {
        var popup = new PopupEntry(title: "Plan umbenennen...", inputTxt: GlobalJson.Data.Plans[PlanId].Name);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        if (result != null)
        {
            // ändert das dazugehörige Menü-Item
            (Application.Current.Windows[0].Page as AppShell).Items.FirstOrDefault(i => i.AutomationId == PlanId).Title = result;

            // ändert Titel vom View
            Title = result;

            GlobalJson.Data.Plans[PlanId].Name = result;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnDeleteClick(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie diesen Plan wirklich löschen?", okText: "Löschen", alert: true);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
            // löscht das dazugehörige Menü-Item
            var shellItem = (Application.Current.Windows[0].Page as AppShell).Items.FirstOrDefault(i => i.AutomationId == PlanId);
            if (shellItem != null)
                (Application.Current.Windows[0].Page as AppShell).Items.Remove(shellItem);

            string file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[PlanId].File);
            if (File.Exists(file))
                File.Delete(file);

            GlobalJson.Data.Plans.Remove(PlanId);

            // save data to file
            GlobalJson.SaveToFile();
        }
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

    private void ImageFit()
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
        var scale = 1 / planContainer.Scale;
        var scaleLimit = SettingsService.Instance.PinMaxScaleLimit / 100;
        if (scale < scaleLimit & scale > SettingsService.Instance.PinMinScaleLimit / 100)
            return 1 / planContainer.Scale * GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale;
        else
            return scaleLimit * GlobalJson.Data.Plans[PlanId].Pins[pinId].PinScale;
    }
}
