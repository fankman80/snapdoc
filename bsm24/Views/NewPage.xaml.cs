#nullable disable

using bsm24.Services;
using bsm24.ViewModels;
using Mopups.Services;
using MR.Gestures;
using System.Globalization;

namespace bsm24.Views;

[QueryProperty(nameof(PinUpdate), "pinUpdate")]
[QueryProperty(nameof(PinDelete), "pinDelete")]

public partial class NewPage : IQueryAttributable
{
    public string PinUpdate { get; set; }
    public string PlanId { get; set; }
    public string PinDelete { get; set; }
    public string PageTitle { get; set; } = "";

    private MR.Gestures.Image activePin;

    private double densityX, densityY;

    private bool isFirstLoad = true;

    public NewPage(string planId)
    {
        InitializeComponent();
        BindingContext = new TransformViewModel();
        PlanId = planId;
        PageTitle = GlobalJson.Data.plans[PlanId].name;
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
                image.AnchorX = GlobalJson.Data.plans[PlanId].pins[PinUpdate].anchor.X;
                image.AnchorY = GlobalJson.Data.plans[PlanId].pins[PinUpdate].anchor.Y;
                image.Source = GlobalJson.Data.plans[PlanId].pins[PinUpdate].pinIcon;  // Bildquelle ändern
                if (GlobalJson.Data.plans[PlanId].pins[PinUpdate].isLocked == true)
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
            foreach (var del_image in GlobalJson.Data.plans[PlanId].pins[PinDelete].images)
            {
                string file;

                file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.imagePath, GlobalJson.Data.plans[PlanId].pins[PinDelete].images[del_image.Key].file);
                if (File.Exists(file))
                    File.Delete(file);

                file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.thumbnailPath, GlobalJson.Data.plans[PlanId].pins[PinDelete].images[del_image.Key].file);
                if (File.Exists(file))
                    File.Delete(file);
            }

            // remove pin from database
            var plan = GlobalJson.Data.plans[PlanId];
            plan.pins.Remove(PinDelete);

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private void AddPlan()
    {
        PlanImage.Source = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.planPath, GlobalJson.Data.plans[PlanId].file);

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

                        if (!GlobalJson.Data.plans[PlanId].pins[img.AutomationId].isLockRotate)
                            img.Rotation = PlanContainer.Rotation * -1;
                    }
                }
            }
        };
    }

    private void AddPins()
    {
        // Load all Pins at first page opening
        if (GlobalJson.Data.plans[PlanId].pins == null) return;

        foreach (var pinId in GlobalJson.Data.plans[PlanId].pins.Keys)
        {
            var pinIcon = GlobalJson.Data.plans[PlanId].pins[pinId].pinIcon;
            AddPin(pinId, pinIcon);
        }
    }

    private void AddPin(string pinId, string pinIcon)
    {
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        Point _originAnchor = GlobalJson.Data.plans[PlanId].pins[pinId].anchor;
        Point _originPos = GlobalJson.Data.plans[PlanId].pins[pinId].pos;
        Size _planSize = GlobalJson.Data.plans[PlanId].imageSize;
        Size _pinSize = GlobalJson.Data.plans[PlanId].pins[pinId].size;

        // berechne Anchor-Koordinaten
        var smallImage = new MR.Gestures.Image
        {
            Source = pinIcon,
            AutomationId = pinId,
            WidthRequest = GlobalJson.Data.plans[PlanId].pins[pinId].size.Width,
            HeightRequest = GlobalJson.Data.plans[PlanId].pins[pinId].size.Height,
            AnchorX = GlobalJson.Data.plans[PlanId].pins[pinId].anchor.X,
            AnchorY = GlobalJson.Data.plans[PlanId].pins[pinId].anchor.Y,
            TranslationX = (_planSize.Width * _originPos.X / densityX) - (_originAnchor.X * _pinSize.Width),
            TranslationY = (_planSize.Height * _originPos.Y / densityY) - (_originAnchor.Y * _pinSize.Height),
        };

        var scaleLimit = Convert.ToDouble(SettingsService.Instance.PinScaleLimit);
        if (scaleLimit < 1) smallImage.Scale = scaleLimit;

        //smallImage.BindingContext = SettingsService.Instance;
        //smallImage.SetBinding(ScaleProperty, new Binding("PinSize"));

        smallImage.Down += (s, e) =>
        {
            if (GlobalJson.Data.plans[PlanId].pins[pinId].isLocked == true) return;
            planPanContainer.IsPanningEnabled = false;
            activePin = smallImage;
        };

        smallImage.Up += (s, e) =>
        {
            if (GlobalJson.Data.plans[PlanId].pins[pinId].isLocked == true) return;
            planPanContainer.IsPanningEnabled = true;
            activePin = null;

            var x = smallImage.TranslationX / GlobalJson.Data.plans[PlanId].imageSize.Width * densityX;
            var y = smallImage.TranslationY / GlobalJson.Data.plans[PlanId].imageSize.Height * densityY;

            var _pin = (s as MR.Gestures.Image);
            var dx = (_pin.AnchorX * _pin.Width) / GlobalJson.Data.plans[PlanId].imageSize.Width * densityX;
            var dy = (_pin.AnchorY * _pin.Height) / GlobalJson.Data.plans[PlanId].imageSize.Height * densityY;

            GlobalJson.Data.plans[PlanId].pins[pinId].pos = new Point(x + dx, y + dy);
            GlobalJson.SaveToFile();
        };

        smallImage.Tapping += async (s, e) =>
        {
            await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={pinId}&pinIcon={pinIcon}");
        };

        PlanContainer.Children.Add(smallImage);

        // set transparency
        if (GlobalJson.Data.plans[PlanId].pins[pinId].isLocked == true)
            smallImage.Opacity = .3;
        else
            smallImage.Opacity = 1;
    }

    private void AdjustImagePosition(MR.Gestures.Image image)
    {
        // Hole die Ankerpunkte, Positionen und Bildgröße wie in deinem Loaded-Handler
        Point _originAnchor = GlobalJson.Data.plans[PlanId].pins[image.AutomationId].anchor;
        Point _originPos = GlobalJson.Data.plans[PlanId].pins[image.AutomationId].pos;
        Size _planSize = GlobalJson.Data.plans[PlanId].imageSize;
        Size _pinSize = GlobalJson.Data.plans[PlanId].pins[image.AutomationId].size;

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
                    densityX = GlobalJson.Data.plans[PlanId].imageSize.Width / PlanImage.Width;
                    densityY = GlobalJson.Data.plans[PlanId].imageSize.Height / PlanImage.Height;

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
        var planPanContainer = (TransformViewModel)PlanContainer.BindingContext;
        planPanContainer.IsPanningEnabled = true;
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
            var pos = new Point(0.5, 0.5);

            Pin newPinData = new()
            {
                pos = pos,
                anchor = Settings.pinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).anchor,
                size = Settings.pinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).size,
                isLocked = false,
                isLockRotate = false,
                infoTxt = "",
                pinTxt = Settings.pinData.FirstOrDefault(item => item.fileName.Equals(newPin, StringComparison.OrdinalIgnoreCase)).imageName,
                pinIcon = newPin,
                images = []
            };

            // Sicherstellen, dass der Plan existiert
            if (GlobalJson.Data.plans.TryGetValue(PlanId, out Plan value))
            {
                var plan = value;

                // Überprüfen, ob die Pins-Struktur initialisiert ist
                plan.pins ??= [];

                // Neuen Pin hinzufügen
                plan.pins[currentDateTime] = newPinData;

                // save data to file
                GlobalJson.SaveToFile();
            }
            else
                Console.WriteLine($"Plan mit ID {PlanId} existiert nicht.");

            AddPin(currentDateTime, newPinData.pinIcon);
        };
    }

    private async void OnEditButtonClicked(object sender, EventArgs e)
    {
        var popup1 = new PopupEditPlan(GlobalJson.Data.plans[PlanId].name);
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
                    var shellItem = (Application.Current.MainPage as AppShell).Items.FirstOrDefault(i => i.AutomationId == PlanId);
                    if (shellItem != null)
                        (Application.Current.MainPage as AppShell).Items.Remove(shellItem);

                    string file = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.planPath, GlobalJson.Data.plans[PlanId].file);
                    if (File.Exists(file))
                        File.Delete(file);

                    GlobalJson.Data.plans.Remove(PlanId);

                    // save data to file
                    GlobalJson.SaveToFile();
                }
                break;

            default:
                // ändert das dazugehörige Menü-Item
                (Application.Current.MainPage as AppShell).Items.FirstOrDefault(i => i.AutomationId == PlanId).Title = result;

                // ändert Titel vom View
                Title = result;

                GlobalJson.Data.plans[PlanId].name = result;

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