#nullable disable

using bsm24.Models;
using bsm24.Services;
using bsm24.ViewModels;
using Mopups.Services;
using MR.Gestures;
using System.Globalization;

namespace bsm24.Views;

[QueryProperty(nameof(PinUpdate), "pinUpdate")]
[QueryProperty(nameof(PinDelete), "pinDelete")]

public partial class NewPage: IQueryAttributable
{
    public string PinUpdate { get; set; }
    public string PlanId { get; set; }
    public string PinDelete { get; set; }
    public string PageTitle { get; set; } = "";

    private MR.Gestures.Image activePin;

    private double densityX, densityY;

    private bool isFirstLoad = true;

    private Point mousePos;

    public NewPage(string planId)
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
        PlanId = planId;
        PageTitle = GlobalJson.Data.Plans[PlanId].Name;
        OnPropertyChanged(nameof(PageTitle));
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
                image.AnchorX = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.X;
                image.AnchorY = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].Anchor.Y;
                image.Source = GlobalJson.Data.Plans[PlanId].Pins[PinUpdate].PinIcon;  // Bildquelle ändern
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

        if (BindingContext is TransformViewModel viewModel)
        {
            // Set Plan Position
            //viewModel.TranslationX = planPos.X;
            //viewModel.TranslationY = planPos.Y;
        }

        PlanContainer.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Scale")
            {
                var scale = 1 / PlanContainer.Scale;
                var scaleLimit = Convert.ToDouble(SettingsService.Instance.PinScaleLimit);
                foreach (MR.Gestures.Image img in PlanContainer.Children.Cast<MR.Gestures.Image>())
                {
                    if (img.AutomationId != null)
                    {
                        // this may cause performance issues !!!
                        if (1 / PlanContainer.Scale < scaleLimit)
                            img.Scale = scale;

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
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
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
        };

        var scaleLimit = Convert.ToDouble(SettingsService.Instance.PinScaleLimit);
        if (scaleLimit < 1) smallImage.Scale = scaleLimit;

        //smallImage.BindingContext = SettingsService.Instance;
        //smallImage.SetBinding(ScaleProperty, new Binding("PinSize"));

        smallImage.Down += (s, e) =>
        {
            if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true) return;
            planPanContainer.IsPanningEnabled = false;
            activePin = smallImage;
        };

        smallImage.Up += (s, e) =>
        {
            if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsLocked == true) return;
            planPanContainer.IsPanningEnabled = true;
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
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={pinId}&pinIcon={pinIcon}");
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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (isFirstLoad)
        {
            AddPlan();
            // Verfolge Änderungen an den Eigenschaften Width und Height des PlanImage
            PlanImage.PropertyChanged += PlanImage_PropertyChanged;
        }
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
                    AddPins();
                }
            }
        }
    }

    public void OnPinching(object sender, PinchEventArgs e)
    {
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        planPanContainer.IsPanningEnabled = false;
    }

    public void OnPinched(object sender, PinchEventArgs e)
    {
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        planPanContainer.IsPanningEnabled = true;
    }

    public void OnRotating(object sender, RotateEventArgs e)
    {
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        planPanContainer.IsPanningEnabled = false;
    }

    public void OnRotated(object sender, RotateEventArgs e)
    {
        if (e.NumberOfTouches == 0)
        {
            var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
            planPanContainer.AnchorX = 0.5;
            planPanContainer.AnchorY = 0.5;
            planPanContainer.IsPanningEnabled = true;
        }
    }

    public void OnDown(object sender, DownUpEventArgs e)
    {
        if (e.NumberOfTouches == 2)
        {
            var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
            planPanContainer.IsPanningEnabled = false;

            // Korrektur für rotierte Pläne einbauen...
            planPanContainer.AnchorX = 1 / PlanContainer.Width * (e.Center.X - PlanContainer.TranslationX);
            planPanContainer.AnchorY = 1 / PlanContainer.Height * (e.Center.Y - PlanContainer.TranslationY);
        }
    }

    public void OnPanning(object sender, PanEventArgs e)
    {
        if (activePin != null)
        {
            // Skalierungsfaktor
            var scaleSpeed = 1 / PlanContainer.Scale;

            // Winkel in Radiant
            double angle = PlanContainer.Rotation * Math.PI / 180.0;

            // Berechnung der Delta-Werte basierend auf Rotation mit Spiegelung
            double deltaX = e.DeltaDistance.X * Math.Cos(angle) - -e.DeltaDistance.Y * Math.Sin(angle);
            double deltaY = -e.DeltaDistance.X * Math.Sin(angle) + e.DeltaDistance.Y * Math.Cos(angle);

            // Skalierung anwenden
            activePin.TranslationX += deltaX * scaleSpeed;
            activePin.TranslationY += deltaY * scaleSpeed;
        }
    }

    private void SetPinClicked(object sender, EventArgs e)
    {
        var currentPage = (NewPage)Shell.Current.CurrentPage;
        if (currentPage != null)
        {
            string currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string newPin = "a_pin_red.png";

            var _scale = PlanContainer.Scale;
            Point screenCenter = new(this.Width / 2, this.Height / 2);
            Point planPos = new(PlanContainer.TranslationX, PlanContainer.TranslationY);
            Point anchorDif = new((0.5 - PlanContainer.AnchorX) * PlanContainer.Width * _scale, (0.5 - PlanContainer.AnchorY) * PlanContainer.Height * _scale);
            Point planAnchor = new(PlanContainer.Width * PlanContainer.AnchorX, PlanContainer.Height * PlanContainer.AnchorY);
            double _x = (screenCenter.X - planPos.X - planAnchor.X - anchorDif.X) / _scale;
            double _y = (screenCenter.Y - planPos.Y - planAnchor.Y - anchorDif.Y) / _scale;

            // Berechnung des Rotationswinkels in Radiant (für die Umrechnung)
            double angleInRadians = PlanContainer.Rotation * Math.PI / 180;

            // Wende die Rotation auf den Punkt (_x, _y) an, um ihn in den gedrehten Raum zu bringen
            double rotatedX = _x * Math.Cos(angleInRadians) - -_y * Math.Sin(angleInRadians);
            double rotatedY = -_x * Math.Sin(angleInRadians) + _y * Math.Cos(angleInRadians);

            Point pos = new(0.5 + (1 / PlanContainer.Width * rotatedX), 0.5 + (1 / PlanContainer.Height * rotatedY));

            Pin newPinData = new()
            {
                Pos = pos,
                Anchor = Settings.PinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).anchor,
                Size = Settings.PinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).size,
                IsLocked = false,
                IsLockRotate = false,
                InfoTxt = "",
                PinTxt = Settings.PinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).imageName,
                PinIcon = newPin,
                Fotos = []
            };

            // Sicherstellen, dass der Plan existiert
            if (GlobalJson.Data.Plans.TryGetValue(PlanId, out Plan value))
            {
                var plan = value;

                // Überprüfen, ob die Pins-Struktur initialisiert ist
                plan.Pins ??= [];

                // Neuen Pin hinzufügen
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

    private void OnMouseScroll(object sender, ScrollWheelEventArgs e)
    {
        double zoomFactor = e.ScrollDelta.Y > 0 ? 1.1 : 0.9; // Sanfter Zoom
        double newScale = PlanContainer.Scale * zoomFactor;
        newScale = Math.Max(0.1, Math.Min(newScale, 10));

        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        planPanContainer.AnchorX = mousePos.X / PlanContainer.Width;
        planPanContainer.AnchorY = mousePos.Y / PlanContainer.Height;
        planPanContainer.Scale = newScale;
    }

    private async void OnEditButtonClicked(object sender, EventArgs e)
    {
        var popup1 = new PopupEditPlan(GlobalJson.Data.Plans[PlanId].Name);
        await MopupService.Instance.PushAsync(popup1);
        var result = await popup1.PopupDismissedTask; //Item1=String Item2=Rotation Integer

        switch (result)
        {
            case null:
                break;

            case "Delete":
                var popup2 = new PopupDualResponse("Wollen Sie diesen Plan wirklich löschen?");
                await MopupService.Instance.PushAsync(popup2);
                var result2 = await popup2.PopupDismissedTask;
                if (result2 != null)
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
                break;

            default:
                // ändert das dazugehörige Menü-Item
                (Application.Current.Windows[0].Page as AppShell).Items.FirstOrDefault(i => i.AutomationId == PlanId).Title = result;

                // ändert Titel vom View
                Title = result;

                GlobalJson.Data.Plans[PlanId].Name = result;

                // save data to file
                GlobalJson.SaveToFile();
                break;
        }
    }
}

public class PinSizeWithFactorConverter : IValueConverter
{
    // Dynamischer Faktor, den du im Converter ändern kannst
    public double AdditionalFactor { get; set; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pinSize)
        {
            // PinSize mit dem zusätzlichen Faktor multiplizieren oder addieren
            return pinSize * AdditionalFactor;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Falls du bidirektionale Bindungen verwenden möchtest
        if (value is double newValue)
        {
            return newValue / AdditionalFactor;
        }
        return value;
    }
}