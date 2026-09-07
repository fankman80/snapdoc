#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Controls;
using SnapDoc.Messages;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc;

public partial class AppShell : Shell
{
    private readonly AuthService _authService = new();

    /// <summary>Kurzzugriff auf das Projekt-ViewModel (Datenquelle aller Bindings).</summary>
    private static ProjectItem Project => ProjectItem.Current;

    // Reine UI-Auswahl (Markierung in der Planliste) - bleibt in der Shell.
    private PlanItem _selectedPlanItem;
    public PlanItem SelectedPlanItem
    {
        get => _selectedPlanItem;
        set
        {
            if (_selectedPlanItem == value) return;

            if (_selectedPlanItem != null)
                _selectedPlanItem.IsSelected = false;

            _selectedPlanItem = value;

            if (_selectedPlanItem != null)
                _selectedPlanItem.IsSelected = true;

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
        Routing.RegisterRoute("xmleditor", typeof(EditorView));
        Routing.RegisterRoute("cameraView", typeof(CameraView));
        Routing.RegisterRoute("generalmapview", typeof(MapView));
        Routing.RegisterRoute("cloudPickerPage", typeof(CloudPickerPage));

        // ItemsSource kommt aus der XAML: ItemsSource="{Binding PlanItems}"
        BindingContext = Project;

        SettingsService.Instance.PropertyChanged += OnSettingsChanged;
        PropertyChanged += OnAppShellPropertyChanged;

        ApplyPlanTemplate();

        Project.ReloadPlansFromData();
    }

    // ---------------------------------------------------------------
    // Shell-eigene UI-Logik
    // ---------------------------------------------------------------
    private void OnAppShellPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlyoutIsPresented) && !FlyoutIsPresented)
            WeakReferenceMessenger.Default.Send(new ResetTouchesMessage());
    }

    private void ApplyPlanTemplate()
    {
        if (PlanCollectionView == null) return;

        PlanCollectionView.ItemTemplate =
            SettingsService.Instance.IsPlanListThumbnails
                ? (DataTemplate)Resources["PlanThumbnailTemplate"]
                : (DataTemplate)Resources["PlanListTemplate"];
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsPlanListThumbnails))
            MainThread.BeginInvokeOnMainThread(ApplyPlanTemplate);

        if (e.PropertyName == nameof(SettingsService.IsHideInactivePlans))
            MainThread.BeginInvokeOnMainThread(Project.ApplyFilterAndSorting);
    }

    public void HighlightCurrentPlan(string planId)
    {
        if (PlanCollectionView == null) return;

        var selected = Project.PlanItems.FirstOrDefault(p => p.PlanId == planId);
        if (selected != null)
            PlanCollectionView.SelectedItem = selected;
    }

    // ---------------------------------------------------------------
    // Navigation
    // ---------------------------------------------------------------
    private async void OnNavigateTapped(object sender, EventArgs e)
    {
        if (sender is not Grid ve || ve.GestureRecognizers.FirstOrDefault() is not TapGestureRecognizer tap)
            return;

        var parameter = tap.CommandParameter?.ToString();
        if (string.IsNullOrWhiteSpace(parameter)) return;

#if WINDOWS
        Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = false;
#endif
        // Navigation auf bestimmte Seiten vermeiden, wenn keine Plaene vorhanden sind
        if (!Project.HasPlans && (parameter == "exportSettings" ||
                                  parameter == "pinList" ||
                                  parameter == "mapview" ||
                                  parameter == "fotogallery"))
        {
            await this.ShowPopupAsync(new PopupAlert(AppResources.keine_plaene_vorhanden_importieren, AppResources.hinweis), Settings.PopupOptions);
            return;
        }

        await Shell.Current.GoToAsync(parameter);
    }

    private async void OnPlanTapped(object sender, EventArgs e)
    {
        if (sender is not Grid ve || ve.GestureRecognizers.FirstOrDefault() is not TapGestureRecognizer tap)
            return;

        var parameter = tap.CommandParameter?.ToString();
        if (string.IsNullOrWhiteSpace(parameter)) return;

#if WINDOWS
        Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = false;
#endif
        await Shell.Current.GoToAsync($"//{parameter}");
    }

    private async void OnAddPdfClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("loadPdfImages");
    }

    private void OnTitleClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.IsProjectLoaded)
            WeakReferenceMessenger.Default.Send(new TitleCaptureRequestedMessage());
    }

    // ---------------------------------------------------------------
    // Planliste
    // ---------------------------------------------------------------
    private void OnPlanSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is { Count: > 0 } && e.CurrentSelection[0] is PlanItem selected)
            SelectedPlanItem = selected;
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        if ((sender as Label)?.BindingContext is not PlanItem item) return;

        item.AllowExport = !item.AllowExport;

        // save data to file
        SaveManager.NotifyDataChanged();

        // Neu filtern und anzeigen, falls HideInactivePlans aktiv ist
        if (SettingsService.Instance.IsHideInactivePlans)
            Project.ApplyFilterAndSorting();
    }

    private static void UpdatePlansOrder(List<string> updatedPlanOrder)
    {
        if (GlobalJson.Data?.Plans == null) return;

        var plansList = GlobalJson.Data.Plans.ToList();
        var reorderedPlans = updatedPlanOrder
            .Select(planRoute => plansList.FirstOrDefault(p => p.Key == planRoute))
            .Where(p => p.Key != null)
            .ToList();

        GlobalJson.Data.Plans = reorderedPlans.ToDictionary(p => p.Key, p => p.Value);
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView)?.ItemsSource is not ObservableCollection<PlanItem> reorderedItems)
            return;

        var orderedIds = reorderedItems.Select(p => p.PlanId).ToList();

        // Masterliste in-place umsortieren (Collection darf nicht neu zugewiesen werden)
        var all = Project.AllPlanItems;
        var reordered = orderedIds
            .Select(id => all.First(p => p.PlanId == id))
            .Concat(all.Where(p => !orderedIds.Contains(p.PlanId)))
            .ToList();

        all.Clear();
        foreach (var p in reordered)
            all.Add(p);

        UpdatePlansOrder(orderedIds);        // Reihenfolge in JSON aktualisieren
        Project.ApplyFilterAndSorting();     // Filter wieder anwenden

        // save data to file
        SaveManager.NotifyDataChanged();
    }

    // ---------------------------------------------------------------
    // Footer-Aktionen
    // ---------------------------------------------------------------
    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupSettings();
        await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
    }

    private async void OnGlobalCloudClicked(object sender, EventArgs e)
    {
        if (SaveManager.CurrentAuth != null && SaveManager.CurrentAuth.IsLoggedIn)
        {
            var popup = new PopupDualResponse(AppResources.bereits_verbunden_abmelden, AppResources.abmelden);
            var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
            if (result?.Result is not DualPopupResult.Ok) return;

            // Polling beim Abmelden stoppen
            SaveManager.StopCloudPolling();
            await _authService.LogoutAsync();
            SaveManager.CurrentAuth = null;
            SettingsService.Instance.RefreshCloudState();
            return;
        }

        var (success, userName, userEmail) = await _authService.LoginAndFetchUserAsync();

        if (success)
        {
            SaveManager.CurrentAuth = _authService;
            SettingsService.Instance.RefreshCloudState();

            // Polling nach erfolgreichem Login starten
            SaveManager.StartCloudPolling(SettingsService.Instance.CloudPollingIntervall);

            await SnackbarExtensions.ShowSafeAsync($"{AppResources.eingeloggt_als}:\n{userName}\n{userEmail}", includeDelay: true);
        }
        else if (userName != nameof(DualPopupResult.Cancel))
        {
            // Nur bei echten Fehlern, nicht beim Abbruch durch den Nutzer
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.login_fehlgeschlagen}:\n{userName}\n{userEmail}", AppResources.fehler), Settings.PopupOptions);
        }
    }
}