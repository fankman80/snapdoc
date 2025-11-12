#nullable disable

using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc.Views;

public partial class PinList : ContentPage
{
    public Command<IconItem> IconTappedCommand { get; }
    private List<PinItem> pinItems = [];
    private List<PinItem> originalPinItems = []; // Originalreihenfolge speichern
    private string OrderDirection = "asc";
    private ObservableCollection<PinItem> filteredPinItems;

    public PinList()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadPins();
        SortPicker.SelectedIndexChanged += OnSortPickerChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SortPicker.SelectedIndexChanged -= OnSortPickerChanged;
    }

    private void LoadPins()
    {
        int pincounter = 0;
        pinListView.ItemsSource = null;
        pinItems.Clear();
        bool saveRequested = false;

        foreach (var plan in GlobalJson.Data.Plans)
        {
            if (plan.Value.Pins != null)
            {
                foreach (var pin in plan.Value.Pins.Values)
                {
                    if (!pin.IsCustomPin)
                    {
                        var pinIcon = pin.PinIcon;

                        if (pinIcon.StartsWith("customicons", StringComparison.OrdinalIgnoreCase))
                        {
                            var _pinIcon = Path.Combine(Settings.DataDirectory, "customicons", Path.GetFileName(pinIcon));
                            if (File.Exists(_pinIcon))
                                pin.PinIcon = _pinIcon;
                        }

                        var newPin = new PinItem(pin);
                        pinItems.Add(newPin);
                        pincounter++;
                    }
                }
            }
        }

        if (saveRequested)
            GlobalJson.SaveToFile();

        originalPinItems = [.. pinItems];
        filteredPinItems = [.. pinItems];

        pinListView.ItemsSource = pinItems;
        PinCounterLabel.Text = $"Pins: {pinItems.Count}";

        IconSorting(OrderDirection);

        if (!string.IsNullOrEmpty(SearchEntry.Text))
            SearchEntry_TextChanged(null, new TextChangedEventArgs(SearchEntry.Text, SearchEntry.Text));
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

        await Shell.Current.GoToAsync($"///{planId}?pinZoom={pinId}");
    }

    private async void OnPinIconClicked(object sender, EventArgs e)
    {
        var button = sender as Image;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        await Shell.Current.GoToAsync($"icongallery?planId={planId}&pinId={pinId}&sender=pinList");
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        await Shell.Current.GoToAsync($"setpin?planId={planId}&pinId={pinId}&sender=pinList");
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        var button = sender as Button;

        PinItem item = (PinItem)button.BindingContext;

        if (item != null)
        {
            item.AllowExport = !item.AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        IconSorting(OrderDirection);
        SettingsService.Instance.SaveSettings();
    }

    private void OnSortDirectionClicked(object sender, EventArgs e)
    {

        if (OrderDirection == "asc")
        {
            SortDirection.ScaleY *= -1;
            OrderDirection = "desc";
            IconSorting("desc");
        }
        else
        {
            SortDirection.ScaleY *= -1;
            OrderDirection = "asc";
            IconSorting("asc");
        }
    }

    private void IconSorting(string order)
    {
        if (SortPicker.SelectedItem == null) return;

        // Setze die Liste auf die ursprüngliche Reihenfolge zurück, bevor sortiert wird
        if (string.IsNullOrWhiteSpace(SearchEntry.Text))
            pinItems = [.. originalPinItems];
        else
            pinItems = [.. filteredPinItems];

        SettingsService.Instance.PinSortCrit = SortPicker.SelectedItem.ToString();

        var selectedOption = SortPicker.SelectedItem.ToString();

        if (order == "asc") // Sortiere aufsteigend
        {
            switch (SettingsService.Instance.PinSortCrit)
            {
                case var crit when crit == SettingsService.Instance.PinSortCrits[0]:
                    pinItems = [.. pinItems.OrderBy(pin => pin.OnPlanId).ToList()];
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
                    pinItems = [.. pinItems.OrderBy(pin => pin.AllowExport).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[5]:
                    pinItems = [.. pinItems.OrderBy(pin => pin.Time).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[6]:
                    pinItems = [.. pinItems.OrderBy(pin => pin.PinPriority).ToList()];
                    break;
            }
        }
        else // Sortiere absteigend
        {
            switch (SettingsService.Instance.PinSortCrit)
            {
                case var crit when crit == SettingsService.Instance.PinSortCrits[0]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.OnPlanId).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[1]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.PinIcon).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[2]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.PinLocation).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[3]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.PinName).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[4]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.AllowExport).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[5]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.Time).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.PinSortCrits[6]:
                    pinItems = [.. pinItems.OrderByDescending(pin => pin.PinPriority).ToList()];
                    break;
            }
        }

        pinListView.ItemsSource = null;
        pinListView.ItemsSource = pinItems;
    }

    private void SearchEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        string searchText = e.NewTextValue?.ToLower() ?? string.Empty;

        filteredPinItems.Clear();
        foreach (var pin in pinItems)
        {
            if (
                pin.OnPlanId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                pin.PinLocation.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                pin.PinName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                pin.PinDesc.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            )
            {
                filteredPinItems.Add(pin);
                continue;
            }
        }

        // Verwende Originalliste falls die Suche leer ist
        if (string.IsNullOrWhiteSpace(SearchEntry.Text))
            filteredPinItems = [.. originalPinItems];

        pinListView.ItemsSource = filteredPinItems;
        PinCounterLabel.Text = $"Pins: {filteredPinItems.Count}";
    }
}
