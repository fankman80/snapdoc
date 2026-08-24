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

    public CloudPickerMode Mode => _mode;
    public bool IsFolderMode => _mode == CloudPickerMode.SelectFolder;
    public bool IsJsonMode => _mode == CloudPickerMode.SelectJsonFile;

    public string HeaderTitle => IsFolderMode 
        ? AppResources.cloud_verzeichnis_waehlen 
        : AppResources.projektdatei_importieren;

    public string HeaderSubtitle => IsFolderMode
        ? "Waehle den Ordner aus, in dem das neue Projekt gespeichert werden soll."
        : "Waehle die .json-Projektdatei aus, die synchronisiert werden soll.";

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
        await LoadRootFolderAsync();
    }

    private async Task LoadRootFolderAsync()
    {
        if (SaveManager.CurrentAuth?.GraphClient == null) return;

        try
        {
            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.projekte_werden_gesucht);

            var myDrive = await SaveManager.CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
            {
                _currentDriveId = myDrive.Id;
                _folderHistory.Clear();
                await LoadFolderContentAsync("root");
            }

            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppResources.fehler, $"{AppResources.konnte_onedrive_nicht_laden}: {ex.Message}", AppResources.ok);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
    }

    private async Task LoadFolderContentAsync(string folderId)
    {
        if (SaveManager.CurrentAuth?.GraphClient == null) return;

        try
        {
            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.projekte_werden_gesucht);

            if (_folderHistory.Count == 0 || _folderHistory.Peek() != folderId)
            {
                _folderHistory.Push(folderId);
                OnPropertyChanged(nameof(CanGoBack));
            }

            var children = await SaveManager.CurrentAuth.GraphClient.Drives[_currentDriveId]
                .Items[folderId].Children
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "folder", "file"];
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

                    // Ordner-Modus: Zeige nur Ordner an
                    // JSON-Modus: Zeige Ordner (zum Navigieren) UND .json-Dateien an
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

            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppResources.fehler, $"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}", AppResources.ok);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is CloudItem selectedItem)
        {
            ((CollectionView)sender).SelectedItem = null;

            if (selectedItem.Id == "..")
            {
                OnBackClicked(sender, e);
                return;
            }

            if (selectedItem.IsFolder)
            {
                await LoadFolderContentAsync(selectedItem.Id);
            }
            else if (IsJsonMode)
            {
                string? activeJsonFile = GlobalJson.Data?.JsonFile;

                if (!string.IsNullOrEmpty(activeJsonFile) &&
                    selectedItem.Name.Equals(activeJsonFile, StringComparison.OrdinalIgnoreCase))
                {
                    if (_folderHistory.Count > 0)
                    {
                        string currentFolderId = _folderHistory.Peek();

                        try
                        {
                            // Ladeanzeige aktivieren
                            await BusyService.ShowAsync("Projekt wird synchronisiert...");

                            bool success = await SaveManager.SyncWithExistingFolderAsync(currentFolderId);

                            if (success)
                                await Navigation.PopAsync();
                            else
                                await DisplayAlertAsync(AppResources.fehler, $"{AppResources.synchronisierung_konnte_nicht_gestartet_werden}", AppResources.ok);
                        }
                        finally
                        {
                            // Ladeanzeige deaktivieren
                            await BusyService.HideAsync();
                        }
                    }
                }
                else
                {
                    await DisplayAlertAsync(AppResources.fehler, $"'{selectedItem.Name}' {AppResources.entspricht_nicht_aktuell_geladenem_projekt} ('{activeJsonFile}').", AppResources.ok);
                }
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (CanGoBack)
        {
            _folderHistory.Pop();
            string previousFolderId = _folderHistory.Pop();
            await LoadFolderContentAsync(previousFolderId);
        }
    }

    private async void OnSelectFolderClicked(object sender, EventArgs e)
    {
        if (IsFolderMode && _folderHistory.Count > 0)
        {
            string currentFolderId = _folderHistory.Peek();

            try
            {
                // Ladeanzeige aktivieren
                await BusyService.ShowAsync("Projekt wird hochgeladen...");

                bool success = await SaveManager.CreateAndSyncNewCloudProjectAsync(currentFolderId);

                if (success)
                    await Navigation.PopAsync();
                else
                    await DisplayAlertAsync(AppResources.fehler, AppResources.projektverzeichnis_cloud_konnte_nicht_erstellt_werden, AppResources.ok);
            }
            finally
            {
                // Ladeanzeige deaktivieren
                await BusyService.HideAsync();
            }
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
