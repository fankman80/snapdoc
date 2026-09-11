#nullable disable
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SnapDoc;

/// <summary>
/// ViewModel-Wrapper um <see cref="JsonDataModel"/> - das Gegenstueck zu
/// PlanItem / PinItem / FotoItem, aber auf Projektebene.
/// Haelt saemtliche Projektdaten, die Planlisten und die abgeleiteten
/// Anzeigewerte fuer Flyout-Header, Footer und Projektseite.
/// </summary>
public partial class ProjectItem : ObservableObject
{
    private const string DefaultTitleImage = SettingsService.FlyoutHeaderImageThumb;
    private JsonDataModel _model;
    private bool _suspendSave;
    public string CustomIconsFolder => Or(_model.CustomIconsPath, "customicons");

    #region Singleton

    private static ProjectItem _current;

    /// <summary>Das aktuell geladene Projekt - Bindungsziel fuer die gesamte App.</summary>
    public static ProjectItem Current => _current ??= new ProjectItem(GlobalJson.Data);

    #endregion

    #region Konstruktor / Modellbindung

    public ProjectItem(JsonDataModel model)
    {
        InfoText = AppResources.kein_projekt_geladen;

        Attach(model);

        WeakReferenceMessenger.Default.Register<ProjectItem, RemoteDataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == RemoteChangeType.PlanListUpdated)
                MainThread.BeginInvokeOnMainThread(r.ReloadPlansFromData);
            else if (m.Value == RemoteChangeType.ProjectDetailsUpdated)
                MainThread.BeginInvokeOnMainThread(() => r.Attach(GlobalJson.Data));
        });

        WeakReferenceMessenger.Default.Register<ProjectItem, TitleImageChangedMessage>(this, async (r, m) =>
        {
            if (GlobalJson.Data == null) return;

            string projectDir = Path.GetDirectoryName(GlobalJson.GetFilePath());
            if (string.IsNullOrEmpty(projectDir)) return;

            await Helper.UpdateProjectTitleImageAsync(GlobalJson.Data, projectDir, m.OldFileName, m.NewFileName);

            MainThread.BeginInvokeOnMainThread(r.RefreshTitleImage);
        });

        WeakReferenceMessenger.Default.Register<ProjectItem, PinAddedMessage>(this, (r, m) =>
            MainThread.BeginInvokeOnMainThread(() => r.Notify(nameof(PinCountTotal))));

        WeakReferenceMessenger.Default.Register<ProjectItem, PinDeletedMessage>(this, (r, m) =>
            MainThread.BeginInvokeOnMainThread(() => r.Notify(nameof(PinCountTotal))));

        SettingsService.Instance.PropertyChanged += OnSettingsChanged;
    }

    /// <summary>Das darunterliegende Serialisierungs-Modell (fuer GlobalJson/SaveManager).</summary>
    public JsonDataModel Model => _model;

    /// <summary>
    /// Haengt das ViewModel an ein (neu geladenes) Modell. Nach GlobalJson.LoadFromFile()
    /// aufrufen - danach aktualisieren sich alle Bindings automatisch.
    /// </summary>
    public void Attach(JsonDataModel model)
    {
        _model?.PropertyChanged -= OnModelPropertyChanged;
        _model = model ?? new JsonDataModel();
        _model.PropertyChanged += OnModelPropertyChanged;

        RefreshAll();
    }

    private void OnModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // Aenderungen, die woanders direkt am Modell gemacht werden, in die UI durchreichen
        OnPropertyChanged(e.PropertyName);
        RaiseComputed();
    }

    private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsProjectLoaded))
            Notify(nameof(IsLoaded), nameof(ShowAddPdfButton),
            nameof(FlyoutHeaderTitle), nameof(FlyoutHeaderDesc));
    }

    #endregion

    #region Projektdaten (Two-Way-Binding)

    public string ObjectName
    {
        get => _model.Object_name;
        set => SetOnModel(_model.Object_name, value, v => _model.Object_name = v,
                          [nameof(HeaderTitle), nameof(FlyoutHeaderTitle), nameof(HasName)]);
    }

    public string ClientName
    {
        get => _model.Client_name;
        set => SetOnModel(_model.Client_name, value, v => _model.Client_name = v,
                          [nameof(FlyoutHeaderDesc)]);
    }

    public string ObjectAddress
    {
        get => _model.Object_address;
        set => SetOnModel(_model.Object_address, value, v => _model.Object_address = v,
                          [nameof(HeaderSubtitle)]);
    }

    public string WorkingTitle
    {
        get => _model.Working_title;
        set => SetOnModel(_model.Working_title, value, v => _model.Working_title = v,
                          [nameof(HeaderSubtitle)]);
    }

    public string ProjectNr
    {
        get => _model.Project_nr;
        set => SetOnModel(_model.Project_nr, value, v => _model.Project_nr = v,
                          [nameof(HeaderSubtitle)]);
    }

    public string ProjectManager
    {
        get => _model.Project_manager;
        set => SetOnModel(_model.Project_manager, value, v => _model.Project_manager = v);
    }

    public DateTime CreationDate
    {
        get => _model.Creation_date;
        set => SetOnModel(_model.Creation_date, value, v => _model.Creation_date = v,
                          [nameof(CreationDateDisplay)]);
    }

    public string TitleImage
    {
        get => _model.TitleImage;
        set => SetOnModel(_model.TitleImage, value, v => _model.TitleImage = v,
        [nameof(TitleImagePath), nameof(ThumbnailImagePath),
                nameof(TitleImageSource), nameof(TitleImageFullSource),
                nameof(HasTitleImage)]);
    }

    #endregion

    #region Pfade und Cloud

    public string ProjectId => _model.ProjectId;
    public string CloudDriveId => _model.CloudDriveId;
    public string CloudFolderId => _model.CloudFolderId;
    public bool IsCloudLinked => !string.IsNullOrWhiteSpace(_model.CloudFolderId);

