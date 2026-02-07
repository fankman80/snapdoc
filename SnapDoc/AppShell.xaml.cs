#nullable disable

using CommunityToolkit.Maui.Extensions;
using SnapDoc.Services;
using SnapDoc.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SnapDoc.Resources.Languages;

namespace SnapDoc;

public partial class AppShell : Shell
{
    public ObservableCollection<PlanItem> AllPlanItems { get; set; }
    public ObservableCollection<PlanItem> PlanItems { get; set; }

    private bool _showAddPdfButton = false;
    public bool ShowAddPdfButton
    {
        get => _showAddPdfButton;
        set { _showAddPdfButton = value; OnPropertyChanged(); }
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

    private string _infoText = AppResources.kein_projekt_geladen;
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
        Routing.RegisterRoute("mapviewosm", typeof(MapViewOSM));
        Routing.RegisterRoute("xmleditor", typeof(EditorView));

        AllPlanItems = [];
        PlanItems = [];
        PlanCollectionView.ItemsSource = PlanItems;

        BindingContext = this;

        SettingsService.Instance.PropertyChanged += OnSettingsChanged;
        AllPlanItems.CollectionChanged += (s, e) => UpdateButtonVisibility();

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

        if (!SettingsService.Instance.IsProjectLoaded)
            InfoText = AppResources.kein_projekt_geladen;
        else if (AllPlanItems == null || AllPlanItems.Count == 0)
            InfoText = AppResources.keine_pdf_seiten;
        else if (SettingsService.Instance.IsHideInactivePlans && (AllPlanItems != null || AllPlanItems.Count > 0))
            InfoText = $"{AppResources.ausgeblendete_plaene}: {AllPlanItems.Count - PlanItems.Count}";
        else
            InfoText = AppResources.plaene_umsortieren_gedrueckt_halten_und_ziehen;
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

    private void OnTitleClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.IsProjectLoaded)
        {
            var projectDetails = new ProjectDetails();
            projectDetails.OnTitleCaptureClicked(null, null);
        }
    }

    private async void OnAddPdfClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("loadPdfImages");
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
                                                        parameter == "mapviewosm" ||
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
            SelectedPlanItem = selected;
    }

    public void HighlightCurrentPlan(string planId)
    {
        if (PlanItems == null || PlanCollectionView == null)
            return;

        var selected = PlanItems.FirstOrDefault(p => p.PlanId == planId);
        if (selected != null)
            PlanCollectionView.SelectedItem = selected;
    }

    private void UpdateButtonVisibility()
    {
        bool isProjectLoaded = SettingsService.Instance.IsProjectLoaded;
        bool isListEmpty = (AllPlanItems == null || AllPlanItems.Count == 0);

        ShowAddPdfButton = isProjectLoaded && isListEmpty;
    }
}
