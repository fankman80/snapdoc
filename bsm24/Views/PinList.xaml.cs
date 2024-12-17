#nullable disable

using bsm24.Models;
using Mopups.Services;
using System.ComponentModel;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class PinList : UraniumContentPage
{
    public Command<IconItem> IconTappedCommand { get; }
    public int DynamicSpan { get; set; } = 1;
    public int MinSize { get; set; } = 1;

    public PinList()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        BindingContext = this;

        LoadPins();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void LoadPins()
    {
        int pincounter = 0;

        List<Pin> pinItems = [];
        foreach (var plan in GlobalJson.Data.Plans)
        {
            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
            {
                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                {
                    var newPin = new Pin
                    {
                        PinDesc = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc,
                        PinIcon = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon,
                        PinName = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName,
                        PinLocation = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation,
                        OnPlanName = GlobalJson.Data.Plans[plan.Key].Name,
                        OnPlanId = plan.Key,
                        SelfId = pin.Key,
                        AllowExport = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport,
                    };

                    newPin.PropertyChanged += Pin_PropertyChanged;
                    pinItems.Add(newPin);
                    pincounter++;
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

        // Entferne die aktuelle Seite aus dem Stack
        var currentPage = Shell.Current.CurrentPage;
        Shell.Current.Navigation.RemovePage(currentPage);

        await Shell.Current.Navigation.PushAsync(newPage);
    }

    private async void UpdateSpan()
    {
        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Icons werden geladen...";

        await Task.Run(() =>
        {
            OnPropertyChanged(nameof(DynamicSpan));
        });

        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;
    }
}
