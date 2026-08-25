using CommunityToolkit.Maui.Extensions;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public enum CloudPickerMode
{
    SelectFolder,
    SelectJsonFile
}

public partial class CloudPickerPage : ContentPage, INotifyPropertyChanged
{
    private string _currentDriveId = string.Empty;
    private readonly Stack<string> _folderHistory = new();
    private readonly CloudPickerMode _mode;
    private bool _isInitialized;

    public CloudPickerMode Mode => _mode;
    public bool IsFolderMode => _mode == CloudPickerMode.SelectFolder;
    public bool IsJsonMode => _mode == CloudPickerMode.SelectJsonFile;

    public string HeaderTitle => IsFolderMode 
        ? AppResources.cloud_verzeichnis_waehlen 
        : AppResources.projektdatei_importieren;

    public string HeaderSubtitle => IsFolderMode
        ? "Wähle den Ordner aus, in dem das neue Projekt gespeichert werden soll."
        : "Wähle die .json-Projektdatei aus, die synchronisiert werden soll.";

    public ObservableCollection<CloudItem> CloudItems { get; set; } = [];
    public bool CanGoBack => _folderHistory.Count > 1;

    public CloudPickerPage(CloudPickerMode mode = CloudPickerMode.SelectFolder)
    {
        _mode = mode;

        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isInitialized)
            return;

        _isInitialized = true;

        await LoadRootFolderAsync();
    }

    private async Task LoadRootFolderAsync()
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return;

        try
        {
            var myDrive =
                await SaveManager.CurrentAuth.GraphClient.Me.Drive.GetAsync();

            if (myDrive != null &&
                !string.IsNullOrEmpty(myDrive.Id))
            {
                _currentDriveId = myDrive.Id;
                _folderHistory.Clear();

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
            if (_folderHistory.Count == 0 || _folderHistory.Peek() != folderId)
            {
                _folderHistory.Push(folderId);
                OnPropertyChanged(nameof(CanGoBack));
            }

            var children = await SaveManager.CurrentAuth.GraphClient
                .Drives[_currentDriveId]
                .Items[folderId]
                .Children
                .GetAsync(config =>
                {
                    // lastModifiedDateTime hinzugefügt
                    config.QueryParameters.Select =
                        ["id", "name", "folder", "file", "lastModifiedDateTime", "parentReference"];
                });

            CloudItems.Clear();

            if (CanGoBack)
            {
                CloudItems.Add(new CloudItem
                {
                    Id = "..",
                    Name = "..",
                    IsFolder = true
                });
            }

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
                                FileName = item.Name ?? string.Empty,
                                LastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue
                            };
                        }

                        CloudItems.Add(cloudItem);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}", AppResources.fehler), Settings.PopupOptions);
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not CloudItem selectedItem)
            return;

        ((CollectionView)sender).SelectedItem = null;

        // "Zurück"-Eintrag
        if (selectedItem.Id == "..")
        {
            await GoBackAsync();
            return;
        }

        // Ordner öffnen
        if (selectedItem.IsFolder)
        {
            try
            {
                await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);

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

        // Bestätigung
        string projectName = Path.GetFileNameWithoutExtension(project.FileName);
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

    private async Task GoBackAsync()
    {
        if (!CanGoBack)
            return;

        _folderHistory.Pop();

        string previousFolderId = _folderHistory.Peek();

        try
        {
            await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);

            await LoadFolderContentAsync(previousFolderId);
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {
        if (!IsFolderMode || _folderHistory.Count == 0)
            return;

        string currentFolderId = _folderHistory.Peek();
        bool success = false;

        try
        {
            await BusyService.ShowAsync(AppResources.projekt_wird_geladen);

            success =
                await SaveManager.CreateAndSyncNewCloudProjectAsync(currentFolderId);
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert(ex.Message, AppResources.fehler), Settings.PopupOptions);
        }
        finally
        {
            // Overlay zuerst schließen
            await BusyService.HideAsync();
        }

        if (success)
            await Navigation.PopAsync();
        else
            await this.ShowPopupAsync(new PopupAlert(AppResources.projektverzeichnis_cloud_konnte_nicht_erstellt_werden, AppResources.fehler), Settings.PopupOptions);
    }

    private async void OnSearchProjectsClicked(object sender, EventArgs e)
    {
        if (SaveManager.CurrentAuth == null ||
            !SaveManager.CurrentAuth.IsLoggedIn)
        {
            await this.ShowPopupAsync(new PopupAlert(AppResources.bitte_zuerst_anmelden, AppResources.info), Settings.PopupOptions);
            return;
        }

        try
        {
            await BusyService.ShowAsync(
                AppResources.projekte_werden_gesucht);

            var remoteProjects =
                await SaveManager.SearchRemoteProjectsAsync();

            await BusyService.HideAsync();

            if (remoteProjects.Count == 0)
            {
                await this.ShowPopupAsync(new PopupAlert(AppResources.keine_projekte_in_cloud_gefunden, AppResources.info), Settings.PopupOptions);
                return;
            }

            var popup = new PopupCloudProjects(remoteProjects);
            var result = await this.ShowPopupAsync<RemoteProjectDto>(popup, Settings.PopupOptions);

            if (result?.Result == null)
                return;

            await NavigateToProjectAsync(result.Result);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler bei der Cloud-Suche: {ex}");
            await this.ShowPopupAsync(new PopupAlert(ex.Message, AppResources.fehler), Settings.PopupOptions);
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    private async Task NavigateToProjectAsync(RemoteProjectDto project)
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return;

        try
        {
            await BusyService.ShowAsync(AppResources.projektordner_wird_geoeffnet);

            _currentDriveId = project.DriveId;
            _folderHistory.Clear();

            // Root als Ausgangspunkt merken
            _folderHistory.Push("root");

            // Projektordner laden
            await LoadFolderContentAsync(project.FolderId);
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.projektordner_konnte_nicht_geoeffnet_werden}:\n{ex.Message}", AppResources.fehler), Settings.PopupOptions);
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
