#nullable disable

using CommunityToolkit.Maui.Extensions;
using DocumentFormat.OpenXml.Wordprocessing;
using SnapDoc.Services;
using SnapDoc.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc;

public partial class AppShell : Shell
{
    public List<PlanItem> AllPlanItems { get; set; }
    public ObservableCollection<PlanItem> PlanItems { get; set; }

    private bool _isActiveToggle;
    public bool IsActiveToggle
    {
        get => _isActiveToggle;
        set
        {
            if (_isActiveToggle == value)
                return;

            _isActiveToggle = value;
            OnPropertyChanged();
            ApplyFilterAndSorting();
        }
    }

    private PlanItem _selectedPlanItem;
    public PlanItem SelectedPlanItem
    {
        get => _selectedPlanItem;
        set
        {
            if (_selectedPlanItem != value)
            {
                _selectedPlanItem?.IsSelected = false;
                _selectedPlanItem = value;
                _selectedPlanItem?.IsSelected = true;
            }
        }
    }

    private string _infoText = "Pläne umsortieren: Gedrückt halten und ziehen";
    public string InfoText
    {
        get => _infoText;
        set
        {
            _infoText = value;
            OnPropertyChanged();
        }
    }

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("open_project", typeof(OpenProject));
        Routing.RegisterRoute("icongallery", typeof(IconGallery));
        Routing.RegisterRoute("fotogallery", typeof(FotoGalleryView));
        Routing.RegisterRoute("setpin", typeof(SetPin));
        Routing.RegisterRoute("imageview", typeof(ImageViewPage));
        Routing.RegisterRoute("project_details", typeof(ProjectDetails));
        Routing.RegisterRoute("loadPdfImages", typeof(LoadPDFPages));
        Routing.RegisterRoute("pinList", typeof(PinList));
        Routing.RegisterRoute("exportSettings", typeof(ExportSettings));
        Routing.RegisterRoute("mapview", typeof(MapView));
        Routing.RegisterRoute("xmleditor", typeof(EditorView));

        AllPlanItems = [];
        PlanItems = [];

        PlanCollectionView.ItemsSource = PlanItems;

        BindingContext = this;

        SettingsService.Instance.PropertyChanged += OnSettingsChanged;

        ApplyPlanTemplate();
    }

    private void ApplyPlanTemplate()
    {
        if (PlanCollectionView == null)
            return;

        PlanCollectionView.ItemTemplate =
            SettingsService.Instance.IsPlanListThumbnails
                ? (DataTemplate)Resources["PlanThumbnailTemplate"]
                : (DataTemplate)Resources["PlanListTemplate"];
    }

    public void ApplyFilterAndSorting()
    {
        if (AllPlanItems == null)
            return;

        var filtered = AllPlanItems
            .Where(p => !SettingsService.Instance.IsHideInactivePlans || p.AllowExport)
            .ToList();

        PlanItems.Clear();
        foreach (var item in filtered)
            PlanItems.Add(item);

        if (SettingsService.Instance.IsHideInactivePlans)
            InfoText = $"{AllPlanItems.Count - PlanItems.Count} ausgeblendete Pläne";
        else
            InfoText = "Pläne umsortieren: Gedrückt halten und ziehen";
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsPlanListThumbnails))
            MainThread.BeginInvokeOnMainThread(ApplyPlanTemplate);

        if (e.PropertyName == nameof(SettingsService.IsHideInactivePlans))
            MainThread.BeginInvokeOnMainThread(ApplyFilterAndSorting);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupSettings();
        await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
    }

    public void OnTitleClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.IsProjectLoaded)
        {
            var projectDetails = new ProjectDetails();
            projectDetails.OnTitleCaptureClicked(null, null);
        }
    }

    private async void OnNavigateTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
        {
            var parameter = tap.CommandParameter?.ToString();
            if (!string.IsNullOrWhiteSpace(parameter))
            {
#if WINDOWS     
                Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID     
                Shell.Current.FlyoutIsPresented = false;
#endif
                // vermeide Navigation auf bestimmte Seiten wenn keine Pläne vorhanden sind
                if (GlobalJson.Data.Plans == null && (parameter == "exportSettings" ||
                                                        parameter == "pinList" ||
                                                        parameter == "mapview" ||
                                                        parameter == "fotogallery"))
                {
                    var popup = new PopupAlert("Es sind noch keine Pläne vorhanden. Importieren zuerst eine oder mehrere PDF-Seiten in der Projektverwaltung.");
                    await this.ShowPopupAsync(popup, Settings.PopupOptions);
                    return;
                }

                await Shell.Current.GoToAsync(parameter);
            }
        }
    }

    private async void OnPlanTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
        {
            var parameter = tap.CommandParameter?.ToString();
            if (!string.IsNullOrWhiteSpace(parameter))
            {
#if WINDOWS     
                Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID     
                Shell.Current.FlyoutIsPresented = false;
#endif
                await Shell.Current.GoToAsync($"//{parameter}");
            }
        }
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        var button = sender as Label;

        PlanItem item = (PlanItem)button.BindingContext;
        if (item == null)
            return;

        item.AllowExport = !item.AllowExport;

        // save data to file
        GlobalJson.SaveToFile();

        // Neu filtern und anzeigen, falls HideInactivePlans aktiv ist
        if (SettingsService.Instance.IsHideInactivePlans)
            ApplyFilterAndSorting();
    }

    private static void UpdatePlansOrder(List<string> updatedPlanOrder)
    {
        var plansList = GlobalJson.Data.Plans.ToList();
        var reorderedPlans = updatedPlanOrder.Select(planRoute =>
        {
            return plansList.FirstOrDefault(p => p.Key == planRoute);
        }).Where(p => p.Key != null).ToList();

        GlobalJson.Data.Plans = reorderedPlans.ToDictionary(p => p.Key, p => p.Value);
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView)?.ItemsSource is not ObservableCollection<PlanItem> reorderedItems)
            return;

        var orderedIds = reorderedItems.Select(p => p.PlanId).ToList();

        // Reorder AllPlanItems anhand der Masterliste, nicht der gefilterten Liste
        AllPlanItems =
        [
            .. orderedIds.Select(id => AllPlanItems.First(p => p.PlanId == id)),
            .. AllPlanItems.Where(p => !orderedIds.Contains(p.PlanId)),
        ];

        UpdatePlansOrder(orderedIds); // Reihenfolge in JSON aktualisieren

        ApplyFilterAndSorting(); // Filter wieder anwenden

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnPlanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection != null && e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is PlanItem selected)
        {
            SelectedPlanItem = selected;
        }
    }

    public void HighlightCurrentPlan(string planId)
    {
        if (PlanItems == null || PlanCollectionView == null)
            return;

        var selected = PlanItems.FirstOrDefault(p => p.PlanId == planId);
        if (selected != null)
            PlanCollectionView.SelectedItem = selected;
    }
}
