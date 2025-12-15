#nullable disable

using CommunityToolkit.Maui.Extensions;
using SnapDoc.Services;
using SnapDoc.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc;

public partial class AppShell : Shell
{
    public ObservableCollection<PlanItem> PlanItems { get; set; }

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

        PlanItems = [];

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

        RebuildFlyout();
    }

    public void RebuildFlyout()
    {
        try
        {
            // FlyoutContent neu aufbauen
            if (PlanCollectionView != null)
            {
                var items = PlanCollectionView.ItemsSource;
                PlanCollectionView.ItemsSource = null;
                PlanCollectionView.ItemsSource = items;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Flyout rebuild failed: {ex.Message}");
        }
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsPlanListThumbnails))
            MainThread.BeginInvokeOnMainThread(ApplyPlanTemplate);
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

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView).ItemsSource is ObservableCollection<PlanItem> reorderedItems)
        {
            var updatedPlans = reorderedItems.Select(item => item.PlanRoute).ToList();
            AppShell.UpdatePlansOrder(updatedPlans);

            // Speichern
            GlobalJson.SaveToFile();
        }
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

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        var button = sender as Label;

        PlanItem item = (PlanItem)button.BindingContext;

        if (item != null)
        {
            item.AllowExport = !item.AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
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