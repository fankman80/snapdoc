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
            await DisplayAlertAsync(
                AppResources.fehler,
                $"{AppResources.konnte_onedrive_nicht_laden}: {ex.Message}",
                AppResources.ok);
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
                    config.QueryParameters.Select =
                        ["id", "name", "folder", "file"];
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

                    bool isJson =
                        item.File != null &&
                        item.Name?.EndsWith(
                            ".json",
                            StringComparison.OrdinalIgnoreCase) == true;

                    if (isFolder || (IsJsonMode && isJson))
                    {
                        CloudItems.Add(new CloudItem
                        {
                            Id = item.Id ?? string.Empty,
                            Name = item.Name ?? "Unbekannt",
                            IsFolder = isFolder
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                AppResources.fehler,
                $"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}",
                AppResources.ok);
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 ||
            e.CurrentSelection[0] is not CloudItem selectedItem)
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
                await BusyService.ShowAsync("Verzeichnis wird geladen...");

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

        string? activeJsonFile = GlobalJson.Data?.JsonFile;

        if (string.IsNullOrEmpty(activeJsonFile))
        {
            await DisplayAlertAsync(
                AppResources.fehler,
                AppResources.kein_projekt_geladen,
                AppResources.ok);

            return;
        }

        // Prüfen, ob die ausgewählte JSON-Datei zum aktuellen Projekt gehört
        if (!selectedItem.Name.Equals(
                activeJsonFile,
                StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync(
                AppResources.fehler,
                $"'{selectedItem.Name}' " +
                $"{AppResources.entspricht_nicht_aktuell_geladenem_projekt} " +
                $"('{activeJsonFile}').",
                AppResources.ok);

            return;
        }

        // Aktuellen Cloud-Ordner synchronisieren
        if (_folderHistory.Count == 0)
            return;

        string currentFolderId = _folderHistory.Peek();

        try
        {
            await BusyService.ShowAsync("Projekt wird synchronisiert...");

            bool success =
                await SaveManager.SyncWithExistingFolderAsync(currentFolderId);

            if (success)
            {
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync(
                    AppResources.fehler,
                    AppResources.synchronisierung_konnte_nicht_gestartet_werden,
                    AppResources.ok);
            }
        }
        finally
        {
            await BusyService.HideAsync();
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
            await BusyService.ShowAsync("Verzeichnis wird geladen...");

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
            await BusyService.ShowAsync("Projekt wird hochgeladen...");

            success =
                await SaveManager.CreateAndSyncNewCloudProjectAsync(currentFolderId);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(
                AppResources.fehler,
                ex.Message,
                AppResources.ok);
        }
        finally
        {
            // Overlay zuerst schließen
            await BusyService.HideAsync();
        }

        if (success)
        {
            // Jetzt ist die CloudPickerPage wieder bedienbar/geschlossen
            await Navigation.PopAsync();
        }
        else
        {
            await DisplayAlertAsync(
                AppResources.fehler,
                AppResources.projektverzeichnis_cloud_konnte_nicht_erstellt_werden,
                AppResources.ok);
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
