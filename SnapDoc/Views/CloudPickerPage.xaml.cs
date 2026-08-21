using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class CloudPickerPage : ContentPage, INotifyPropertyChanged
{
    private string _currentDriveId = string.Empty;
    private readonly Stack<string> _folderHistory = new();
    public ObservableCollection<CloudItem> CloudItems { get; set; } = [];
    public bool CanGoBack => _folderHistory.Count > 1;

    public CloudPickerPage()
    {
        InitializeComponent();
        BindingContext = this;
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
            var myDrive = await SaveManager.CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
            {
                _currentDriveId = myDrive.Id;
                _folderHistory.Clear();
                await LoadFolderContentAsync("root");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppResources.fehler, $"{AppResources.konnte_onedrive_nicht_laden}: {ex.Message}", AppResources.ok);
        }
    }

    private async Task LoadFolderContentAsync(string folderId)
    {
        if (SaveManager.CurrentAuth?.GraphClient == null) return;

        try
        {
            // Neuen Ordner in die Historie legen
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
                    // Zeige nur Ordner oder .json Dateien
                    if (item.Folder != null || (item.File != null && item.Name?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        CloudItems.Add(new CloudItem
                        {
                            Id = item.Id ?? string.Empty,
                            Name = item.Name ?? "Unbekannt",
                            IsFolder = item.Folder != null
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppResources.fehler, $"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}", AppResources.ok);
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is CloudItem selectedItem)
        {
            ((CollectionView)sender).SelectedItem = null;

            // Prüfen, ob der ".." Zurück-Eintrag geklickt wurde
            if (selectedItem.Id == "..")
            {
                OnBackClicked(sender, e);
                return;
            }

            if (selectedItem.IsFolder)
            {
                await LoadFolderContentAsync(selectedItem.Id);
            }
            else
            {
                // Prüfe, ob der Dateiname exakt der geladenen GlobalJson entspricht
                string? activeJsonFile = GlobalJson.Data?.JsonFile;

                if (!string.IsNullOrEmpty(activeJsonFile) &&
                    selectedItem.Name.Equals(activeJsonFile, StringComparison.OrdinalIgnoreCase))
                {
                    if (_folderHistory.Count > 0)
                    {
                        string currentFolderId = _folderHistory.Peek();
                        bool success = await SaveManager.SyncWithExistingFolderAsync(currentFolderId);

                        if (success)
                        {
                            await DisplayAlertAsync(AppResources.erfolg, $"{AppResources.projekt_wird_synchronisiert_mit}: '{selectedItem.Name}'", AppResources.ok);
                            await Navigation.PopAsync();
                        }
                        else
                        {
                            await DisplayAlertAsync(AppResources.fehler, $"{AppResources.synchronisierung_konnte_nicht_gestartet_werden}", AppResources.ok);
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
        if (_folderHistory.Count > 0)
        {
            string currentFolderId = _folderHistory.Peek();

            // Erstelle neues Projektverzeichnis im ausgewählten Ordner
            bool success = await SaveManager.CreateAndSyncNewCloudProjectAsync(currentFolderId);

            if (success)
            {
                await DisplayAlertAsync(AppResources.erfolg, AppResources.neues_projektverzeichnis_cloud_erstellt, AppResources.ok);
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlertAsync(AppResources.fehler, AppResources.projektverzeichnis_cloud_konnte_nicht_erstellt_werden, AppResources.ok);
            }
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}