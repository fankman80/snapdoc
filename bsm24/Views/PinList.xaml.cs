#nullable disable

using bsm24.Models;
using System.ComponentModel;
using System.Threading.Tasks;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class PinList : UraniumContentPage
{
    public Command<IconItem> IconTappedCommand { get; }
    public int DynamicSpan = 1;
    public int MinSize = 1;

    public PinList()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        BindingContext = this;
        LoadPins();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadPins();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void LoadPins()
    {
        int pincounter = 0;
        List<PinItem> pinItems = [];
        pinListView.ItemsSource = null;

        foreach (var plan in GlobalJson.Data.Plans)
        {
            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
            {
                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                {
                    if (!GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsCustomPin)
                    {
                        var pinIcon = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon;
                        if (pinIcon.Contains("customicons", StringComparison.OrdinalIgnoreCase))
                            pinIcon = Path.Combine(FileSystem.AppDataDirectory, pinIcon);
                        var newPin = new PinItem
                        {
                            PinDesc = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc,
                            PinIcon = pinIcon,
                            PinName = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName,
                            PinLocation = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation,
                            OnPlanName = GlobalJson.Data.Plans[plan.Key].Name,
                            OnPlanId = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].OnPlanId,
                            SelfId = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].SelfId,
                            AllowExport = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport,
                        };
                        newPin.PropertyChanged += Pin_PropertyChanged;
                        pinItems.Add(newPin);
                        pincounter++;
                    }
                }     
            }
            pinListView.ItemsSource = pinItems;
        }
        pinListView.Footer = "Pins: " + pincounter;
    }

    private void Pin_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Pin.AllowExport))
        {
            var pin = (Pin)sender;

            GlobalJson.Data.Plans[pin.OnPlanId].Pins[pin.SelfId].AllowExport = pin.AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnPinClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        var newPage = new Views.NewPage(planId, pinId)
        {
            Title = GlobalJson.Data.Plans[planId].Name,
            AutomationId = planId
        };

        // Methode zur Vermeidung eines zu grossen Navigations-Stacks
        var navigationStack = Shell.Current.Navigation.NavigationStack;
        Page existingPage = null;
        for (int i = 0; i < navigationStack.Count; i++)
        {
            var page = navigationStack[i];
            if (page != null && page.AutomationId == planId)
            {
                existingPage = page;
                break;
            }
        }
        if (existingPage != null)
        {
            while (navigationStack[navigationStack.Count - 1] != existingPage)
                await Shell.Current.Navigation.PopAsync(false);
        }
        else
            await Shell.Current.Navigation.PushAsync(newPage);
    }

    private async void UpdateSpan()
    {
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "Icons werden geladen...";

        await Task.Run(() =>
        {
            OnPropertyChanged(nameof(DynamicSpan));
        });

        busyOverlay.IsActivityRunning = false;
        busyOverlay.IsOverlayVisible = false;
    }
}
