#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Controls;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.Views;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SnapDoc;

public partial class AppShell : Shell
{
    private const uint AddPlanAnimationMs = 120;
    private const double AddPlanRowHeight = 44;

    private readonly AuthService _authService = new();
    private static ProjectItem Project => ProjectItem.Current;

    private bool _isAddPlanMenuOpen;
    private INotifyCollectionChanged _observedPlanItems;

    private PlanItem _selectedPlanItem;
    public PlanItem SelectedPlanItem
    {
        get => _selectedPlanItem;
        set
        {
            if (_selectedPlanItem == value) return;

            _selectedPlanItem?.IsSelected = false;
            _selectedPlanItem = value;
            _selectedPlanItem?.IsSelected = true;

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
        Routing.RegisterRoute("cameraPage", typeof(CameraPage));
        Routing.RegisterRoute("generalmapview", typeof(MapView));
        Routing.RegisterRoute("cloudPickerPage", typeof(CloudPickerPage));

        BindingContext = Project;

        SettingsService.Instance.PropertyChanged += OnSettingsChanged;
        PropertyChanged += OnAppShellPropertyChanged;

        // Planliste neu vermessen, wenn Plaene hinzukommen/wegfallen oder die Liste ersetzt wird
        PlanCollectionView.PropertyChanged += OnPlanCollectionViewPropertyChanged;
        ObservePlanItems(PlanCollectionView.ItemsSource);

        ApplyPlanTemplate();
        Project.ReloadPlansFromData();
    }

    // ---------------------------------------------------------------
    // Shell-eigene UI-Logik
    // ---------------------------------------------------------------
    private void OnAppShellPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FlyoutIsPresented)) return;

        if (!FlyoutIsPresented)
        {
            SetAddPlanMenuImmediately(false);
            WeakReferenceMessenger.Default.Send(new ResetTouchesMessage());
            return;
        }

        // Leeres Projekt: Planquellen direkt anbieten
        if (!Project.HasPlans && SettingsService.Instance.IsProjectLoaded)
            SetAddPlanMenuImmediately(true);
    }

    private void ApplyPlanTemplate()
    {
        if (PlanCollectionView == null) return;

        PlanCollectionView.ItemTemplate =
            SettingsService.Instance.IsPlanListThumbnails
                ? (DataTemplate)Resources["PlanThumbnailTemplate"]
                : (DataTemplate)Resources["PlanListTemplate"];

        RefreshPlanListHeight();
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsPlanListThumbnails))
            MainThread.BeginInvokeOnMainThread(ApplyPlanTemplate);

        if (e.PropertyName == nameof(SettingsService.IsHideInactivePlans))
            MainThread.BeginInvokeOnMainThread(Project.ApplyFilterAndSorting);

        if (e.PropertyName == nameof(SettingsService.IsProjectLoaded))
            MainThread.BeginInvokeOnMainThread(UpdatePlanListMaxHeight);
    }

    public void HighlightCurrentPlan(string planId)
    {
        if (PlanCollectionView == null) return;

        var selected = Project.PlanItems.FirstOrDefault(p => p.PlanId == planId);
        if (selected != null)
            PlanCollectionView.SelectedItem = selected;
    }

    private static void CloseFlyoutOnMobile()
    {
#if WINDOWS
        Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = false;
#endif
    }

    // ---------------------------------------------------------------
    // Hoehe der Planliste
    // Die Liste waechst mit ihrem Inhalt, "Plan hinzufuegen" haengt direkt darunter.
    // Erst wenn der Platz nicht mehr reicht, wird sie begrenzt und scrollt.
    // ---------------------------------------------------------------
    private void OnPlanAreaSizeChanged(object sender, EventArgs e)
        => UpdatePlanListMaxHeight();

    private void UpdatePlanListMaxHeight()
    {
        if (PlanArea == null || PlanCollectionView == null || PlanArea.Height <= 0) return;

        double addRow = AddPlanRow.IsVisible ? AddPlanRowHeight : 0;
        double available = PlanArea.Height - PlanArea.Padding.VerticalThickness - addRow;

        PlanCollectionView.MaximumHeightRequest = Math.Max(0, available);
    }

    private void OnPlanCollectionViewPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemsView.ItemsSource))
            ObservePlanItems(PlanCollectionView.ItemsSource);
    }

    private void ObservePlanItems(object itemsSource)
    {
        _observedPlanItems?.CollectionChanged -= OnPlanItemsCollectionChanged;
        _observedPlanItems = itemsSource as INotifyCollectionChanged;
        _observedPlanItems?.CollectionChanged += OnPlanItemsCollectionChanged;

        RefreshPlanListHeight();
    }

    private void OnPlanItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        => RefreshPlanListHeight();

    /// <summary>
    /// Die CollectionView meldet Inhaltsaenderungen nicht immer an das Layout weiter.
    /// Deshalb wird nach Aenderungen explizit neu vermessen.
    /// </summary>
    private void RefreshPlanListHeight()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (PlanCollectionView == null) return;
            UpdatePlanListMaxHeight();
            ((IView)PlanCollectionView).InvalidateMeasure();
        });
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

        CloseFlyoutOnMobile();

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

        CloseFlyoutOnMobile();

        await Shell.Current.GoToAsync($"//{parameter}");
    }

    // ---------------------------------------------------------------
    // Plan hinzufuegen (unter der Planliste)
    // ---------------------------------------------------------------
    private async void OnAddPlanTapped(object sender, TappedEventArgs e)
        => await SetAddPlanMenuAsync(!_isAddPlanMenuOpen);

    private async void OnAddPdfTapped(object sender, TappedEventArgs e)
    {
        SetAddPlanMenuImmediately(false);
        CloseFlyoutOnMobile();
        await Shell.Current.GoToAsync("loadPdfImages");
    }

    private async void OnAddWebMapTapped(object sender, TappedEventArgs e)
    {
        SetAddPlanMenuImmediately(false);
        await AddWebMapPlanAsync();
    }

    /// <summary>
    /// Wechselt animiert zwischen "+ Plan hinzufuegen" und den beiden Planquellen.
    /// Die Zeilenhoehe bleibt konstant (44).
    /// </summary>
    private async Task SetAddPlanMenuAsync(bool open)
    {
        if (_isAddPlanMenuOpen == open) return;
        _isAddPlanMenuOpen = open;

        var show = open ? AddPlanOptions : AddPlanCollapsed;
        var hide = open ? AddPlanCollapsed : AddPlanOptions;

        show.CancelAnimations();
        hide.CancelAnimations();

        await hide.FadeToAsync(0, AddPlanAnimationMs);
        if (_isAddPlanMenuOpen != open) return; // inzwischen erneut umgeschaltet
        hide.IsVisible = false;

        show.Opacity = 0;
        show.IsVisible = true;
        await show.FadeToAsync(1, AddPlanAnimationMs);
    }

    private void SetAddPlanMenuImmediately(bool open)
    {
        if (AddPlanOptions == null || AddPlanCollapsed == null) return;

        _isAddPlanMenuOpen = open;

        AddPlanOptions.CancelAnimations();
        AddPlanCollapsed.CancelAnimations();

        AddPlanOptions.IsVisible = open;
        AddPlanOptions.Opacity = open ? 1 : 0;
        AddPlanCollapsed.IsVisible = !open;
        AddPlanCollapsed.Opacity = open ? 0 : 1;
    }

    private async Task AddWebMapPlanAsync()
    {
        var popup = new PopupEntry(header: AppResources.karte_aus_webmap,
                                   desc: AppResources.online_map_requirement_hint + ".",
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        string planId = "webmap_" + SyncClock.NewId();
        Plan plan = new()
        {
            Name = result.Result == "" ? "Online Map" : result.Result,
            File = "",
            ImageSize = new Size(0, 0),
            IsGrayscale = false,
            Description = "",
            AllowExport = true,
            PlanColor = "#00FFFFFF"
        };
        plan.Touch();

        var newPlan = new KeyValuePair<string, Plan>(planId, plan);
        LoadDataToView.AddPlan(newPlan);

        GlobalJson.Data.Plans ??= [];
        GlobalJson.Data.Plans[planId] = plan;

        SaveManager.NotifyDataChanged();
        Project.ApplyFilterAndSorting();

        CloseFlyoutOnMobile();
        await Shell.Current.GoToAsync($"//{planId}");
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