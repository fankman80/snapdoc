#nullable disable

using bsm24.Services;
using System.ComponentModel;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class PinList : UraniumContentPage
{
    public Command<IconItem> IconTappedCommand { get; }
    private List<PinItem> pinItems = [];
    private List<PinItem> originalPinItems = []; // Originalreihenfolge speichern
    private object previousSelectedItem;

    public PinList()
    {
        InitializeComponent();
        BindingContext = this;
        LoadPins();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SortPicker.PropertyChanged += OnSortPickerChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SortPicker.PropertyChanged -= OnSortPickerChanged;
    }

    private void LoadPins()
    {
        int pincounter = 0;
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
                            Time = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].DateTime
                        };
                        newPin.PropertyChanged += Pin_PropertyChanged;
                        pinItems.Add(newPin);
                        pincounter++;
                    }
                }     
            }
        }

        originalPinItems = [.. pinItems];

        pinListView.ItemsSource = pinItems;
        pinListView.Footer = "Pins: " + pincounter;

        IconSorting();
    }

    private void Pin_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinItem.AllowExport))
        {
            var pinItem = (PinItem)sender;

            GlobalJson.Data.Plans[pinItem.OnPlanId].Pins[pinItem.SelfId].AllowExport = pinItem.AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnPinClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        await Shell.Current.GoToAsync($"//{planId}?pinZoom={pinId}");
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        await Shell.Current.GoToAsync($"setpin?planId={planId}&pinId={pinId}");
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        var currentSelectedItem = SortPicker.SelectedItem;
        if (previousSelectedItem != currentSelectedItem)
        {
            previousSelectedItem = currentSelectedItem;
            IconSorting();
            SettingsService.Instance.SaveSettings();
        }
    }

    private void IconSorting()
    {
        if (SortPicker.SelectedItem == null) return;

        // Setze die Liste auf die ursprüngliche Reihenfolge zurück, bevor sortiert wird
        pinItems = [.. originalPinItems];

        SettingsService.Instance.PinSortCrit = SortPicker.SelectedItem.ToString();

        var selectedOption = SortPicker.SelectedItem.ToString();

        switch (SettingsService.Instance.PinSortCrit)
        {
            case var crit when crit == SettingsService.Instance.PinSortCrits[0]:
                pinItems = [.. pinItems.OrderBy(pin => pin.OnPlanName).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.PinSortCrits[1]:
                pinItems = [.. pinItems.OrderBy(pin => pin.PinIcon).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.PinSortCrits[2]:
                pinItems = [.. pinItems.OrderBy(pin => pin.PinLocation).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.PinSortCrits[3]:
                pinItems = [.. pinItems.OrderBy(pin => pin.PinName).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.PinSortCrits[4]:
                pinItems = [.. pinItems.OrderByDescending(pin => pin.AllowExport).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.PinSortCrits[5]:
                pinItems = [.. pinItems.OrderBy(pin => pin.Time).ToList()];
                break;
        }

        pinListView.ItemsSource = null;
        pinListView.ItemsSource = pinItems;
    }
}