#pragma warning disable CA1822
    public string ProjectDirectory =>
        string.IsNullOrWhiteSpace(SettingsService.Instance?.ProjectPath)
            ? null
            : Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath);
#pragma warning restore CA1822

    public string PlanFolder => Or(_model.PlanPath, "plans");
    public string ImageFolder => Or(_model.ImagePath, "images");
    public string ThumbnailFolder => Or(_model.ThumbnailPath, "thumbnails");
    public string CustomPinsFolder => Or(_model.CustomPinsPath, "custompins");
    public string TitleImageFileName => Or(_model.TitleImage, DefaultTitleImage);

    private static string Or(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    #endregion

    #region Titelbild

    private bool HasCustomTitleImage =>
        ProjectDirectory != null &&
        !string.IsNullOrEmpty(_model.TitleImage) &&
        _model.TitleImage != DefaultTitleImage;

    private string ThumbnailImagePathRaw =>
        ProjectDirectory == null ? null : Path.Combine(ProjectDirectory, ThumbnailFolder, TitleImageFileName);

    private string ImagePathRaw =>
        ProjectDirectory == null ? null : Path.Combine(ProjectDirectory, ImageFolder, TitleImageFileName);

    /// <summary>Originalbild (volle Aufloesung) - fuer ProjectDetails.</summary>
    public ImageSource TitleImageFullSource
    {
        get
        {
            // Fallback auf das Thumbnail, falls das Original (noch) nicht geladen ist
            string path = HasCustomTitleImage && File.Exists(ImagePathRaw)
            ? ImagePathRaw
            : HasTitleImage ? ThumbnailImagePathRaw : null;

            if (path == null)
                return ImageSource.FromFile(DefaultTitleImage);

            var bytes = File.ReadAllBytes(path);
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }

    /// <summary>True, wenn ein eigenes Titelbild gesetzt ist und die Datei existiert.</summary>
    public bool HasTitleImage => HasCustomTitleImage && File.Exists(ThumbnailImagePathRaw);

    public string TitleImagePath => HasTitleImage ? ImagePathRaw : "";

    public string ThumbnailImagePath =>
        HasTitleImage ? ThumbnailImagePathRaw : DefaultTitleImage;

    /// <summary>Bild als Stream - umgeht den MAUI-Bildcache bei gleichem Dateinamen.</summary>
    public ImageSource TitleImageSource
    {
        get
        {
            if (!HasTitleImage)
                return ImageSource.FromFile(DefaultTitleImage);

            var bytes = File.ReadAllBytes(ThumbnailImagePathRaw);
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
    }

    public void RefreshTitleImage()
        => Notify(nameof(HasTitleImage), nameof(TitleImagePath), nameof(ThumbnailImagePath),
        nameof(TitleImageSource), nameof(TitleImageFullSource));

    #endregion

    #region Anzeigewerte (ersetzen Helper.HeaderUpdate)

    public static bool IsLoaded => SettingsService.Instance?.IsProjectLoaded == true;
    public bool HasName => !string.IsNullOrWhiteSpace(_model.Object_name);

    public string FlyoutHeaderTitle =>
        IsLoaded && HasName ? _model.Object_name : SettingsService.FlyoutHeaderTitle;

    public string FlyoutHeaderDesc =>
        IsLoaded && HasName
        ? _model.Client_name ?? ""
        : SettingsService.FlyoutHeaderDesc;

    public string HeaderTitle => HasName ? _model.Object_name : "-";

    public string HeaderSubtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_model.Project_nr)) parts.Add(_model.Project_nr);
            if (!string.IsNullOrWhiteSpace(_model.Working_title)) parts.Add(_model.Working_title);
            if (!string.IsNullOrWhiteSpace(_model.Object_address)) parts.Add(_model.Object_address);
            return string.Join("  /  ", parts);
        }
    }

    public string CreationDateDisplay =>
        _model.Creation_date == default
        ? string.Empty
        : _model.Creation_date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public int PlanCount => _model.Plans?.Count ?? 0;
    public bool HasPlans => PlanCount > 0;
    public int PinCountTotal => _model.Plans?.Values.Sum(p => p.Pins?.Count ?? 0) ?? 0;

    #endregion

    #region Planlisten

    /// <summary>Alle Plaene des Projekts (Masterliste).</summary>
    public ObservableCollection<PlanItem> AllPlanItems { get; } = [];

    /// <summary>Gefilterte Anzeigeliste fuer die CollectionView im Flyout.</summary>
    public ObservableCollection<PlanItem> PlanItems { get; } = [];

    [ObservableProperty] public partial string InfoText { get; set; }

    public bool ShowAddPdfButton => IsLoaded && AllPlanItems.Count == 0;

    public void ApplyFilterAndSorting()
    {
        var filtered = AllPlanItems
            .Where(p => !SettingsService.Instance.IsHideInactivePlans || p.AllowExport)
            .ToList();

        PlanItems.Clear();
        foreach (var item in filtered)
            PlanItems.Add(item);

        InfoText = BuildInfoText();

        OnPropertyChanged(nameof(ShowAddPdfButton));
        RaiseComputed();
    }

    private string BuildInfoText()
    {
        if (!IsLoaded)
            return AppResources.kein_projekt_geladen;

        if (AllPlanItems.Count == 0)
            return AppResources.keine_pdf_seiten;

        if (SettingsService.Instance.IsHideInactivePlans && AllPlanItems.Count > PlanItems.Count)
            return $"{AppResources.ausgeblendete_plaene}: {AllPlanItems.Count - PlanItems.Count}";

        return AppResources.plaene_umsortieren_gedrueckt_halten_und_ziehen;
    }

    /// <summary>Planliste und Shell-Routen komplett aus GlobalJson neu aufbauen.</summary>
    public void ReloadPlansFromData()
    {
        LoadDataToView.ClearAllPlansFromShell();

        if (_model?.Plans != null)
        {
            foreach (var plan in _model.Plans)
                LoadDataToView.AddPlan(plan);
        }

        ApplyFilterAndSorting();

        // Pruefen, ob der aktuell geoeffnete Plan geloescht wurde
        string currentRoute = Shell.Current?.CurrentState?.Location?.OriginalString;
        if (string.IsNullOrEmpty(currentRoute)) return;

        string currentPlanId = currentRoute.TrimStart('/');
        bool isPlanRoute = currentPlanId.StartsWith("webmap_") || currentPlanId.StartsWith("plan_");

        if (isPlanRoute && (_model?.Plans == null || !_model.Plans.ContainsKey(currentPlanId)))
            Shell.Current.GoToAsync("//homescreen");
    }

    #endregion

    #region Commands und Reset

    [RelayCommand]
    private static void Save() => SaveManager.NotifyDataChanged();

    /// <summary>Setzt alle Projektdaten zurueck (Datenteil von LoadDataToView.ResetData).</summary>
    public void Reset()
    {
        _suspendSave = true;
        try
        {
            _model.Client_name = null;
            _model.Object_address = null;
            _model.Working_title = null;
            _model.Project_nr = null;
            _model.Object_name = null;
            _model.Creation_date = DateTime.Now;
            _model.Project_manager = null;
            _model.Plans = null;
            _model.PlanPath = null;
            _model.ImagePath = null;
            _model.ThumbnailPath = null;
            _model.CustomPinsPath = null;

            AllPlanItems.Clear();
            PlanItems.Clear();
        }
        finally
        {
            _suspendSave = false;
        }

        RefreshAll();
    }

    #endregion

    #region Infrastruktur

    private void SetOnModel<T>(T current, T value, Action<T> setter, string[] alsoNotify = null, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;

        setter(value);
        OnPropertyChanged(propertyName);

        if (alsoNotify != null)
            foreach (var p in alsoNotify)
                OnPropertyChanged(p);

        if (!_suspendSave)
            SaveManager.NotifyDataChanged();   // Autosave beim Tippen/Aendern
    }

    private void Notify(params string[] propertyNames)
    {
        foreach (var p in propertyNames)
            OnPropertyChanged(p);
    }

    private void RaiseComputed() => Notify(
        nameof(HeaderTitle), nameof(HeaderSubtitle), nameof(HasName),
        nameof(FlyoutHeaderTitle), nameof(FlyoutHeaderDesc),
        nameof(IsLoaded), nameof(PlanCount), nameof(HasPlans), nameof(PinCountTotal),
        nameof(IsCloudLinked), nameof(CreationDateDisplay),
        nameof(HasTitleImage), nameof(TitleImagePath), nameof(ThumbnailImagePath));

    /// <summary>Alle Bindings neu auswerten (nach Attach/Reset/Load).</summary>
    public void RefreshAll() => OnPropertyChanged(string.Empty);

    #endregion
}
