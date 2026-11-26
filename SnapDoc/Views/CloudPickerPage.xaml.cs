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
    private CloudPickerMode _mode = CloudPickerMode.SelectFolder;
    private bool _isInitialized;
    private bool _isNavigating;
    private static TaskCompletionSource<CloudFolderSelection?>? _folderPickTcs;
    private CloudFolderSelection? _pickedFolder;
    private static readonly SemaphoreSlim _objectNameLookupGate = new(4);
    public bool ShowEmptyState => IsJsonMode && IsListEmpty;

    private static string _pendingExportFileName = string.Empty;
    private string _exportFileName = string.Empty;

    /// <summary>
    /// Drive des aktuell angezeigten Ordners. Kann das eigene OneDrive sein oder –
    /// nach Oeffnen eines geteilten Ordners – das Drive einer anderen Person / SharePoint.
    /// </summary>
    private string CurrentDriveId => Breadcrumbs.LastOrDefault()?.DriveId ?? string.Empty;

    public string ExportFileName
    {
        get => _exportFileName;
        set
        {
            if (_exportFileName == value) return;
            _exportFileName = value;
            OnPropertyChanged();
        }
    }

    public string ModeParam
    {
        set
        {
            if (Enum.TryParse<CloudPickerMode>(value, out var parsedMode))
            {
                _mode = parsedMode;
                Title = GetTitle();
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Mode));
                OnPropertyChanged(nameof(IsFolderMode));
                OnPropertyChanged(nameof(IsJsonMode));
                OnPropertyChanged(nameof(IsExportMode));
                OnPropertyChanged(nameof(IsFolderSelection));
                OnPropertyChanged(nameof(HeaderSubtitle));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }
    }

    public bool IsListEmpty => CloudItems == null || CloudItems.Count == 0;
    public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; set; } = [];
    public CloudPickerMode Mode => _mode;
    public bool IsFolderMode => _mode == CloudPickerMode.SelectFolder;
    public bool IsJsonMode => _mode == CloudPickerMode.SelectJsonFile;
    public bool IsExportMode => _mode == CloudPickerMode.SelectExportFolder;
    public bool IsFolderSelection => IsFolderMode || IsExportMode;

    public string HeaderSubtitle => _mode switch
    {
        CloudPickerMode.SelectExportFolder => AppResources.ordner_auswaehlen_bericht_speichern,
        CloudPickerMode.SelectFolder => AppResources.ordner_auswaehlen_projekt_speichern,
        _ => AppResources.projektdatei_auswaehlen_zum_synchronisieren
    };

    public string ActionButtonText => IsExportMode
        ? AppResources.bericht_hier_speichern
        : AppResources.projekt_hier_speichern;

    private string GetTitle() => _mode switch
    {
        CloudPickerMode.SelectExportFolder => AppResources.bericht_in_cloud_hochladen,
        CloudPickerMode.SelectFolder => AppResources.projekt_upload,
        _ => AppResources.projekt_download
    };

    public ObservableCollection<CloudItem> CloudItems { get; set; } = [];

    public CloudPickerPage()
    {
        InitializeComponent();
        Title = GetTitle();
        ExportFileName = _pendingExportFileName;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isInitialized)
            return;
        _isInitialized = true;
        Title = GetTitle();
        await LoadRootFolderAsync();
    }

    private async Task LoadRootFolderAsync()
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return;
        try
        {
            // Einstiegspunkt bleibt das eigene OneDrive. Geteilte Ordner erscheinen
            // darin als Verknuepfung ("Zu Meine Dateien hinzufuegen").
            var myDrive = await SaveManager.CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
            {
                string rootName = !string.IsNullOrWhiteSpace(myDrive.Name)
                    ? myDrive.Name
                    : "Cloud";
                Breadcrumbs.Clear();
                Breadcrumbs.Add(new BreadcrumbItem { DriveId = myDrive.Id, Id = "root", Name = rootName, IsLast = true });
                await LoadFolderContentAsync(myDrive.Id, "root");
            }
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.konnte_onedrive_nicht_laden}: {ex.Message}", AppResources.fehler), Settings.PopupOptions);
        }
    }

    private async Task<bool> LoadFolderContentAsync(string driveId, string folderId)
    {
        if (SaveManager.CurrentAuth?.GraphClient == null)
            return false;
        try
        {
            var children = await SaveManager.CurrentAuth.GraphClient
                .Drives[driveId]
                .Items[folderId]
                .Children
                .GetAsync(config =>
                {
                    config.QueryParameters.Select =
                        ["id", "name", "folder", "file", "lastModifiedDateTime", "parentReference", "remoteItem"];
                });

            CloudItems.Clear();
            if (children?.Value != null)
            {
                foreach (var item in children.Value)
                {
                    // Geteilter Ordner als Verknuepfung: zeigt auf das Drive einer anderen Person.
                    // item.Folder ist hier null, die echten Daten stehen in RemoteItem.
                    var remote = item.RemoteItem;
                    bool isShortcut = remote?.Folder != null
                                      && !string.IsNullOrEmpty(remote.Id)
                                      && !string.IsNullOrEmpty(remote.ParentReference?.DriveId);

                    bool isFolder = item.Folder != null || isShortcut;
                    bool isFile = item.File != null;
                    bool isJson = isFile && item.Name?.EndsWith(".json", StringComparison.OrdinalIgnoreCase) == true;

                    // Ordner immer, JSON nur im Download-Modus, alle Dateien im Export-Modus
                    if (!(isFolder || (IsJsonMode && isJson) || (IsExportMode && isFile)))
                        continue;

                    var cloudItem = new CloudItem
                    {
                        Id = isShortcut ? remote!.Id! : item.Id ?? string.Empty,
                        DriveId = isShortcut ? remote!.ParentReference!.DriveId! : driveId,
                        Name = item.Name ?? "Unbekannt",
                        IsFolder = isFolder,
                        LastModified = item.LastModifiedDateTime
                    };

                    // Projektinfos nur im Download-Modus
                    if (isJson && IsJsonMode)
                    {
                        cloudItem.RemoteProject = new RemoteProjectDto
                        {
                            DriveId = item.ParentReference?.DriveId ?? driveId,
                            FolderId = item.ParentReference?.Id ?? folderId,
                            FileName = item.Name ?? string.Empty,
                            LastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue
                        };
                        // Objektbezeichnung im Hintergrund nachladen
                        _ = ResolveObjectNameAsync(cloudItem);
                    }

                    CloudItems.Add(cloudItem);
                }
            }

            OnPropertyChanged(nameof(IsListEmpty));
            OnPropertyChanged(nameof(ShowEmptyState));
            return true;
        }
        catch (Exception ex)
        {
            await this.ShowPopupAsync(new PopupAlert($"{AppResources.ordnerinhalt_konnte_nicht_geladen_werden}: {ex.Message}", AppResources.fehler), Settings.PopupOptions);
            return false;
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

    private async void OnItemSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count == 0 || e.CurrentSelection[0] is not CloudItem selectedItem)
            return;
        if (sender is CollectionView cv)
            cv.SelectedItem = null;

        // Ordner öffnen (auch geteilte Ordner – dann wechselt das Drive)
        if (selectedItem.IsFolder)
        {
            if (_isNavigating)
                return;
            _isNavigating = true;
            try
            {
                await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);

                if (!await LoadFolderContentAsync(selectedItem.DriveId, selectedItem.Id))
                    return; // Breadcrumbs bleiben unverändert

                if (Breadcrumbs.Count > 0)
                    Breadcrumbs.Last().IsLast = false;
                Breadcrumbs.Add(new BreadcrumbItem
                {
                    DriveId = selectedItem.DriveId,
                    Id = selectedItem.Id,
                    Name = selectedItem.Name,
                    IsLast = true
                });
                ScrollBreadcrumbsToRight();
            }
            finally
            {
                await BusyService.HideAsync();
                _isNavigating = false;
            }
            return;
        }

        // Export-Modus: Dateiname übernehmen (zum Überschreiben)
        if (IsExportMode)
        {
            ExportFileName = selectedItem.Name;
            return;
        }

        // Nur im JSON-Modus können Dateien ausgewählt werden
        if (!IsJsonMode)
            return;
        if (!selectedItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return;
        if (selectedItem.RemoteProject == null)
        {
            await this.ShowPopupAsync(new PopupAlert(AppResources.fehler_beim_herunterladen_des_projekts, AppResources.fehler), Settings.PopupOptions);
            return;
        }

        var project = selectedItem.RemoteProject;
        string projectName = project.DisplayName;
        var popup = new PopupDualResponse(string.Format(AppResources.projekt_wirklich_herunterladen, projectName), AppResources.info);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result?.Result is not DualPopupResult.Ok) return;

        try
        {
            await BusyService.ShowAsync(AppResources.projekt_wird_heruntergeladen);
            bool success = await SaveManager.DownloadRemoteProjectAsync(project);
            await BusyService.HideAsync();
            if (success)
            {
                await this.ShowPopupAsync(new PopupAlert(AppResources.projekt_erfolgreich_heruntergeladen, AppResources.info), Settings.PopupOptions);
                await Navigation.PopAsync();
            }
            else
            {
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

    private async void OnBreadcrumbTapped(object? sender, TappedEventArgs e)
    {
        var tappedItem = e.Parameter as BreadcrumbItem ?? (sender as Element)?.BindingContext as BreadcrumbItem;
        if (tappedItem == null || tappedItem.IsLast || _isNavigating)
            return;

        int targetIndex = Breadcrumbs.IndexOf(tappedItem);
        if (targetIndex == -1)
            return;

        _isNavigating = true;
        try
        {
            await BusyService.ShowAsync(AppResources.verzeichnis_wird_geladen);

            if (!await LoadFolderContentAsync(tappedItem.DriveId, tappedItem.Id))
                return;

            while (Breadcrumbs.Count > targetIndex + 1)
                Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
            Breadcrumbs.Last().IsLast = true;
            ScrollBreadcrumbsToRight();
        }
        finally
        {
            await BusyService.HideAsync();
            _isNavigating = false;
        }
    }


    private async void OnSelectFolderClicked(object? sender, EventArgs e)
    {
        if (Breadcrumbs.Count == 0 || string.IsNullOrEmpty(CurrentDriveId))
            return;

        var current = Breadcrumbs.Last();

        // Export-Modus: nur Ordner zurückgeben, Upload macht ExportSettings
        if (IsExportMode)
        {
            string? fileName = NormalizeExportFileName(ExportFileName);
            if (fileName == null)
            {
                await this.ShowPopupAsync(new PopupAlert(AppResources.ungueltiger_dateiname, AppResources.fehler), Settings.PopupOptions);
                return;
            }
            bool exists = CloudItems.Any(i => !i.IsFolder && string.Equals(i.Name, fileName, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                var popup = new PopupDualResponse(string.Format(AppResources.datei_existiert_ueberschreiben, fileName), AppResources.info);
                var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
                if (result?.Result is not DualPopupResult.Ok)
                    return;
            }
            _pickedFolder = new CloudFolderSelection(current.DriveId, current.Id, current.Name, fileName);
            await Navigation.PopAsync();
            CompleteFolderPick();
            return;
        }

        if (!IsFolderMode)
            return;

        bool isSuccess = false;
        try
        {
            await BusyService.ShowAsync(AppResources.projekt_wird_hochgeladen);
            var (success, driveId, folderId) =
                await SaveManager.CreateAndSyncNewCloudProjectAsync(current.DriveId, current.Id);
            isSuccess = success;
            // CloudDriveId/CloudFolderId setzt und speichert CreateAndSyncNewCloudProjectAsync bereits
            // (GlobalJson.SaveToFile). Das fruehere manuelle File.WriteAllText ist daher entfallen.
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

    /// <summary>
    /// Bereinigt den Dateinamen und ergänzt .docx. null = ungültig.
    /// </summary>
    private static string? NormalizeExportFileName(string? input)
    {
        string name = input?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return null;
        char[] invalid = ['"', '*', ':', '<', '>', '?', '/', '\\', '|'];
        if (name.IndexOfAny(invalid) >= 0 || name.EndsWith('.'))
            return null;
        if (!name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            name += ".docx";
        return name;
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
                    await BreadcrumbScrollView.ScrollToAsync(contentWidth - viewportWidth, 0, true);
            }
            catch
            {
                // Fallback falls die Viewport-Berechnung beim ersten Laden hakt
            }
        });
    }

    /// <summary>
    /// Öffnet den Picker im Export-Modus und liefert Ordner + Dateiname zurück
    /// (null = abgebrochen).
    /// </summary>
    public static async Task<CloudFolderSelection?> PickExportFolderAsync(string? defaultFileName = null)
    {
        _folderPickTcs?.TrySetResult(null);
        var tcs = new TaskCompletionSource<CloudFolderSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _folderPickTcs = tcs;
        _pendingExportFileName = defaultFileName ?? string.Empty;
        try
        {
            await Shell.Current.GoToAsync($"cloudPickerPage?mode={CloudPickerMode.SelectExportFolder}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CloudPicker konnte nicht geöffnet werden: {ex}");
            _folderPickTcs = null;
            return null;
        }
        return await tcs.Task;
    }

    private void CompleteFolderPick()
    {
        if (!IsExportMode)
            return;
        var tcs = _folderPickTcs;
        _folderPickTcs = null;
        tcs?.TrySetResult(_pickedFolder);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
            CompleteFolderPick();
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
    public string DriveId { get; set; } = string.Empty;
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

public sealed record CloudFolderSelection(string DriveId, string FolderId, string FolderName, string FileName);
