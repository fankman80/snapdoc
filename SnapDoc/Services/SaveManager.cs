using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graph;
using SnapDoc.Messages;
using SnapDoc.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace SnapDoc.Services;

public static class SaveManager
{
    private static readonly ConcurrentDictionary<(string LocalFilePath, string SubFolder), byte> _pendingUploadQueue = new();
    private static CancellationTokenSource? _pollingCts;
    public static string? TargetFolderId { get; set; }
    private static CancellationTokenSource? _debounceCts;
    private static DateTime _lastKnownWriteTime;
    private static readonly Lock _fileLock = new();
    private static string CloudFileName => GlobalJson.Data?.JsonFile ?? "snapdoc_data.json";
    private static DateTimeOffset _lastKnownCloudSyncTime = DateTimeOffset.MinValue;
    private static string? _lastKnownETag;

    public static AuthService? CurrentAuth { get; set; }
    public static void Initialize(string filePath)
    {
        GlobalJson.LoadFromFile(filePath);
        if (File.Exists(filePath))
        {
            _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
        }
    }

    // Standard-Aufruf ohne Dateien (nur JSON sync)
    public static void NotifyDataChanged(int delayMilliseconds = 2000)
    {
        NotifyDataChanged([], delayMilliseconds);
    }

    // Komfort-Überladung für eine einzelne Datei
    public static void NotifyDataChanged(string localFilePath, string subFolder, int delayMilliseconds = 2000)
    {
        NotifyDataChanged([(localFilePath, subFolder)], delayMilliseconds);
    }

