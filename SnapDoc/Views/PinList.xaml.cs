#nullable disable

using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc.Views;

public partial class PinList : ContentPage
{
    public Command<IconItem> IconTappedCommand { get; }
    public ObservableCollection<PinItem> DisplayPins { get; } = [];
    private readonly List<PinItem> _allPins = [];
    private string OrderDirection = "asc";

    public PinList()
    {
        InitializeComponent();
        pinListView.ItemsSource = DisplayPins;
        BindingContext = this;
    }

    protected override async void OnAppearing()
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
        _allPins.Clear();
        foreach (var plan in GlobalJson.Data.Plans.Values)
        {
            if (plan.Pins != null)
            {
                foreach (var pin in plan.Pins.Values)
                {
                    _allPins.Add(new PinItem(pin));
                }
            }
        }
        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        if (SortPicker.SelectedItem == null)
            return;

        var selectedCrit = SortPicker.SelectedItem.ToString();
        SettingsService.Instance.PinSortCrit = selectedCrit;

        var searchText = SearchEntry.Text?.ToLower() ?? string.Empty;
        var query = _allPins.Where(pin =>
            string.IsNullOrWhiteSpace(searchText) ||
            (pin.OnPlanId?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (pin.PinLocation?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (pin.PinName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (pin.PinDesc?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
        );

        object keySelector(PinItem pin)
        {
            var crits = SettingsService.Instance.PinSortCrits;
            if (selectedCrit == crits[0]) return pin.OnPlanId;
            if (selectedCrit == crits[1]) return pin.PinIcon;
            if (selectedCrit == crits[2]) return pin.PinLocation;
            if (selectedCrit == crits[3]) return pin.PinName;
            if (selectedCrit == crits[4]) return pin.IsAllowExport;
            if (selectedCrit == crits[5]) return pin.Time;
            if (selectedCrit == crits[6]) return pin.PinPriority;
            return pin.Time; // Default
        }

        var sorted = OrderDirection == "asc"
            ? [.. query.OrderBy(keySelector)]
            : query.OrderByDescending(keySelector).ToList();

        DisplayPins.Clear();
        foreach (var item in sorted)
        {
            DisplayPins.Add(item);
        }

        PinCounterLabel.Text = $"{AppResources.pins}: {DisplayPins.Count}";
    }

    private void Pin_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PinItem.IsAllowExport))
        {
            var pinItem = (PinItem)sender;

            GlobalJson.Data.Plans[pinItem.OnPlanId].Pins[pinItem.SelfId].IsAllowExport = pinItem.IsAllowExport;

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

        await Shell.Current.GoToAsync($"icongallery?planId={planId}&pinId={pinId}");
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        string planId = button.AutomationId;
        string pinId = button.ClassId;

        await Shell.Current.GoToAsync($"setpin?planId={planId}&pinId={pinId}");
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        var button = sender as Button;

        PinItem item = (PinItem)button.BindingContext;

        if (item != null)
        {
            item.IsAllowExport = !item.IsAllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        ApplyFilterAndSort();
        SettingsService.Instance.SaveSettings();
    }

    private void OnSortDirectionClicked(object sender, EventArgs e)
    {
        OrderDirection = (OrderDirection == "asc") ? "desc" : "asc";
        SortDirection.ScaleY *= -1;
        ApplyFilterAndSort();
    }

    private void SearchEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilterAndSort();
    }
}
