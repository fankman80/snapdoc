#nullable disable
using Codeuctivity.OpenXmlPowerTools;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Controls;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using static SnapDoc.Models.SyncStampExtensions;

#if WINDOWS
using System.Diagnostics;
#endif

namespace SnapDoc.Views;

public partial class OpenProject : ContentPage
{
    private bool _isProcessing = false;
    private CancellationTokenSource _loadCts;
    private static List<RemoteProjectDto> _remoteCache;
    private static DateTime _remoteCacheTime;
    private static readonly TimeSpan RemoteCacheTtl = TimeSpan.FromMinutes(2);

    public OpenProject()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadJsonFiles();
    }

    private async void LoadJsonFiles()
    {
        // Laufenden Hintergrund-Abgleich des vorherigen Aufrufs stoppen
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        string rootDirectory = Settings.DataDirectory;
        bool isOnline = SaveManager.CurrentAuth?.IsLoggedIn == true;

        // -----------------------------------------------------------------
        // 1. Lokale JSON-Dateien einlesen
        // -----------------------------------------------------------------
        var foundFiles = await Task.Run(() =>
        {
            List<FileItem> items = [];
            try
            {
                var files = Directory.EnumerateFiles(rootDirectory, "*.json", SearchOption.AllDirectories);

                string activeFilePath = !string.IsNullOrWhiteSpace(SettingsService.Instance?.ProjectPath)
                            ? Path.Combine(
                                Settings.DataDirectory,
                                SettingsService.Instance.ProjectPath,
                                SettingsService.DefaultJson)
                            : null;

                foreach (var file in files)
                {
                    if (file.Contains($"{Path.DirectorySeparatorChar}customicons{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                        file.Contains($"{Path.DirectorySeparatorChar}custompins{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string currentFilePath = file;
                    string projectDir = Path.GetDirectoryName(currentFilePath);
                    string thumbPath = "banner_thumbnail.png";
                    string projectName = Path.GetFileNameWithoutExtension(currentFilePath); // Fallback
                    var cachedData = GlobalJson.ReadFromFile(currentFilePath);

                    try
                    {
                        var projectData = cachedData;

                        if (projectData != null && !string.IsNullOrWhiteSpace(projectDir))
                        {
                            // Umbenennungs-Logik: Pruefen ob der Name vom Standard abweicht
                            string currentFileName = Path.GetFileName(currentFilePath);
                            if (!currentFileName.Equals(SettingsService.DefaultJson, StringComparison.OrdinalIgnoreCase))
                            {
                                string newFilePath = Path.Combine(projectDir, SettingsService.DefaultJson);

                                // Nur umbenennen, falls am Ziel nicht schon eine Datei liegt
                                if (!File.Exists(newFilePath))
                                {
                                    File.Move(currentFilePath, newFilePath);
                                    currentFilePath = newFilePath; // Ab hier den neuen Pfad nutzen!
                                }
                            }

                            // Object_name aus der JSON als Anzeigename nutzen
                            if (!string.IsNullOrWhiteSpace(projectData.Object_name))
                                projectName = projectData.Object_name;
                            else
                                projectName = Path.GetFileName(projectDir); // Zweiter Fallback

                            string titleImageName = !string.IsNullOrWhiteSpace(projectData.TitleImage)
                                ? projectData.TitleImage : "banner_thumbnail.png";

                            string thumbnailFolder = !string.IsNullOrWhiteSpace(projectData.ThumbnailPath)
                                ? projectData.ThumbnailPath : "thumbnails";

                            string fullThumbPath = Path.Combine(projectDir, thumbnailFolder, titleImageName);

                            if (File.Exists(fullThumbPath))
                                thumbPath = fullThumbPath;
                        }
                    }
                    catch
                    {
                        // Einzelne defekte JSON ignorieren
                    }

                    items.Add(new FileItem
                    {
                        FileName = projectName,
                        FilePath = currentFilePath,
                        FileDate = File.GetLastWriteTime(currentFilePath),
                        ImagePath = thumbPath,
                        ThumbnailPath = thumbPath,
                        IsActive = currentFilePath == activeFilePath,
                        IsSyncChecked = !isOnline,
                        CachedData = cachedData
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Einlesen der Projekte: {ex.Message}");
            }

            return items
                .OrderByDescending(f => f.FileDate)
                .ToList();
        }, ct);

        if (ct.IsCancellationRequested)
            return;

        // -----------------------------------------------------------------
        // 2. CollectionView sofort anzeigen
        // -----------------------------------------------------------------
        FileListView.ItemsSource = foundFiles;
        ProjectCounterLabel.Text = $"{foundFiles.Count} {AppResources.projekte}";

        // -----------------------------------------------------------------
        // 3. Cloud-Abgleich im Hintergrund
        // -----------------------------------------------------------------
        _ = Task.Run(async () =>
        {
            if (SaveManager.CurrentAuth?.IsLoggedIn != true)
                return;

            try
            {
                var remoteProjects = await GetRemoteProjectsAsync();
                if (remoteProjects == null || ct.IsCancellationRequested)
                    return;

                // O(1)-Lookup statt FirstOrDefault pro Item
                var byFolderId = remoteProjects
                    .Where(rp => !string.IsNullOrWhiteSpace(rp.FolderId))
                    .GroupBy(rp => rp.FolderId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                // =========================================================
                // PHASE A: Nur die Wolken-Entscheidung - ohne Netzwerk
                // =========================================================
                var matched = new List<(FileItem Item, RemoteProjectDto Remote)>();

                foreach (var item in foundFiles)
                {
                    if (ct.IsCancellationRequested) return;

                    var localData = item.CachedData;
                    if (localData == null)
                    {
                        MarkChecked(item, false);
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(localData.CloudFolderId) &&
                        byFolderId.TryGetValue(localData.CloudFolderId, out var rp))
                    {
                        MarkChecked(item, true);              // sofort sichtbar
                        matched.Add((item, rp));
                    }
                    else
                    {
                        matched.Add((item, null));           // in Phase B klaeren
                    }
                }

                // =========================================================
                // PHASE B: Graph-Direktpruefung fuer Projekte ohne Index-Treffer
                // =========================================================
                var unresolved = matched.Where(m => m.Remote == null).ToList();
                var resolved = new System.Collections.Concurrent.ConcurrentBag<(FileItem Item, RemoteProjectDto Remote)>();

                using (var gate = new SemaphoreSlim(4))
                {
                    await Task.WhenAll(unresolved.Select(async m =>
                    {
                        await gate.WaitAsync(ct);
                        try
                        {
                            if (ct.IsCancellationRequested) return;

                            var d = m.Item.CachedData;

                            if (d == null ||
                                string.IsNullOrWhiteSpace(d.CloudDriveId) ||
                                string.IsNullOrWhiteSpace(d.CloudFolderId))
                            {
                                MarkChecked(m.Item, false);
                                return;
                            }

                            try
                            {
                                // Nur die Id abfragen - deutlich kleinere Antwort
                                var folderCheck = await SaveManager.CurrentAuth.GraphClient
                                    .Drives[d.CloudDriveId]
                                    .Items[d.CloudFolderId]
                                    .GetAsync(rc => rc.QueryParameters.Select = ["id"], ct);

                                if (folderCheck != null)
                                {
                                    var rp = new RemoteProjectDto
                                    {
                                        DriveId = d.CloudDriveId,
                                        FolderId = d.CloudFolderId,
                                        FileName = SettingsService.DefaultJson
                                    };

                                    MarkChecked(m.Item, true);
                                    resolved.Add((m.Item, rp));
                                }
                                else
                                {
                                    MarkChecked(m.Item, false);
                                }
                            }
                            catch
                            {
                                // Ordner existiert in der Cloud nicht mehr (404 / NotFound)
                                // -> Verwaiste IDs lokal loeschen
                                d.CloudDriveId = null;
                                d.CloudFolderId = null;

                                await File.WriteAllTextAsync(
                                    m.Item.FilePath,
                                    System.Text.Json.JsonSerializer.Serialize(d, GlobalJson.GetOptions()),
                                    CancellationToken.None);

                                MarkChecked(m.Item, false);
                            }
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }

                if (ct.IsCancellationRequested)
                    return;

                // =========================================================
                // PHASE C: Inhaltsabgleich (Titelbild, Thumbnails)
                // =========================================================
                var toSync = matched.Where(m => m.Remote != null)
                                    .Concat(resolved)
                                    .ToList();

                foreach (var (item, remoteProject) in toSync)
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        if (item.IsActive)
                            continue;

                        var localData = item.CachedData;
                        if (localData == null)
                            continue;

                        string projectDir = Path.GetDirectoryName(item.FilePath);
                        if (string.IsNullOrWhiteSpace(projectDir))
                            continue;

                        // 3.3 Cloud-Verknuepfung aktualisieren
                        bool cloudLinkChanged = localData.CloudDriveId != remoteProject.DriveId ||
                                                localData.CloudFolderId != remoteProject.FolderId;

                        if (cloudLinkChanged)
                        {
                            localData.CloudDriveId = remoteProject.DriveId;
                            localData.CloudFolderId = remoteProject.FolderId;

                            await File.WriteAllTextAsync(
                                item.FilePath,
                                System.Text.Json.JsonSerializer.Serialize(localData, GlobalJson.GetOptions()),
                                CancellationToken.None);
                        }

                        // 3.4 Cloud-JSON lesen
                        var remoteData = await SaveManager.GetRemoteProjectDataAsync(
                                         remoteProject.DriveId,
                                         remoteProject.FolderId,
                                         remoteProject.FileName);

                        if (remoteData == null)
                            continue;

                        string localTitleImage = !string.IsNullOrWhiteSpace(localData.TitleImage)
                                ? localData.TitleImage
                                : "banner_thumbnail.png";

                        string remoteTitleImage = !string.IsNullOrWhiteSpace(remoteData.TitleImage)
                                ? remoteData.TitleImage
                                : "banner_thumbnail.png";

                        // 3.5 Titelbild geaendert?
                        bool titleImageChanged = !localTitleImage.Equals(
                                remoteTitleImage,
                                StringComparison.OrdinalIgnoreCase);

                        if (titleImageChanged)
                        {
                            System.Diagnostics.Debug.WriteLine($"TitleImage geaendert: {item.FileName}: {localTitleImage} -> {remoteTitleImage}");

                            bool downloaded = await Helper.UpdateProjectTitleImageAsync(
                                    localData,
                                    projectDir,
                                    localTitleImage,
                                    remoteTitleImage);

                            if (downloaded)
                            {
                                // Lokale JSON auf den Cloud-Stand bringen
                                using (var _ = SyncStampGate.Suspend())
                                {
                                    localData.TitleImage = remoteTitleImage;
                                    localData.TitleImageSize = remoteData.TitleImageSize;
                                }

                                await File.WriteAllTextAsync(
                                    item.FilePath,
                                    System.Text.Json.JsonSerializer.Serialize(localData, GlobalJson.GetOptions()),
                                    CancellationToken.None);
                            }
                        }
                        else
                        {
                            // 3.6 Name gleich, aber Dateien fehlen?
                            string thumbnailFolder = !string.IsNullOrWhiteSpace(localData.ThumbnailPath)
                                    ? localData.ThumbnailPath
                                    : "thumbnails";

                            string imageFolder = !string.IsNullOrWhiteSpace(localData.ImagePath)
                                    ? localData.ImagePath
                                    : "images";

                            string thumbPath = Path.Combine(projectDir, thumbnailFolder, remoteTitleImage);
                            string imagePath = Path.Combine(projectDir, imageFolder, remoteTitleImage);

                            if (!File.Exists(thumbPath))
                            {
                                await SaveManager.DownloadMediaOnDemandAsync(
                                    fileName: remoteTitleImage,
                                    subFolder: thumbnailFolder,
                                    driveId: remoteProject.DriveId,
                                    folderId: remoteProject.FolderId,
                                    projectDir: projectDir);
                            }

                            if (!File.Exists(imagePath))
                            {
                                await SaveManager.DownloadMediaOnDemandAsync(
                                    fileName: remoteTitleImage,
                                    subFolder: imageFolder,
                                    driveId: remoteProject.DriveId,
                                    folderId: remoteProject.FolderId,
                                    projectDir: projectDir);
                            }
                        }

                        // 3.7 CollectionView aktualisieren (nur bei tatsaechlicher Aenderung)
                        string finalThumbnailFolder = !string.IsNullOrWhiteSpace(localData.ThumbnailPath)
                                ? localData.ThumbnailPath
                                : "thumbnails";

                        string finalThumbPath = Path.Combine(projectDir, finalThumbnailFolder, remoteTitleImage);

                        if (File.Exists(finalThumbPath) &&
                            !string.Equals(item.ThumbnailPath, finalThumbPath, StringComparison.OrdinalIgnoreCase))
                        {
                            // Pfadwechsel pruefen, um unnoetiges Neuladen (Blinken) zu verhindern
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                item.ImagePath = null;
                                item.ThumbnailPath = null;
                                await Task.Delay(50);
                                item.ImagePath = finalThumbPath;
                                item.ThumbnailPath = finalThumbPath;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Cloud-Abgleich fuer '{item.FileName}' fehlgeschlagen: {ex}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Liste wurde neu geladen - stiller Abbruch
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud-Sync fehlgeschlagen: {ex}");
            }
            finally
            {
                // Sicherheitsnetz: nichts darf dauerhaft gesperrt bleiben
                if (!ct.IsCancellationRequested)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var item in foundFiles)
                            item.IsSyncChecked = true;
                    });
                }

                // Cache freigeben - wird nach dem Abgleich nicht mehr gebraucht
                foreach (var item in foundFiles)
                    item.CachedData = null;
            }
        }, ct);
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(desc: AppResources.neues_projekt_eroeffnen,
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);

        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result?.Result == null) return;

        // Eingabe saeubern
        string sanitizedName = SanitizeFileName(result.Result);

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            await SnackbarExtensions.ShowSafeAsync(AppResources.invalid_project_name, includeDelay: true);
            return;
        }

        // Pruefe, ob die Datei existiert und haenge fortlaufend eine Nummer an
        int counter = 1;
        string _result = sanitizedName;

        while (Directory.Exists(Path.Combine(Settings.DataDirectory, _result)))
        {
            _result = $"{sanitizedName} ({counter})";
            counter++;
        }

        string filePath = Path.Combine(Settings.DataDirectory, _result, SettingsService.DefaultJson);

        SaveManager.ResetCloudSync();
        LoadDataToView.ResetData();

        GlobalJson.CreateNewFile(filePath);
        GlobalJson.LoadFromFile(filePath);

        GlobalJson.Data.Client_name = "";
        GlobalJson.Data.Object_address = "";
        GlobalJson.Data.Working_title = "";
        GlobalJson.Data.Project_nr = "";
        GlobalJson.Data.Object_name = result.Result;
        GlobalJson.Data.Creation_date = DateTime.Now;
        GlobalJson.Data.Project_manager = "";
        GlobalJson.Data.PlanPath = "plans";
        GlobalJson.Data.ImagePath = "images";
        GlobalJson.Data.ThumbnailPath = "thumbnails";
        GlobalJson.Data.CustomPinsPath = "custompins";
        GlobalJson.Data.TitleImage = "banner_thumbnail.png";

        SettingsService.Instance.IsProjectLoaded = true;
        SettingsService.Instance.ProjectPath = _result; 

        LoadDataToView.LoadData(new FileResult(filePath));
        ProjectItem.Current.Attach(GlobalJson.Data);
        SaveManager.NotifyDataChanged();
        LoadJsonFiles();
        await Shell.Current.GoToAsync("project_details");

#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = false;
#endif
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        string cleanName = string.Concat(fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();

        cleanName = cleanName.Replace("/", "_").Replace("\\", "_").Replace("$", "").Replace("{", "").Replace("}", "");

        if (cleanName.Length > 100)
            cleanName = cleanName[..100];

        return cleanName;
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.bitte_waehle_zip
            });

            if (fileResult == null)
                return;

            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.projekt_wird_importiert);

            var targetDirectory = Settings.DataDirectory;

            await Task.Run(async () =>
            {
                string tempZipPath = Path.Combine(FileSystem.CacheDirectory, fileResult.FileName);

                try
                {
                    using (var stream = await fileResult.OpenReadAsync())
                    using (var localStream = File.Create(tempZipPath))
                    {
                        await stream.CopyToAsync(localStream);
                    }

                    Helper.UnpackDirectory(tempZipPath, targetDirectory);
                }
                finally
                {
                    if (File.Exists(tempZipPath))
                        File.Delete(tempZipPath);
                }
            });

            LoadJsonFiles();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Import-Fehler: {ex.Message}");
            await SnackbarExtensions.ShowSafeAsync(AppResources.datei_konnte_nicht_importiert_werden, includeDelay: true);
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    private async void OnProjectClicked(object sender, TappedEventArgs e)
    {
        // Sperre pruefen: Wenn bereits ein Projekt geladen wird, Klick ignorieren!
        if (_isProcessing)
            return;

        var layout = sender as BindableObject;

        if (layout?.BindingContext is not FileItem item)
            return;

        // Wenn das Projekt bereits aktiv ist, nichts tun.
        if (item.IsActive)
            return;

        // Erst nach abgeschlossenem Cloud-Check oeffnen
        if (!item.IsSyncChecked)
            return;

        // Sperre aktivieren
        _isProcessing = true;

        // Ladeanzeige aktivieren
        await BusyService.ShowAsync(AppResources.projekt_wird_geladen);

        try
        {
            await Task.Delay(150);

            SaveManager.ResetCloudSync();

            if (FileListView.ItemsSource is IEnumerable<FileItem> items)
            {
                foreach (var f in items)
                {
                    f.IsActive = false;
                }
                item.IsActive = true;
            }

            SettingsService.Instance.IsProjectLoaded = true;
            LoadDataToView.ResetData();

            // Laedt die Datei, zieht fehlende Sync-Stempel nach und
            // entfernt abgelaufene Tombstones.
            SaveManager.Initialize(item.FilePath);

            if (SaveManager.CurrentAuth?.IsLoggedIn == true)
            {
                await BusyService.ShowAsync(AppResources.daten_werden_synchronisiert);

                // Merged direkt in GlobalJson.Data und speichert anschliessend -
                // kein erneutes LoadFromFile noetig.
                await SaveManager.SyncJsonOnlyFromCloudAsync();

                await BusyService.ShowAsync(AppResources.projekt_wird_geladen);
            }

            LoadDataToView.LoadData(new FileResult(item.FilePath));
            ProjectItem.Current.Attach(GlobalJson.Data);

            if (GlobalJson.Data.Plans != null)
            {
                var repairCount = false;

                foreach (var plan in SyncOps.LivePlans(GlobalJson.Data))
                {
                    int live = SyncOps.LivePinCount(plan.Value);

                    if (plan.Value.PinCount != live)
                    {
                        plan.Value.PinCount = live;
                        repairCount = true;
                    }
                }

                if (repairCount)
                    SaveManager.NotifyDataChanged();
            }

            // Overlay vor dem Shell-Seitenwechsel schliessen.
            await BusyService.HideAsync();

            await Shell.Current.GoToAsync("project_details");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cloud Sync oder Lade-Fehler: {ex}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await this.ShowPopupAsync(new PopupAlert(AppResources.projekt_konnte_nicht_geladen_werden, AppResources.fehler), Settings.PopupOptions);
            });
        }
        finally
        {
            await BusyService.HideAsync();
            _isProcessing = false;
        }
    }

    private async void OnDownloadFromCloudClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("cloudPickerPage?mode=SelectJsonFile");
    }

    /// <summary>
    /// Entfernt die Tile-Cache-Ordner aller Plaene eines Projekts.
    /// </summary>
    private static void DeleteTileCacheForProject()
    {
        string cacheDir = Path.Combine(FileSystem.AppDataDirectory, "Tiles");

        if (!Directory.Exists(cacheDir) || GlobalJson.Data?.Plans == null)
            return;

        foreach (var plan in GlobalJson.Data.Plans)
        {
            // Webmap-Plaene haben keine Datei. Ohne diese Pruefung wuerde
            // das Suchmuster zu "**" und ALLE Tile-Ordner samt denen
            // anderer Projekte geloescht.
            if (string.IsNullOrWhiteSpace(plan.Value.File))
                continue;

            string baseFileName = Path.GetFileNameWithoutExtension(plan.Value.File).Replace("_r", "");

            if (string.IsNullOrWhiteSpace(baseFileName))
                continue;

            string searchPattern = $"*{baseFileName}*";

            foreach (var dir in Directory.GetDirectories(cacheDir, searchPattern))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>
    /// Setzt Wolkensymbol und Freigabe-Flag eines Projekts auf dem UI-Thread.
    /// </summary>
    private static void MarkChecked(FileItem item, bool hasCloud) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            item.HasCloudSync = hasCloud;
            item.IsSyncChecked = true;
        });

    /// <summary>
    /// Liefert den Cloud-Projektindex, innerhalb der TTL aus dem Cache.
    /// force = true nach Upload/Loeschen, um den Cache zu umgehen.
    /// </summary>
    private static async Task<List<RemoteProjectDto>> GetRemoteProjectsAsync(bool force = false)
    {
        if (!force && _remoteCache != null && DateTime.UtcNow - _remoteCacheTime < RemoteCacheTtl)
            return _remoteCache;

        _remoteCache = await SaveManager.SearchRemoteProjectsAsync();
        _remoteCacheTime = DateTime.UtcNow;
        return _remoteCache;
    }

    /// <summary>
    /// Verwirft den Index-Cache - nach Upload oder Projektloeschung aufrufen.
    /// </summary>
    private static void InvalidateRemoteCache() => _remoteCache = null;

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
            var button = sender as Button;

            if (button?.BindingContext is not FileItem item)
                return;

            var popup1 = new PopupDualResponse(AppResources.wollen_sie_dieses_projekt_wirklich_loeschen, okText: AppResources.loeschen, alert: true);
            var result1 = await this.ShowPopupAsync<DualPopupResult>(popup1, Settings.PopupOptions);

            if (result1.Result is not DualPopupResult.Ok)
                return;

            string fullPath = item.FilePath;

            if (string.IsNullOrEmpty(fullPath))
                return;

            // Nicht der Dateiname entscheidet, sondern ob es das geladene Projekt ist
            bool isCurrentProject = item.IsActive;

            // Tile-Cache nur loeschen, solange GlobalJson.Data noch das Projekt haelt
            if (isCurrentProject)
                DeleteTileCacheForProject();

            // Loesche das Projektverzeichnis und alle enthaltenen Dateien
            string projectDirectoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(projectDirectoryPath) && Directory.Exists(projectDirectoryPath))
                Directory.Delete(projectDirectoryPath, true);

            // Aktives Projekt entladen - aber auf der Projektliste bleiben
            if (isCurrentProject)
            {
                SaveManager.ResetCloudSync();
                SettingsService.Instance.IsProjectLoaded = false;
                SettingsService.Instance.ProjectPath = null;
                LoadDataToView.ResetData();
                GlobalJson.Data = new JsonDataModel();
                GlobalJson.UpdateFilePath(null);
                ProjectItem.Current.Attach(GlobalJson.Data);
            }

            InvalidateRemoteCache();
            LoadJsonFiles();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_isProcessing)
            return;

        _isProcessing = true;

        try
        {
            var button = sender as Button;

            if (button?.BindingContext is not FileItem item)
                return;

            var _popup = new PopupProjectEdit();
            var _result = await this.ShowPopupAsync<string>(_popup, Settings.PopupOptions);

            if (_result == null || string.IsNullOrEmpty(_result.Result))
                return;

            switch (_result.Result)
            {
                case "Zip":
                    await Task.Delay(200);

                    var popup2 = new PopupDualResponse(AppResources.wollen_sie_projekt_als_zip_exportieren);
                    var result2 = await this.ShowPopupAsync<DualPopupResult>(popup2, Settings.PopupOptions);

                    if (result2.Result is DualPopupResult.Ok)
                    {
                        string sourceDirectory = Path.GetDirectoryName(item.FilePath);
                        string outputPath = Path.Combine(Settings.DataDirectory, Path.GetFileNameWithoutExtension(item.FileName) + ".zip");

                        try
                        {
                            // Ladeanzeige aktivieren
                            await BusyService.ShowAsync(AppResources.daten_werden_komprimiert);

                            // Hintergrundoperation
                            await Task.Run(() => { Helper.PackDirectory(sourceDirectory, outputPath); });
                        }
                        finally
                        {
                            // Ladeanzeige deaktivieren
                            await BusyService.HideAsync();
                            await Task.Delay(100);
                        }

                        if (File.Exists(outputPath))
                        {
                            using (var saveStream = File.Open(outputPath, FileMode.Open))
                            {
                                var fileSaveResult = await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(item.FileName) + ".zip", saveStream);

                                if (fileSaveResult.IsSuccessful)
                                    await SnackbarExtensions.ShowSafeAsync(AppResources.zip_wurde_exportiert, includeDelay: true);
                            }

                            File.Delete(outputPath);
                        }
                    }
                    break;

                case "Folder":
                    var directoryPath = Path.GetDirectoryName(Path.Combine(Settings.DataDirectory, item.FilePath));

                    if (Directory.Exists(directoryPath))
                    {
#if WINDOWS
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = directoryPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
#endif
                    }
                    break;

                case "Upload":
                    if (SaveManager.CurrentAuth == null || !SaveManager.CurrentAuth.IsLoggedIn)
                    {
                        await this.ShowPopupAsync(new PopupAlert(AppResources.bitte_zuerst_anmelden, AppResources.info), Settings.PopupOptions);
                        return;
                    }

                    if (item.HasCloudSync)
                    {
                        await SnackbarExtensions.ShowSafeAsync("Das Projekt ist bereits synchronisiert.", includeDelay: true);
                        return; 
                    }

                    if (FileListView.ItemsSource is IEnumerable<FileItem> items)
                    {
                        foreach (var f in items)
                            f.IsActive = false;
                    }

                    item.IsActive = true;
                    SettingsService.Instance.IsProjectLoaded = true;
    
                    SettingsService.Instance.ProjectPath = Path.GetFileName(Path.GetDirectoryName(item.FilePath));

                    LoadDataToView.ResetData();
                    SaveManager.Initialize(item.FilePath);
                    LoadDataToView.LoadData(new FileResult(item.FilePath));
                    ProjectItem.Current.Attach(GlobalJson.Data);
                    InvalidateRemoteCache();

                    await Shell.Current.GoToAsync("cloudPickerPage?mode=SelectFolder");
                    break;
            }
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"Error: {ex.Message}", includeDelay: true);
        }
        finally
        {
            await BusyService.HideAsync();
            _isProcessing = false;
        }
    }
}