    // Hauptmethode für mehrere Dateien gleichzeitig
    public static void NotifyDataChanged(IEnumerable<(string LocalFilePath, string SubFolder)> files, int delayMilliseconds = 2000)
    {
        foreach (var (localFilePath, subFolder) in files)
        {
            if (!string.IsNullOrEmpty(localFilePath))
                _pendingUploadQueue.TryAdd((localFilePath, subFolder), 0);
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMilliseconds, token);
                if (!token.IsCancellationRequested)
                {
                    await SaveWithSyncCheckAsync();
                }
            }
            catch (OperationCanceledException) { /* Debounce abgebrochen */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim verzoegerten Speichern: {ex.Message}");
            }
        }, token);
    }

    // Synchronisiert die aktuelle Datei mit einem bestehenden Ordner in der Cloud.
    public static async Task<bool> SyncWithExistingFolderAsync(string parentFolderId)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        try
        {
            TargetFolderId = parentFolderId;
            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync(); // Drive laden
            string driveId = myDrive!.Id!;

            await EnsureProjectSubfoldersAsync(CurrentAuth, driveId, TargetFolderId);

            // FEHLENDE IDs ZUWEISEN:
            if (GlobalJson.Data != null)
            {
                GlobalJson.Data.CloudDriveId = driveId;
                GlobalJson.Data.CloudFolderId = TargetFolderId;
            }

            await SaveWithSyncCheckAsync();

            // Lokales Projektverzeichnis ebenfalls rekursiv hochladen
            string localJsonPath = GlobalJson.GetFilePath();
            string? localProjectDir = Path.GetDirectoryName(localJsonPath);
            if (!string.IsNullOrEmpty(localProjectDir))
            {
                await UploadDirectoryRecursiveAsync(driveId, TargetFolderId, localProjectDir);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Synchronisieren: {ex.Message}");
            return false;
        }
    }

    // Erstellt ein neues Projektverzeichnis inklusive Unterordnern im gewählten Cloud-Ordner und lädt die aktuelle JSON-Datei hoch.
    public static async Task<bool> CreateAndSyncNewCloudProjectAsync(string parentFolderId)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        try
        {
            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive?.Id == null) return false;

            string projectName = GlobalJson.Data?.ProjectPath ?? "NeuesProjekt";

            // Erstelle das Projektverzeichnis im ausgewählten Ordner
            var projectFolder = await GetOrCreateFolderAsync(CurrentAuth, myDrive.Id, parentFolderId, projectName);
            if (projectFolder?.Id == null) return false;

            TargetFolderId = projectFolder.Id;
            await EnsureProjectSubfoldersAsync(CurrentAuth, myDrive.Id, TargetFolderId);

            if (GlobalJson.Data != null)
            {
                GlobalJson.Data.CloudDriveId = myDrive.Id;
                GlobalJson.Data.CloudFolderId = TargetFolderId;
            }

            // 1. JSON speichern
            await SaveWithSyncCheckAsync();

            // 2. NEU: Alle lokalen Mediendateien (Bilder, Pläne etc.) in die Cloud hochladen
            string localJsonPath = GlobalJson.GetFilePath();
            string? localProjectDir = Path.GetDirectoryName(localJsonPath);
            if (!string.IsNullOrEmpty(localProjectDir))
            {
                await UploadDirectoryRecursiveAsync(myDrive.Id, TargetFolderId, localProjectDir);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen des Cloud-Projekts: {ex.Message}");
            return false;
        }
    }

    public static async Task SaveWithSyncCheckAsync()
    {
        string filePath = GlobalJson.GetFilePath();
        if (string.IsNullOrEmpty(filePath)) return;

        lock (_fileLock)
        {
            if (File.Exists(filePath))
            {
                DateTime currentDiskTime = File.GetLastWriteTimeUtc(filePath);
                if (currentDiskTime > _lastKnownWriteTime)
                    ResolveConflictAndMerge(filePath);
            }

            GlobalJson.SaveToFile();
            _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
        }

        if (CurrentAuth != null && CurrentAuth.IsLoggedIn && CurrentAuth.GraphClient != null)
        {
            // FIX: Verhindert ungewollte Uploads. Nur synchronisieren, wenn explizit verknüpft!
            if (GlobalJson.Data == null ||
                string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
                string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
            {
                return;
            }

            try
            {
                string driveId = GlobalJson.Data.CloudDriveId;
                string targetFolderId = GlobalJson.Data.CloudFolderId;
                try
                {
                    var cloudItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                        .ItemWithPath(CloudFileName)
                        .GetAsync();

                    if (cloudItem?.LastModifiedDateTime != null && cloudItem.LastModifiedDateTime > _lastKnownCloudSyncTime)
                    {
                        // Jemand anderes hat die Datei verändert! Herunterladen und mergen.
                        var cloudStream = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                            .ItemWithPath(CloudFileName)
                            .Content
                            .GetAsync();

                        if (cloudStream != null)
                        {
                            var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(cloudStream, GlobalJson.GetOptions());
                            if (cloudData != null)
                            {
                                lock (_fileLock)
                                {
                                    MergeModels(GlobalJson.Data, cloudData);
                                    GlobalJson.SaveToFile();
                                    _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
                                }
                            }
                        }
                    }
                }
                catch (Microsoft.Graph.Models.ODataErrors.ODataError) { /* Existiert nicht */ }

                string json = GlobalJson.ToJson();
                byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(json);
                using var stream = new MemoryStream(byteArray);

                try
                {
                    var uploadedItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                        .ItemWithPath(CloudFileName)
                        .Content
                        .PutAsync(stream, requestConfig =>
                        {
                            if (!string.IsNullOrEmpty(_lastKnownETag))
                                requestConfig.Headers.Add("If-Match", _lastKnownETag);
                        });

                    if (uploadedItem != null)
                    {
                        _lastKnownCloudSyncTime = uploadedItem.LastModifiedDateTime ?? DateTimeOffset.UtcNow;
                        _lastKnownETag = uploadedItem.ETag;
                    }
                }
                catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 412 || ex.Error?.Code == "conditionNotMet")
                {
                    Console.WriteLine("Konflikt beim Upload! ETag stimmt nicht mehr überein.");
                    await SaveWithSyncCheckAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud-Upload fehlgeschlagen: {ex.Message}");
            }
        }

        // Nach dem JSON-Sync alle angesammelten Dateien im Hintergrund abarbeiten
        if (!_pendingUploadQueue.IsEmpty && CurrentAuth != null && CurrentAuth.IsLoggedIn)
        {
            var keys = _pendingUploadQueue.Keys.ToList();
            foreach (var fileItem in keys)
            {
                // Entfernt genau dieses Element atomar aus der Queue
                if (_pendingUploadQueue.TryRemove(fileItem, out _))
                {
                    if (File.Exists(fileItem.LocalFilePath))
                        await UploadSingleFileAsync(fileItem.LocalFilePath, fileItem.SubFolder);
                }
            }
        }
    }

    public static async Task LoadDataAsync(AuthService authService, string localFilePath)
    {
        ResetCloudSync();

        GlobalJson.LoadFromFile(localFilePath);

        if (authService.IsLoggedIn && authService.GraphClient != null)
        {
            try
            {
                var myDrive = await authService.GraphClient.Me.Drive.GetAsync();

                if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
                {
                    string projectName = GlobalJson.Data?.ProjectPath ?? "DefaultProject";
                    string? targetFolderId = await EnsureCloudFolderStructureAsync(authService, myDrive.Id, projectName);

                    if (!string.IsNullOrEmpty(targetFolderId))
                    {
                        // Datei-Infos abrufen (für den Zeitstempel)
                        var cloudItem = await authService.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                            .ItemWithPath(CloudFileName)
                            .GetAsync();

                        // Datei-Inhalt abrufen
                        var stream = await authService.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                            .ItemWithPath(CloudFileName)
                            .Content
                            .GetAsync();

                        if (stream != null && cloudItem != null)
                        {
                            var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(
                                stream,
                                GlobalJson.GetOptions()
                            );

                            if (cloudData != null)
                            {
                                GlobalJson.Data = cloudData;
                                GlobalJson.UpdateFilePath(localFilePath);
                                GlobalJson.SaveToFile();

                                _lastKnownWriteTime = File.GetLastWriteTimeUtc(localFilePath);

                                // Zeitstempel für den nächsten Sync-Check merken
                                if (cloudItem.LastModifiedDateTime != null)
                                {
                                    _lastKnownCloudSyncTime = cloudItem.LastModifiedDateTime.Value;
                                    _lastKnownETag = cloudItem.ETag;
                                }
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud-Laden fehlgeschlagen, nutze lokale Datei weiter: {ex.Message}");
            }
        }
    }

    private static void ResolveConflictAndMerge(string filePath)
    {
        try
        {
            string externalJson = File.ReadAllText(filePath);
            var externalData = JsonSerializer.Deserialize<JsonDataModel>(externalJson, GlobalJson.GetOptions());

            if (externalData != null)
                MergeModels(GlobalJson.Data, externalData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Mergen: {ex.Message}");
        }
    }

    public static void MergeModels(JsonDataModel local, JsonDataModel cloud)
    {
        if (cloud == null) return;

        bool titleImageChanged = local.TitleImage != cloud.TitleImage;

        // Projektdetails vergleichen und uebertragen
        bool projectDetailsChanged = false;
        if (local.Client_name != cloud.Client_name ||
            local.Working_title != cloud.Working_title ||
            local.Object_address != cloud.Object_address ||
            local.Project_nr != cloud.Project_nr ||
            local.Object_name != cloud.Object_name ||
            local.Project_manager != cloud.Project_manager ||
            local.Creation_date != cloud.Creation_date ||
            titleImageChanged) // <-- Hier ergaenzt
        {
            local.Client_name = cloud.Client_name;
            local.Working_title = cloud.Working_title;
            local.Object_address = cloud.Object_address;
            local.Project_nr = cloud.Project_nr;
            local.Object_name = cloud.Object_name;
            local.Project_manager = cloud.Project_manager;
            local.Creation_date = cloud.Creation_date;

            projectDetailsChanged = true;
        }

        if (titleImageChanged)
        {
            // 1. Alten Dateinamen sichern, BEVOR das Modell überschrieben wird
            string oldImage = GlobalJson.Data.TitleImage;
            string newImage = cloud.TitleImage;

            // 2. Speicher im Modell aktualisieren
            local.TitleImage = newImage;
            local.TitleImageSize = cloud.TitleImageSize;

            // 3. Messenger informieren (AppShell löscht alt, lädt neu & aktualisiert UI)
            _ = Task.Run(() =>
            {
                WeakReferenceMessenger.Default.Send(new TitleImageChangedMessage(oldImage, newImage));
            });
        }

        if (cloud.Plans == null)
        {
            if (projectDetailsChanged)
                WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));
            return;
        }

        local.Plans ??= [];
        bool planStructureChanged = false; // Nur fuer Hinzufuegen/Loeschen von Plaenen

        // Geloeschte Plaene entfernen (Strukturaenderung)
        var deletedPlanIds = local.Plans.Keys.Except(cloud.Plans.Keys).ToList();
        if (deletedPlanIds.Count > 0)
        {
            foreach (var deletedId in deletedPlanIds)
            {
                local.Plans.Remove(deletedId);
            }
            planStructureChanged = true;
        }

        foreach (var cloudPlanKp in cloud.Plans)
        {
            var planId = cloudPlanKp.Key;
            var cloudPlan = cloudPlanKp.Value;

            // Neue Plaene hinzufuegen (Strukturaenderung)
            if (!local.Plans.TryGetValue(planId, out Plan? localPlan))
            {
                local.Plans.Add(planId, cloudPlan);
                planStructureChanged = true;
                continue;
            }

            // Plan-Eigenschaften abgleichen
            bool nameOrExportChanged = localPlan.Name != cloudPlan.Name || localPlan.AllowExport != cloudPlan.AllowExport;
            bool colorChanged = localPlan.PlanColor != cloudPlan.PlanColor;
            bool detailsChanged = localPlan.Description != cloudPlan.Description ||
                                  localPlan.IsGrayscale != cloudPlan.IsGrayscale ||
                                  colorChanged ||
                                  nameOrExportChanged;

            if (detailsChanged ||
                localPlan.File != cloudPlan.File ||
                localPlan.ImageSize != cloudPlan.ImageSize)
            {
                localPlan.Name = cloudPlan.Name;
                localPlan.File = cloudPlan.File;
                localPlan.Description = cloudPlan.Description;
                localPlan.ImageSize = cloudPlan.ImageSize;
                localPlan.IsGrayscale = cloudPlan.IsGrayscale;
                localPlan.PlanColor = cloudPlan.PlanColor;
                localPlan.AllowExport = cloudPlan.AllowExport;

                // Bei Namens- oder Exportaenderung direkt das UI-Element aktualisieren (ohne Shell-Reload)
                if (nameOrExportChanged || colorChanged)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (Shell.Current is AppShell appShell)
                        {
                            var item = appShell.AllPlanItems.FirstOrDefault(p => p.PlanId == planId);
                            if (item != null)
                            {
                                item.Title = cloudPlan.Name;
                                item.AllowExport = cloudPlan.AllowExport;
                                item.PlanColor = cloudPlan.PlanColor;
                            }
                        }
                    });
                }
                if (detailsChanged)
                    WeakReferenceMessenger.Default.Send(new PlanDetailsChangedMessage((planId, cloudPlan.Name, cloudPlan.Description, cloudPlan.IsGrayscale, cloudPlan.PlanColor)));
            }

            localPlan.Pins ??= [];

            // Geloeschte Pins entfernen
            var deletedPinIds = localPlan.Pins.Keys.Except(cloudPlan.Pins?.Keys ?? Enumerable.Empty<string>()).ToList();
            foreach (var deletedId in deletedPinIds)
            {
                localPlan.Pins.Remove(deletedId);
                WeakReferenceMessenger.Default.Send(new PinDeletedMessage(deletedId));
            }

            // Neue oder geaenderte Pins verarbeiten
            if (cloudPlan.Pins != null)
            {
                foreach (var cloudPinKp in cloudPlan.Pins)
                {
                    var pinId = cloudPinKp.Key;
                    var cloudPin = cloudPinKp.Value;

                    if (!localPlan.Pins.TryGetValue(pinId, out Pin? localPin))
                    {
                        localPlan.Pins.Add(pinId, cloudPin);
                        WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
                    }
                    else
                    {
                        // Visuelle Eigenschaften pruefen (loest Canvas-Redraw aus)
                        bool uiNeedsRedraw = localPin.Pos != cloudPin.Pos ||
                                             localPin.PinRotation != cloudPin.PinRotation ||
                                             localPin.PinIcon != cloudPin.PinIcon ||
                                             localPin.PinColor != cloudPin.PinColor ||
                                             localPin.PinScale != cloudPin.PinScale ||
                                             localPin.IsLockAutoScale != cloudPin.IsLockAutoScale ||
                                             localPin.IsLockRotate != cloudPin.IsLockRotate;

                        // ALLE Daten synchronisieren
                        localPin.Anchor = cloudPin.Anchor;
                        localPin.DateTime = cloudPin.DateTime;
                        localPin.IsWebMapPin = cloudPin.IsWebMapPin;
                        localPin.IsCustomPin = cloudPin.IsCustomPin;
                        localPin.Pos = cloudPin.Pos;
                        localPin.PinPriority = cloudPin.PinPriority;
                        localPin.Fotos = cloudPin.Fotos;
                        localPin.GeoLocation = cloudPin.GeoLocation;
                        localPin.IsAllowExport = cloudPin.IsAllowExport;
                        localPin.IsLockAutoScale = cloudPin.IsLockAutoScale;
                        localPin.IsLockPosition = cloudPin.IsLockPosition;
                        localPin.IsLockRotate = cloudPin.IsLockRotate;
                        localPin.OnPlanId = cloudPin.OnPlanId;
                        localPin.PinColor = cloudPin.PinColor;
                        localPin.PinIcon = cloudPin.PinIcon;
                        localPin.PinName = cloudPin.PinName;
                        localPin.PinDesc = cloudPin.PinDesc;
                        localPin.Size = cloudPin.Size;
                        localPin.SelfId = cloudPin.SelfId;
                        localPin.PinScale = cloudPin.PinScale;
                        localPin.PinLocation = cloudPin.PinLocation;
                        localPin.PinRotation = cloudPin.PinRotation;

                        if (uiNeedsRedraw)
                        {
                            WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinId));
                        }
                    }
                }
            }
        } // Schliesst "foreach (var cloudPlanKp in cloud.Plans)"

        // UI-Benachrichtigungen feuern
        if (projectDetailsChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));

        // Nur bei echter Strukturaenderung (Plaene hinzugefuegt oder geloescht) ein Shell-Reload ausloesen
        if (planStructureChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.PlanListUpdated));
    }

    private static async Task<string?> EnsureCloudFolderStructureAsync(AuthService authService, string driveId, string projectName)
    {
        try
        {
            // Basisordner "SnapDoc" im OneDrive Root prüfen/erstellen
            var snapDocItem = await GetOrCreateFolderAsync(authService, driveId, "root", "SnapDoc");
            if (snapDocItem == null || string.IsNullOrEmpty(snapDocItem.Id)) return null;

            // Projektordner innerhalb von "SnapDoc" prüfen/erstellen
            var projectItem = await GetOrCreateFolderAsync(authService, driveId, snapDocItem.Id, projectName);
            return projectItem?.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen der Ordnerstruktur: {ex.Message}");
            return null;
        }
    }

    private static async Task<Microsoft.Graph.Models.DriveItem?> GetOrCreateFolderAsync(AuthService authService, string driveId, string parentId, string folderName)
    {
        if (authService?.GraphClient == null) return null;

        try
        {
            var childrenResponse = await authService.GraphClient.Drives[driveId].Items[parentId].Children.GetAsync();
            var childrenList = childrenResponse?.Value;

            if (childrenList != null)
            {
                var existingFolder = childrenList.FirstOrDefault(f =>
                    f?.Name != null &&
                    f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase) &&
                    f.Folder != null);

                if (existingFolder != null)
                {
                    return existingFolder;
                }
            }
        }
        catch { /* Falls der Ordner nicht gefunden wird, wird er neu erstellt */ }

        var newFolder = new Microsoft.Graph.Models.DriveItem
        {
            Name = folderName,
            Folder = new Microsoft.Graph.Models.Folder()
        };

        return await authService.GraphClient.Drives[driveId].Items[parentId].Children.PostAsync(newFolder);
    }

    private static async Task EnsureProjectSubfoldersAsync(AuthService authService, string driveId, string projectFolderId)
    {
        string[] subfolders = [
            "images",
            "images/originals",
            "plans",
            "plans/thumbnails",
            "thumbnails",
        GlobalJson.Data?.CustomPinsPath ?? "custompins"
        ];

        foreach (var subfolder in subfolders)
        {
            var parts = subfolder.Split('/');
            string currentParentId = projectFolderId;

            foreach (var part in parts)
            {
                var folderItem = await GetOrCreateFolderAsync(authService, driveId, currentParentId, part);
                if (folderItem != null && !string.IsNullOrEmpty(folderItem.Id))
                    currentParentId = folderItem.Id;
                else
                    break;
            }
        }
    }

    // Verknüpft das aktuelle Projekt mit einem SharePoint/OneDrive-Ordner und speichert die IDs in der JSON.
    public static async Task LinkProjectToCloudAsync(string driveId, string folderId)
    {
        if (GlobalJson.Data == null) return;

        GlobalJson.Data.CloudDriveId = driveId;
        GlobalJson.Data.CloudFolderId = folderId;

        await SaveWithSyncCheckAsync();
    }

    // Durchsucht den gesamten SharePoint/OneDrive des Nutzers nach SnapDoc-Projekten.
    public static async Task<List<RemoteProjectDto>> SearchRemoteProjectsAsync()
    {
        var results = new List<RemoteProjectDto>();
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return results;

        try
        {
            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive?.Id == null)
                return results;

            // Sucht nach allen *.json Dateien im Laufwerk
            var searchResponse = await CurrentAuth.GraphClient.Drives[myDrive.Id]
                .SearchWithQ(".json")
                .GetAsSearchWithQGetResponseAsync();

            if (searchResponse?.Value == null)
                return results;

            foreach (var item in searchResponse.Value)
            {
                // Nur JSONs mit Name und gültigem Elternelement berücksichtigen
                if (!string.IsNullOrEmpty(item.Name) && item.ParentReference?.Id != null)
                {
                    results.Add(new RemoteProjectDto
                    {
                        FileName = item.Name,
                        DriveId = item.ParentReference.DriveId ?? myDrive.Id,
                        FolderId = item.ParentReference.Id,
                        LastModified = item.LastModifiedDateTime ?? DateTimeOffset.MinValue
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler bei der Cloud-Suche: {ex.Message}");
        }

        return results;
    }

    // Lädt eine ausgewählte Cloud-Projekt-JSON herunter, baut die lokale Ordnerstruktur auf und speichert die Verknüpfungs-IDs in der JSON.
    public static async Task<bool> DownloadRemoteProjectAsync(RemoteProjectDto remoteProject)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return false;

        try
        {
            string projectName = Path.GetFileNameWithoutExtension(remoteProject.FileName);
            string localProjectDir = Path.Combine(Settings.DataDirectory, projectName);

            Directory.CreateDirectory(localProjectDir);

            // Alle Dateien des Cloud-Projekts sammeln
            var files = await GetAllCloudFilesAsync(remoteProject.DriveId, remoteProject.FolderId);

            Console.WriteLine($"Gefundene Dateien: {files.Count}");

            using var semaphore = new SemaphoreSlim(SettingsService.Instance.ParallelDownloads);
            int completedFiles = 0;
            int totalFiles = files.Count;

            var downloadTasks = files.Select(async file =>
            {
                await semaphore.WaitAsync();

                try
                {
                    string relativePath = file.RelativePath;

                    string localFilePath = Path.Combine(
                        localProjectDir,
                        relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                    string? directory = Path.GetDirectoryName(localFilePath);

                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    await DownloadCloudFileAsync(
                        remoteProject.DriveId,
                        file.Id,
                        localFilePath);

                    int done = Interlocked.Increment(ref completedFiles);
                    await BusyService.UpdateProgressAsync(done, totalFiles, Path.GetFileName(relativePath));

                    Console.WriteLine($"Download fertig: {relativePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler beim Download von {file.RelativePath}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(downloadTasks);

            // JSON-Verknüpfung aktualisieren
            string localJsonPath =
                Path.Combine(localProjectDir, remoteProject.FileName);

            var projectData = GlobalJson.ReadFromFile(localJsonPath);

            if (projectData != null)
            {
                projectData.CloudDriveId = remoteProject.DriveId;
                projectData.CloudFolderId = remoteProject.FolderId;
                projectData.ProjectPath = projectName;
                projectData.JsonFile = remoteProject.FileName;

                string updatedJson = JsonSerializer.Serialize(projectData, GlobalJson.GetOptions());

                File.WriteAllText(localJsonPath, updatedJson);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Herunterladen des Projekts: {ex.Message}");

            return false;
        }
    }

    // Rekursiv alle Dateien eines Cloud-Ordners sammeln, inklusive Unterordnern.
    private static async Task<List<CloudDownloadFile>> GetAllCloudFilesAsync(
    string driveId,
    string rootFolderId)
    {
        var result = new List<CloudDownloadFile>();

        await CollectCloudFilesRecursiveAsync(driveId, rootFolderId, "", result);

        return result;
    }


    // Rekursive Hilfsmethode, die alle Dateien eines Cloud-Ordners sammelt.
    private static async Task CollectCloudFilesRecursiveAsync(string driveId, string folderId, string relativePath, List<CloudDownloadFile> result)
    {
        if (CurrentAuth?.GraphClient == null)
            return;

        var response =
            await CurrentAuth.GraphClient
                .Drives[driveId]
                .Items[folderId]
                .Children
                .GetAsync(config =>
                {
                    config.QueryParameters.Select = ["id", "name", "folder", "file"];
                });

        if (response?.Value == null)
            return;

        foreach (var item in response.Value)
        {
            if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Name))
                continue;

            if (item.Folder != null)
            {
                string newRelativePath =
                    string.IsNullOrEmpty(relativePath)
                        ? item.Name
                        : $"{relativePath}/{item.Name}";

                await CollectCloudFilesRecursiveAsync(
                    driveId,
                    item.Id,
                    newRelativePath,
                    result);
            }
            else if (item.File != null)
            {
                string filePath =
                    string.IsNullOrEmpty(relativePath) ? item.Name : $"{relativePath}/{item.Name}";

                result.Add(new CloudDownloadFile
                {
                    Id = item.Id,
                    RelativePath = filePath
                });
            }
        }
    }

    // Lädt eine einzelne Datei aus der Cloud herunter und speichert sie lokal.
    private static async Task DownloadCloudFileAsync(string driveId, string fileId, string localFilePath)
    {
        if (CurrentAuth?.GraphClient == null)
            return;

        using var contentStream =
            await CurrentAuth.GraphClient
                .Drives[driveId]
                .Items[fileId]
                .Content
                .GetAsync();

        if (contentStream == null)
            return;

        string? directory = Path.GetDirectoryName(localFilePath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var fileStream =
            new FileStream(
                localFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);

        await contentStream.CopyToAsync(fileStream);
    }

    // Lädt alle Dateien eines lokalen Projektverzeichnisses rekursiv in den entsprechenden Cloud-Ordner hoch.
    private static async Task UploadDirectoryRecursiveAsync(string driveId, string rootFolderId, string localDirPath)
    {
        if (CurrentAuth?.GraphClient == null || !Directory.Exists(localDirPath))
            return;

        // Alle Dateien des Projekts sammeln
        var files = new List<(string FilePath, string RelativePath)>();
        CollectLocalFiles(localDirPath, localDirPath, files);

        int totalFiles = files.Count;
        int completedFiles = 0;

        if (totalFiles == 0) return;

        // Initialen Fortschritt auf 0 setzen
        await BusyService.UpdateProgressAsync(0, totalFiles);

        using var semaphore = new SemaphoreSlim(SettingsService.Instance.ParallelUploads);

        var uploadTasks = files.Select(async file =>
        {
            await semaphore.WaitAsync();

            try
            {
                string relativePath = file.RelativePath;
                string cloudPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

                await UploadFileAsync(
                    driveId,
                    rootFolderId,
                    file.FilePath,
                    cloudPath);

                // Zähler erhöhen und UI informieren
                int done = Interlocked.Increment(ref completedFiles);
                await BusyService.UpdateProgressAsync(done, totalFiles, Path.GetFileName(relativePath));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Upload von {file.RelativePath}: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(uploadTasks);
    }

    // Lädt eine einzelne lokale Datei in den entsprechenden Cloud-Unterordner hoch.
    private static async Task UploadFileAsync(string driveId, string rootFolderId, string localFilePath, string relativeCloudPath)
    {
        if (CurrentAuth?.GraphClient == null)
            return;

        string[] parts = relativeCloudPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0) return;

        string fileName = parts[^1];

        string currentFolderId = rootFolderId;

        // Unterordner durchlaufen/erstellen
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string folderName = parts[i];

            var folder =
                await GetOrCreateFolderAsync(
                    CurrentAuth,
                    driveId,
                    currentFolderId,
                    folderName);

            if (folder?.Id == null)
            {
                Console.WriteLine($"Konnte Cloud-Ordner nicht erstellen: {folderName}");
                return;
            }

            currentFolderId = folder.Id;
        }

        // Datei hochladen
        await using var fileStream =
            new FileStream(
                localFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

        await CurrentAuth.GraphClient
            .Drives[driveId]
            .Items[currentFolderId]
            .ItemWithPath(fileName)
            .Content
            .PutAsync(fileStream);
    }

    private static void CollectLocalFiles(
    string rootDirectory,
    string currentDirectory,
    List<(string FilePath, string RelativePath)> files)
    {
        foreach (var filePath in Directory.GetFiles(currentDirectory))
        {
            string fileName = Path.GetFileName(filePath);

            // JSON wird separat über SaveWithSyncCheckAsync hochgeladen
            if (fileName.Equals(CloudFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            string relativePath =
                Path.GetRelativePath(
                    rootDirectory,
                    filePath);

            files.Add((filePath, relativePath));
        }

        foreach (var directory in Directory.GetDirectories(currentDirectory))
        {
            CollectLocalFiles(
                rootDirectory,
                directory,
                files);
        }
    }

    // Lädt eine einzelne lokale Datei direkt in den entsprechenden Cloud-Unterordner hoch.
    public static async Task UploadSingleFileAsync(string localFilePath, string subFolder = "")
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;

        // Prüfen ob Projekt mit Cloud verknüpft ist
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
            return;

        if (!File.Exists(localFilePath)) return;

        try
        {
            string driveId = GlobalJson.Data.CloudDriveId;
            string currentFolderId = GlobalJson.Data.CloudFolderId;
            string fileName = Path.GetFileName(localFilePath);

            // Falls die Datei in einen Unterordner (z.B. "images") soll, die ID dieses Ordners ermitteln
            if (!string.IsNullOrEmpty(subFolder))
            {
                var parts = subFolder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var folder = await GetOrCreateFolderAsync(CurrentAuth, driveId, currentFolderId, part);
                    if (folder?.Id != null)
                    {
                        currentFolderId = folder.Id;
                    }
                    else
                    {
                        Console.WriteLine($"Konnte Unterordner {part} nicht finden/erstellen.");
                        return;
                    }
                }
            }

            // Datei hochladen
            using var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await CurrentAuth.GraphClient.Drives[driveId].Items[currentFolderId]
                .ItemWithPath(fileName)
                .Content
                .PutAsync(fileStream);

            Console.WriteLine($"Erfolgreich hochgeladen: {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Upload von {localFilePath}: {ex.Message}");
        }
    }

    // Prüft, ob in der Cloud eine neuere Version der JSON existiert
    public static async Task<bool> IsCloudVersionNewerAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        // Keine Cloud-Verknüpfung vorhanden
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
            return false;

        try
        {
            var cloudItem = await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            if (cloudItem?.LastModifiedDateTime != null)
            {
                string localFilePath = GlobalJson.GetFilePath();
                if (File.Exists(localFilePath))
                {
                    DateTime localDiskTime = File.GetLastWriteTimeUtc(localFilePath);
                    return cloudItem.LastModifiedDateTime > localDiskTime;
                }
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError) { /* Datei existiert in Cloud (noch) nicht */ }
        catch (Exception ex) { Console.WriteLine($"Fehler beim Check-Update: {ex.Message}"); }

        return false;
    }

    // Lädt nur die JSON-Datei herunter und prüft gezielt auf fehlende Mediendateien
    public static async Task<bool> SyncJsonOnlyFromCloudAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
        {
            return false;
        }

        try
        {
            string driveId = GlobalJson.Data.CloudDriveId;
            string targetFolderId = GlobalJson.Data.CloudFolderId;
            string filePath = GlobalJson.GetFilePath();

            // Nur die JSON-Datei abrufen
            var cloudItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            var cloudStream = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .Content
                .GetAsync();

            if (cloudStream != null && cloudItem != null)
            {
                var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(cloudStream, GlobalJson.GetOptions());
                if (cloudData != null)
                {
                    // Daten lokal zusammenführen
                    MergeModels(GlobalJson.Data, cloudData);
                    GlobalJson.SaveToFile();

                    _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
                    if (cloudItem.LastModifiedDateTime != null)
                    {
                        _lastKnownCloudSyncTime = cloudItem.LastModifiedDateTime.Value;
                        _lastKnownETag = cloudItem.ETag;
                    }

                    // Nur Dateien herunterladen, die lokal fehlen
                    await DownloadMissingProjectFilesAsync(driveId, targetFolderId);

                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim intelligenten Sync: {ex.Message}");
        }

        return false;
    }

    // Prüft, ob Medien aus der JSON lokal auf der Festplatte fehlen, und lädt nur diese herunter
    private static async Task DownloadMissingProjectFilesAsync(string driveId, string rootFolderId)
    {
        if (CurrentAuth?.GraphClient == null || GlobalJson.Data == null) return;
        string? projectDir = Path.GetDirectoryName(GlobalJson.GetFilePath());
        if (string.IsNullOrEmpty(projectDir)) return;

        if (GlobalJson.Data.Plans != null)
        {
            foreach (var planPair in GlobalJson.Data.Plans)
            {
                var plan = planPair.Value;

                // Plaene pruefen (bestehende Logik)
                if (!string.IsNullOrEmpty(plan.File))
                {
                    string localPath = Path.Combine(projectDir, "plans", plan.File);
                    if (!File.Exists(localPath))
                        await DownloadSpecificFileAsync(driveId, rootFolderId, $"plans/{plan.File}", localPath);
                }

                // CustomPins innerhalb des Plans pruefen
                if (plan.Pins != null)
                {
                    foreach (var pinPair in plan.Pins)
                    {
                        var pin = pinPair.Value;
                        if (pin.IsCustomPin && !string.IsNullOrEmpty(pin.PinIcon))
                        {
                            string localPinPath = Path.Combine(projectDir, GlobalJson.Data.CustomPinsPath, pin.PinIcon);
                            if (!File.Exists(localPinPath))
                            {
                                await DownloadSpecificFileAsync(driveId, rootFolderId, $"{GlobalJson.Data.CustomPinsPath}/{pin.PinIcon}", localPinPath);

                                string dataFile = Path.ChangeExtension(pin.PinIcon, ".data");
                                string localDataPath = Path.Combine(projectDir, GlobalJson.Data.CustomPinsPath, dataFile);
                                await DownloadSpecificFileAsync(driveId, rootFolderId, $"{GlobalJson.Data.CustomPinsPath}/{dataFile}", localDataPath);
                            }
                        }
                    }
                }
            }
        }
    }

    private static async Task DownloadSpecificFileAsync(string driveId, string rootFolderId, string relativeCloudPath, string localDestinationPath)
    {
        try
        {
            var fileStream = await CurrentAuth!.GraphClient!.Drives[driveId].Items[rootFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(localDestinationPath)!);
                using var localFile = File.Create(localDestinationPath);
                await fileStream.CopyToAsync(localFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Nachladen der Datei {relativeCloudPath}: {ex.Message}");
        }
    }

    public static async Task DeleteCloudFileAsync(string relativeCloudPath)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;
        if (GlobalJson.Data == null || string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) || string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)) return;

        try
        {
            string cloudPath = relativeCloudPath.Replace("\\", "/");
            await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(cloudPath)
                .DeleteAsync();

            Console.WriteLine($"Erfolgreich in der Cloud geloescht: {cloudPath}");
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError)
        {
            /* Datei war in der Cloud bereits nicht mehr vorhanden */
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Loeschen der Cloud-Datei {relativeCloudPath}: {ex.Message}");
        }
    }

    public static void StartCloudPolling(int intervalSeconds = 12)
    {
        StopCloudPolling(); // Eventuell laufenden Timer stoppen

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await CheckETagAndSyncAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Timer wurde regulär beendet
            }
        }, token);
    }

    public static void StopCloudPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    private static async Task CheckETagAndSyncAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)) return;

        try
        {
            string driveId = GlobalJson.Data.CloudDriveId;
            string targetFolderId = GlobalJson.Data.CloudFolderId;

            // Nur Metadaten abrufen (abgefragtes Objekt enthält den ETag, verbraucht kaum Datenvolumen)
            var cloudItem = await CurrentAuth.GraphClient.Drives[driveId]
                .Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            if (cloudItem?.ETag != null)
            {
                // Wenn noch kein ETag gespeichert ist, initialisieren
                if (string.IsNullOrEmpty(_lastKnownETag))
                {
                    _lastKnownETag = cloudItem.ETag;
                    return;
                }

                // Prüfen, ob sich der ETag verändert hat
                if (cloudItem.ETag != _lastKnownETag)
                {
                    _lastKnownETag = cloudItem.ETag;
                    await SyncJsonOnlyFromCloudAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Polling-Check fehlgeschlagen: {ex.Message}");
        }
    }

    // Laedt ein spezifisches Foto oder Bild bedarfsgesteuert herunter
    // Unterstuetzt optionale explizite Pfade/IDs fuer ungeladene Projekte aus der Liste
    public static async Task<bool> DownloadMediaOnDemandAsync(string fileName, string subFolder = "", string? driveId = null, string? folderId = null, string? projectDir = null)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        // Nutze explizit uebergebene Werte oder Fallback auf das aktuell geladene GlobalJson
        string? effectiveDriveId = driveId ?? GlobalJson.Data?.CloudDriveId;
        string? effectiveFolderId = folderId ?? GlobalJson.Data?.CloudFolderId;
        string? effectiveProjectDir = projectDir ?? Path.GetDirectoryName(GlobalJson.GetFilePath());

        if (string.IsNullOrEmpty(effectiveDriveId) ||
            string.IsNullOrEmpty(effectiveFolderId) ||
            string.IsNullOrEmpty(effectiveProjectDir))
            return false;

        try
        {
            // Cloud-Pfad zusammensetzen
            string relativeCloudPath = string.IsNullOrWhiteSpace(subFolder)
                ? fileName
                : $"{subFolder.Trim('/', '\\')}/{fileName}".Replace("\\", "/");

            // Lokalen Zielpfad aufbauen
            string localDestinationPath = string.IsNullOrWhiteSpace(subFolder)
                ? Path.Combine(effectiveProjectDir, fileName)
                : Path.Combine(effectiveProjectDir, subFolder, fileName);

            // Abbruch, falls die Datei bereits lokal existiert
            if (File.Exists(localDestinationPath)) return true;

            var fileStream = await CurrentAuth.GraphClient.Drives[effectiveDriveId].Items[effectiveFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream != null)
            {
                string? targetDir = Path.GetDirectoryName(localDestinationPath);
                if (!string.IsNullOrEmpty(targetDir))
                    Directory.CreateDirectory(targetDir);

                using var localFile = File.Create(localDestinationPath);
                await fileStream.CopyToAsync(localFile);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bedarfs-Download fehlgeschlagen fuer {fileName} in '{subFolder}': {ex.Message}");
        }

        return false;
    }

    // Ueberladung fuer Abwaertskompatibilitaet mit bestehendem Foto/Thumbnail-Code
    public static Task<bool> DownloadMediaOnDemandAsync(string fileName, bool isThumbnail)
    {
        string subFolder = isThumbnail
            ? (GlobalJson.Data?.ThumbnailPath ?? "thumbnails")
            : (GlobalJson.Data?.ImagePath ?? "images");

        return DownloadMediaOnDemandAsync(fileName, subFolder);
    }

    public static void ResetCloudSync()
    {
        TargetFolderId = null;
        _lastKnownETag = null;
        _lastKnownCloudSyncTime = DateTimeOffset.MinValue;

        // Laufende Speicher-Debounces des vorherigen Projekts abbrechen
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        _pendingUploadQueue.Clear();
    }
}

public class RemoteProjectDto
{
    public string FileName { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}

public class CloudDownloadFile
{
    public string Id { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}