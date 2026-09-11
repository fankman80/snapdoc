using CommunityToolkit.Maui.Extensions;
using Microsoft.Graph;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SnapDoc.Views;

[QueryProperty(nameof(ModeParam), "mode")]
public partial class CloudPickerPage : ContentPage, INotifyPropertyChanged
{
    private string _currentDriveId = string.Empty;
    private CloudPickerMode _mode = CloudPickerMode.SelectFolder;
    private bool _isInitialized;

    // Begrenzt gleichzeitige Object_name-Abfragen, damit nicht 50 Requests auf einmal feuern
    private static readonly SemaphoreSlim _objectNameLookupGate = new(4);

    public string ModeParam
    {
        set
        {
            if (Enum.TryParse<CloudPickerMode>(value, out var parsedMode))
            {
                _mode = parsedMode;
                Title = IsFolderMode ? AppResources.projekt_upload : AppResources.projekt_download;

                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Mode));
                OnPropertyChanged(nameof(IsFolderMode));
                OnPropertyChanged(nameof(IsJsonMode));
                OnPropertyChanged(nameof(HeaderSubtitle));
            }
        }
    }

    public bool IsListEmpty => CloudItems == null || CloudItems.Count == 0;
    public CloudPickerMode Mode => _mode;
    public bool IsFolderMode => _mode == CloudPickerMode.SelectFolder;
    public bool IsJsonMode => _mode == CloudPickerMode.SelectJsonFile;
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; set; } = [];
    public string HeaderSubtitle => IsFolderMode
        ? AppResources.ordner_auswaehlen_projekt_speichern
        : AppResources.projektdatei_auswaehlen_zum_synchronisieren;

    public ObservableCollection<CloudItem> CloudItems { get; set; } = [];

    public CloudPickerPage()
    {
        InitializeComponent();

        Title = IsFolderMode
                    ? AppResources.projekt_upload
                    : AppResources.projekt_download;

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isInitialized)
            return;

        _isInitialized = true;

        Title = IsFolderMode
            ? AppResources.projekt_upload
            : AppResources.projekt_download;

        await LoadRootFolderAsync();
    }

    private async Task LoadRootFolderAsync()
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return;
        try
        {
            var myDrive = await SaveManager.CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
            {
                _currentDriveId = myDrive.Id;

                string rootName = !string.IsNullOrWhiteSpace(myDrive.Name)
                    ? myDrive.Name
                    : "Cloud";

                Breadcrumbs.Clear();
                Breadcrumbs.Add(new BreadcrumbItem { Id = "root", Name = rootName, IsLast = true });

                await LoadFolderContentAsync("root");
            }
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.konnte_onedrive_nicht_laden}: {ex.Message}", AppResources.fehler), Settings.PopupOptions);
        }
    }

    private async Task LoadFolderContentAsync(string folderId)
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return;

        try
        {
            var children = await SaveManager.CurrentAuth.GraphClient
                .Drives[_currentDriveId]
                .Items[folderId]
                .Children
                .GetAsync(config =>
                {
                    // lastModifiedDateTime hinzugefügt
                    config.QueryParameters.Select = ["id", "name", "folder", "file", "lastModifiedDateTime", "parentReference"];
                });

            CloudItems.Clear();

            if (children?.Value != null)
            {
                foreach (var item in children.Value)
                {
                    bool isFolder = item.Folder != null;
                    bool isJson = item.File != null && item.Name?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true;

                    if (isFolder || (IsJsonMode && isJson))
                    {
                        var cloudItem = new CloudItem
                        {
                            Id = item.Id ?? string.Empty,
                            Name = item.Name ?? "Unbekannt",
                            IsFolder = isFolder,
                            LastModified = item.LastModifiedDateTime // Datum übernehmen
                        };

                        // Wenn es eine Json ist, das RemoteProject erstellen
                        if (isJson)
                        {
                            cloudItem.RemoteProject = new RemoteProjectDto
                            {
                                DriveId = item.ParentReference?.DriveId ?? _currentDriveId,
                                FolderId = item.ParentReference?.Id ?? folderId,
                                ItemId = item.Id ?? string.Empty,
                                FileName = item.Name ?? string.Empty,
                                LastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue
                            };

                            // Objektbezeichnung im Hintergrund nachladen (blockiert die Anzeige nicht).
                            // Solange dies noch läuft, bleibt einfach der Dateiname sichtbar.
                            _ = ResolveObjectNameAsync(cloudItem);
                        }

                        CloudItems.Add(cloudItem);
                    }
                }
            }
            OnPropertyChanged(nameof(IsListEmpty));
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}", AppResources.fehler), Settings.PopupOptions);
        }
    }

    /// <summary>
    /// Liest ausschliesslich das Feld "Object_name" aus der Cloud-JSON aus (ohne komplettes
    /// Deserialisieren) und ersetzt anschliessend im UI den Dateinamen durch die Objektbezeichnung.
    /// Schlägt der Abruf fehl (z.B. Netzwerkfehler oder Feld fehlt), bleibt einfach der Dateiname stehen.
    /// </summary>
    private static async Task ResolveObjectNameAsync(CloudItem cloudItem)
    {
        var project = cloudItem.RemoteProject;

        if (project == null || SaveManager.CurrentAuth?.GraphClient == null)
            return;

        await _objectNameLookupGate.WaitAsync();

        try
        {
            await using var stream = await SaveManager.CurrentAuth.GraphClient
                .Drives[project.DriveId]
                .Items[project.FolderId]
                .ItemWithPath(project.FileName)
                .Content
                .GetAsync();

            if (stream == null)
                return;

            using var document = await JsonDocument.ParseAsync(stream);

            if (document.RootElement.TryGetProperty("Object_name", out var property))
            {
                string? objectName = property.GetString();

                if (!string.IsNullOrWhiteSpace(objectName))
                {
                    project.ObjectName = objectName;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        cloudItem.ObjectName = objectName;
                    });
                }
            }
        }
        catch
        {
            // Datei evtl. (noch) nicht lesbar / kein Object_name vorhanden.
        }
        finally
        {
            _objectNameLookupGate.Release();
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not CloudItem selectedItem)
            return;

        ((CollectionView)sender).SelectedItem = null;

        // Ordner öffnen
        if (selectedItem.IsFolder)
        {
            try
            {
                await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);

                // Breadcrumb aktualisieren
                if (Breadcrumbs.Count > 0)
                    Breadcrumbs.Last().IsLast = false;

                Breadcrumbs.Add(new BreadcrumbItem { Id = selectedItem.Id, Name = selectedItem.Name, IsLast = true });

                ScrollBreadcrumbsToRight();

                await LoadFolderContentAsync(selectedItem.Id);
            }
            finally
            {
                await BusyService.HideAsync();
            }

            return;
        }

        // Nur im JSON-Modus können Dateien ausgewählt werden
        if (!IsJsonMode)
            return;

        // JSON-Datei?
        if (!selectedItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return;

        // RemoteProjectDto vorhanden?
        if (selectedItem.RemoteProject == null)
        {
            await this.ShowPopupAsync(new PopupAlert(AppResources.fehler_beim_herunterladen_des_projekts, AppResources.fehler), Settings.PopupOptions);
            return;
        }

        var project = selectedItem.RemoteProject;

        // Bestätigung - bevorzugt die Objektbezeichnung aus der JSON, sonst Fallback auf Dateinamen
        string projectName = project.DisplayName;
        var popup = new PopupDualResponse(string.Format(AppResources.projekt_wirklich_herunterladen, projectName), AppResources.info);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result?.Result is not DualPopupResult.Ok) return;

        try
        {
            await BusyService.ShowAsync(AppResources.projekt_wird_heruntergeladen);

            bool success = await SaveManager.DownloadRemoteProjectAsync(project);

            if (success)
            {
                await BusyService.HideAsync();
                await this.ShowPopupAsync(new PopupAlert(AppResources.projekt_erfolgreich_heruntergeladen, AppResources.info), Settings.PopupOptions);

                // Picker verlassen
                await Navigation.PopAsync();
            }
            else
            {
                await BusyService.HideAsync();
                await this.ShowPopupAsync(new PopupAlert(AppResources.fehler_beim_herunterladen_des_projekts, AppResources.fehler), Settings.PopupOptions);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Herunterladen des Projekts: {ex}");

            await BusyService.HideAsync();
            await this.ShowPopupAsync(new PopupAlert(ex.Message, AppResources.fehler), Settings.PopupOptions);
        }
    }

    private async void OnBreadcrumbTapped(object sender, TappedEventArgs e)
    {
        var tappedItem = e.Parameter as BreadcrumbItem ?? (sender as Element)?.BindingContext as BreadcrumbItem;

        if (tappedItem == null) return;

        if (tappedItem.IsLast) return;

        int targetIndex = Breadcrumbs.IndexOf(tappedItem);
        if (targetIndex == -1) return;

        while (Breadcrumbs.Count > targetIndex + 1)
        {
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }
        Breadcrumbs.Last().IsLast = true;

        try
        {
            await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);
            await LoadFolderContentAsync(tappedItem.Id);

            ScrollBreadcrumbsToRight();
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {
        if (!IsFolderMode || Breadcrumbs.Count == 0)
            return;

        string currentFolderId = Breadcrumbs.Last().Id;

        // Nur isSuccess wird ganz am Ende der Methode noch benötigt
        bool isSuccess = false;

        try
        {
            await BusyService.ShowAsync(AppResources.projekt_wird_hochgeladen);

            // Die Methode gibt die Werte direkt in die neu erstellten Variablen success, driveId und folderId
            var (success, driveId, folderId) = await SaveManager.CreateAndSyncNewCloudProjectAsync(currentFolderId);

            // Den Status für die spätere Navigation speichern
            isSuccess = success;

            if (isSuccess && !string.IsNullOrEmpty(folderId))
            {
                // 1. Lokale Daten im RAM aktualisieren (direkte Nutzung der Tuple-Variablen)
                GlobalJson.Data.CloudDriveId = driveId;
                GlobalJson.Data.CloudFolderId = folderId;

                // 2. Lokale JSON direkt speichern
                string json = System.Text.Json.JsonSerializer.Serialize(GlobalJson.Data, GlobalJson.GetOptions());
                string filePath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, SettingsService.DefaultJson);
                File.WriteAllText(filePath, json);
            }
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert(ex.Message, AppResources.fehler), Settings.PopupOptions);
        }
        finally
        {
            await BusyService.HideAsync();
        }

        if (isSuccess)
            await Navigation.PopAsync();
        else
            await this.ShowPopupAsync(new PopupAlert(AppResources.projektverzeichnis_cloud_konnte_nicht_erstellt_werden, AppResources.fehler), Settings.PopupOptions);
    }

    private async void ScrollBreadcrumbsToRight()
    {
        await Task.Delay(50);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                double contentWidth = BreadcrumbScrollView.ContentSize.Width;
                double viewportWidth = BreadcrumbScrollView.Width;

                if (contentWidth > viewportWidth)
                {
                    await BreadcrumbScrollView.ScrollToAsync(contentWidth - viewportWidth, 0, true);
                }
            }
            catch
            {
                // Fallback falls die Viewport-Berechnung beim ersten Laden hakt
            }
        });
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}

public partial class BreadcrumbItem : INotifyPropertyChanged
{
    private bool _isLast;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public bool IsLast
    {
        get => _isLast;
        set
        {
            _isLast = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLast)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}