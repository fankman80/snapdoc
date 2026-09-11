using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using SnapDoc.Messages;
using SnapDoc.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using SkiaSharp;
using static SnapDoc.Helper;

namespace SnapDoc.Services;

public static class SaveManager
{
    private const int MaxSaveRetries = 3;

    private static readonly ConcurrentDictionary<(string LocalFilePath, string SubFolder), byte> _pendingUploadQueue = new();

    private static readonly Lock _fileLock = new();
    private static readonly Lock _debounceLock = new();

    // Verhindert, dass Debounce-Timer und Cloud-Polling gleichzeitig speichern
    private static readonly SemaphoreSlim _saveGate = new(1, 1);

    private static CancellationTokenSource? _pollingCts;
    private static CancellationTokenSource? _debounceCts;

    private static DateTime _lastKnownWriteTime;
    private static DateTimeOffset _lastKnownCloudSyncTime = DateTimeOffset.MinValue;
    private static string? _lastKnownETag;

    private static string CloudFileName => SettingsService.DefaultJson;

    public static string? TargetFolderId { get; set; }
    public static AuthService? CurrentAuth { get; set; }

    public static void Initialize(string filePath)
    {
        GlobalJson.LoadFromFile(filePath);

        _lastKnownWriteTime = File.Exists(filePath)
            ? File.GetLastWriteTimeUtc(filePath)
            : default;
    }

    // ===============================================================
    //  Debounce
    // ===============================================================

    // Standard-Aufruf ohne Dateien (nur JSON sync)
    public static void NotifyDataChanged(int delayMilliseconds = 2000)
        => NotifyDataChanged([], delayMilliseconds);

    // Komfort-Ueberladung fuer eine einzelne Datei
    public static void NotifyDataChanged(string localFilePath, string subFolder, int delayMilliseconds = 2000)
        => NotifyDataChanged([(localFilePath, subFolder)], delayMilliseconds);

    // Hauptmethode fuer mehrere Dateien gleichzeitig
    public static void NotifyDataChanged(IEnumerable<(string LocalFilePath, string SubFolder)> files, int delayMilliseconds = 2000)
    {
        foreach (var (localFilePath, subFolder) in files)
        {
            if (!string.IsNullOrEmpty(localFilePath))
                _pendingUploadQueue.TryAdd((localFilePath, subFolder), 0);
        }

        CancellationToken token;

        // Token-Wechsel gegen parallele Aufrufe absichern
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            token = _debounceCts.Token;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMilliseconds, token);

                if (!token.IsCancellationRequested)
                    await SaveWithSyncCheckAsync();
            }
            catch (OperationCanceledException) { /* Debounce abgebrochen */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim verzoegerten Speichern: {ex.Message}");
            }
        }, token);
    }

    // ===============================================================
    //  Speichern und Cloud-Abgleich
    // ===============================================================

    public static async Task SaveWithSyncCheckAsync(int retryCount = 0)
    {
        // Doppelte Ausfuehrung (Debounce + Polling) serialisieren
        await _saveGate.WaitAsync();
        try
        {
            await SaveWithSyncCheckCoreAsync(retryCount);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private static async Task SaveWithSyncCheckCoreAsync(int retryCount)
    {
        string filePath = GlobalJson.GetFilePath();
        if (string.IsNullOrEmpty(filePath))
            return;

        var activeData = GlobalJson.Data;
        if (activeData == null)
            return;

        // 1. IMMER lokal speichern
        lock (_fileLock)
        {
            if (_lastKnownWriteTime != default && File.Exists(filePath))
            {
                DateTime currentDiskTime = File.GetLastWriteTimeUtc(filePath);
                if (currentDiskTime > _lastKnownWriteTime)
                    ResolveConflictAndMerge(filePath);
            }

            GlobalJson.SaveToFile();

            _lastKnownWriteTime = File.Exists(filePath)
                ? File.GetLastWriteTimeUtc(filePath)
                : DateTime.UtcNow;
        }

        // 2. Keine Cloud-Verknuepfung? Dann war das lokale Speichern bereits erfolgreich.
        if (string.IsNullOrEmpty(activeData.CloudDriveId) || string.IsNullOrEmpty(activeData.CloudFolderId))
            return;

        // 3. Nicht eingeloggt / offline? Lokal wurde bereits gespeichert.
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return;

        string driveId = activeData.CloudDriveId;
        string targetFolderId = activeData.CloudFolderId;
        string activeCloudFileName = CloudFileName;
        string frozenJsonPayload = GlobalJson.ToJson();

        // 4. Ab hier Cloud-Synchronisation
        try
        {
            try
            {
                var cloudItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                    .ItemWithPath(activeCloudFileName)
                    .GetAsync();

                bool baselineNeverEstablished = _lastKnownCloudSyncTime == DateTimeOffset.MinValue;

                if (cloudItem?.LastModifiedDateTime != null && cloudItem.LastModifiedDateTime > _lastKnownCloudSyncTime)
                {
                    if (baselineNeverEstablished)
                    {
                        _lastKnownCloudSyncTime = cloudItem.LastModifiedDateTime.Value;
                        _lastKnownETag = cloudItem.ETag;
                    }
                    else
                    {
                        var cloudStream = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                            .ItemWithPath(activeCloudFileName)
                            .Content
                            .GetAsync();

                        if (cloudStream != null)
                        {
                            var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(cloudStream, GlobalJson.GetOptions());

                            if (cloudData != null)
                            {
                                lock (_fileLock)
                                {
                                    // SICHERHEITSCHECK: Nur Mergen, wenn das Projekt noch dasselbe ist
                                    if (GlobalJson.GetFilePath() == filePath)
                                    {
                                        MergeModels(GlobalJson.Data, cloudData);
                                        GlobalJson.SaveToFile();
                                        _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
                                        frozenJsonPayload = GlobalJson.ToJson();
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (ODataError) { /* Existiert nicht */ }

            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(frozenJsonPayload);
            using var stream = new MemoryStream(byteArray);

            try
            {
                var uploadedItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                    .ItemWithPath(activeCloudFileName)
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
            catch (ODataError ex) when (ex.ResponseStatusCode == 412 || ex.Error?.Code == "conditionNotMet")
            {
                if (retryCount >= MaxSaveRetries)
                {
                    Console.WriteLine($"Upload nach {MaxSaveRetries} Versuchen abgebrochen (ETag-Konflikt).");
                    return;
                }

                Console.WriteLine($"Konflikt beim Upload (Versuch {retryCount + 1}/{MaxSaveRetries}). ETag stimmt nicht mehr ueberein.");

                // Baseline verwerfen, damit der naechste Versuch neu mergt
                _lastKnownETag = null;
                _lastKnownCloudSyncTime = DateTimeOffset.MinValue;

                await Task.Delay(500 * (retryCount + 1));
                await SaveWithSyncCheckCoreAsync(retryCount + 1);
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cloud-Upload fehlgeschlagen: {ex.Message}");
        }

        // Nach dem JSON-Sync alle angesammelten Dateien im Hintergrund abarbeiten
        if (!_pendingUploadQueue.IsEmpty && CurrentAuth is { IsLoggedIn: true })
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

        if (!authService.IsLoggedIn || authService.GraphClient == null)
            return;

        try
        {
            var myDrive = await authService.GraphClient.Me.Drive.GetAsync();
            if (myDrive == null || string.IsNullOrEmpty(myDrive.Id))
                return;

            string? projectDirectory = Path.GetDirectoryName(localFilePath);

            // Prioritaet auf Object_name, danach Ordnername, sonst Fallback
            string projectName = !string.IsNullOrWhiteSpace(GlobalJson.Data?.Object_name)
                ? GlobalJson.Data.Object_name
                : (!string.IsNullOrEmpty(projectDirectory) ? Path.GetFileName(projectDirectory) : "DefaultProject");

            string sanitizedProjectName = SanitizeName(projectName);

            string? targetFolderId = await EnsureCloudFolderStructureAsync(authService, myDrive.Id, sanitizedProjectName);
            if (string.IsNullOrEmpty(targetFolderId))
                return;

            var cloudItem = await authService.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            var stream = await authService.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .Content
                .GetAsync();

            if (stream == null || cloudItem == null)
                return;

            var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(stream, GlobalJson.GetOptions());
            if (cloudData == null)
                return;

            GlobalJson.Data = cloudData;
            GlobalJson.UpdateFilePath(localFilePath);
            GlobalJson.SaveToFile();

            _lastKnownWriteTime = File.GetLastWriteTimeUtc(localFilePath);

            if (cloudItem.LastModifiedDateTime != null)
            {
                _lastKnownCloudSyncTime = cloudItem.LastModifiedDateTime.Value;
                _lastKnownETag = cloudItem.ETag;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cloud-Laden fehlgeschlagen, nutze lokale Datei weiter: {ex.Message}");
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

    // ===============================================================
    //  Merge
    // ===============================================================

    public static void MergeModels(JsonDataModel local, JsonDataModel cloud)
    {
        if (local == null || cloud == null) return;

        bool titleImageChanged = local.TitleImage != cloud.TitleImage;
        string oldTitleImage = local.TitleImage;

        // Projektdetails vergleichen und uebertragen
        bool projectDetailsChanged = false;

        if (local.Client_name != cloud.Client_name ||
            local.Working_title != cloud.Working_title ||
            local.Object_address != cloud.Object_address ||
            local.Project_nr != cloud.Project_nr ||
            local.Object_name != cloud.Object_name ||
            local.Project_manager != cloud.Project_manager ||
            local.Creation_date != cloud.Creation_date ||
            titleImageChanged)
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
            local.TitleImage = cloud.TitleImage;
            local.TitleImageSize = cloud.TitleImageSize;

            // ProjectItem laedt die neue Datei nach und aktualisiert die UI
            WeakReferenceMessenger.Default.Send(new TitleImageChangedMessage(oldTitleImage, cloud.TitleImage));
        }

        if (cloud.Plans == null)
        {
            if (projectDetailsChanged)
                WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));
            return;
        }

        local.Plans ??= [];

        bool planStructureChanged = false;
        bool planOrderChanged = !local.Plans.Keys.SequenceEqual(cloud.Plans.Keys);

        // Geloeschte Plaene entfernen (Strukturaenderung)
        var deletedPlanIds = local.Plans.Keys.Except(cloud.Plans.Keys).ToList();
        if (deletedPlanIds.Count > 0)
        {
            foreach (var deletedId in deletedPlanIds)
                local.Plans.Remove(deletedId);

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

            MergePlan(planId, localPlan, cloudPlan);
        }

        // Wenn sich die Reihenfolge geaendert hat, lokales Dictionary neu aufbauen
        if (planOrderChanged)
        {
            var orderedDictionary = new Dictionary<string, Plan>();

            foreach (var cloudKey in cloud.Plans.Keys)
            {
                if (local.Plans.TryGetValue(cloudKey, out Plan? plan))
                    orderedDictionary.Add(cloudKey, plan);
            }

            local.Plans = orderedDictionary;
            planStructureChanged = true;
        }

        // UI-Benachrichtigungen feuern
        if (projectDetailsChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));

        // Nur bei echter Strukturaenderung ein Shell-Reload ausloesen
        if (planStructureChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.PlanListUpdated));
    }

    private static void MergePlan(string planId, Plan localPlan, Plan cloudPlan)
    {
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

            if (detailsChanged)
                WeakReferenceMessenger.Default.Send(new PlanDetailsChangedMessage((planId, cloudPlan.Name, cloudPlan.Description, cloudPlan.IsGrayscale, cloudPlan.PlanColor)));
        }

        localPlan.Pins ??= [];

        // Geloeschte Pins entfernen
        var deletedPinIds = localPlan.Pins.Keys
        .Except(cloudPlan.Pins?.Keys ?? Enumerable.Empty<string>())
        .ToList();

        foreach (var deletedId in deletedPinIds)
        {
            localPlan.Pins.Remove(deletedId);
            WeakReferenceMessenger.Default.Send(new PinDeletedMessage(deletedId));
        }

        if (cloudPlan.Pins != null)
        {
            // Neue oder geaenderte Pins verarbeiten
            foreach (var cloudPinKp in cloudPlan.Pins)
            {
                var pinId = cloudPinKp.Key;
                var cloudPin = cloudPinKp.Value;

                if (!localPlan.Pins.TryGetValue(pinId, out Pin? localPin))
                {
                    localPlan.Pins.Add(pinId, cloudPin);
                    WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
                    continue;
                }

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
                localPin.IsCustomIcon = cloudPin.IsCustomIcon;
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
                    WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinId));
            }
        }

        // Zaehler nach allen Aenderungen einmalig korrigieren
        if (localPlan.PinCount != localPlan.Pins.Count)
            localPlan.PinCount = localPlan.Pins.Count;
    }

    // ===============================================================
    //  Ordnerstruktur
    // ===============================================================

    private static async Task<string?> EnsureCloudFolderStructureAsync(AuthService authService, string driveId, string projectName)
    {
        try
        {
            // Basisordner "SnapDoc" im OneDrive Root pruefen/erstellen
            var snapDocItem = await GetOrCreateFolderAsync(authService, driveId, "root", "SnapDoc");
            if (snapDocItem == null || string.IsNullOrEmpty(snapDocItem.Id)) return null;

            // Projektordner innerhalb von "SnapDoc" pruefen/erstellen
            var projectItem = await GetOrCreateFolderAsync(authService, driveId, snapDocItem.Id, projectName);
            return projectItem?.Id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen der Ordnerstruktur: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sucht einen Ordner ueber ALLE Ergebnisseiten und legt ihn nur an,
    /// wenn er sicher nicht existiert. Bei Abruffehlern wird nichts erstellt,
    /// um Duplikate in der Cloud zu vermeiden.
    /// </summary>
    private static async Task<DriveItem?> GetOrCreateFolderAsync(AuthService authService, string driveId, string parentId, string folderName)
    {
        if (authService?.GraphClient == null) return null;

        try
        {
            var response = await authService.GraphClient.Drives[driveId].Items[parentId].Children.GetAsync();

            while (response?.Value != null)
            {
                var existingFolder = response.Value.FirstOrDefault(f =>
                    f?.Name != null &&
                    f.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase) &&
                    f.Folder != null);

                if (existingFolder != null)
                    return existingFolder;

                if (string.IsNullOrEmpty(response.OdataNextLink))
                    break;

                // Naechste Seite laden - ohne diese Schleife entstehen Duplikate
                response = await authService.GraphClient.Drives[driveId].Items[parentId].Children
                    .WithUrl(response.OdataNextLink)
                    .GetAsync();
            }
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // Elternordner existiert nicht - Erstellung waere sinnlos
            Console.WriteLine($"Elternordner nicht gefunden: {parentId}");
            return null;
        }
        catch (Exception ex)
        {
            // Abruf fehlgeschlagen: NICHT erstellen, sonst drohen Duplikate
            Console.WriteLine($"Ordnerabruf fehlgeschlagen ({folderName}): {ex.Message}");
            return null;
        }

        var newFolder = new DriveItem
        {
            Name = folderName,
            Folder = new Folder()
        };

        try
        {
            return await authService.GraphClient.Drives[driveId].Items[parentId].Children.PostAsync(newFolder);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ordner konnte nicht erstellt werden ({folderName}): {ex.Message}");
            return null;
        }
    }

    private static async Task EnsureProjectSubfoldersAsync(AuthService authService, string driveId, string projectFolderId)
    {
        var project = ProjectItem.Current;

        string[] subfolders =
        [
            project.ImageFolder,
            $"{project.ImageFolder}/originals",
            project.PlanFolder,
            $"{project.PlanFolder}/thumbnails",
            project.ThumbnailFolder,
            project.CustomPinsFolder,
            project.CustomIconsFolder
        ];

        foreach (var subfolder in subfolders)
        {
            var parts = subfolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
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

    // ===============================================================
    //  Cloud-Projekte anlegen / verknuepfen
    // ===============================================================

    // Synchronisiert die aktuelle Datei mit einem bestehenden Ordner in der Cloud.
    public static async Task<bool> SyncWithExistingFolderAsync(string parentFolderId)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        try
        {
            TargetFolderId = parentFolderId;

            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive?.Id == null) return false;

            string driveId = myDrive.Id;

            await EnsureProjectSubfoldersAsync(CurrentAuth, driveId, TargetFolderId);

            if (GlobalJson.Data != null)
            {
                GlobalJson.Data.CloudDriveId = driveId;
                GlobalJson.Data.CloudFolderId = TargetFolderId;
            }

            await SaveWithSyncCheckAsync();

            // Lokales Projektverzeichnis ebenfalls rekursiv hochladen
            string? localProjectDir = Path.GetDirectoryName(GlobalJson.GetFilePath());
            if (!string.IsNullOrEmpty(localProjectDir))
                await UploadDirectoryRecursiveAsync(driveId, TargetFolderId, localProjectDir);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Synchronisieren: {ex.Message}");
            return false;
        }
    }

    // Erstellt ein neues Projektverzeichnis inklusive Unterordnern im gewaehlten Cloud-Ordner.
    public static async Task<(bool IsSuccess, string? DriveId, string? FolderId)> CreateAndSyncNewCloudProjectAsync(string parentFolderId)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return (false, null, null);

        try
        {
            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive?.Id == null)
                return (false, null, null);

            string localFilePath = GlobalJson.GetFilePath();
            string? localProjectDir = Path.GetDirectoryName(localFilePath);

            // Projektnamen bevorzugt aus Object_name lesen, sonst Ordnername, sonst Fallback
            string projectName = !string.IsNullOrWhiteSpace(GlobalJson.Data?.Object_name)
                ? GlobalJson.Data.Object_name
                : (!string.IsNullOrEmpty(localProjectDir) ? Path.GetFileName(localProjectDir) : "Projekt");

            string cloudFolderName = $"{DateTime.Now:yyMMdd}_{SanitizeName(projectName)}";

            var projectFolder = await GetOrCreateFolderAsync(CurrentAuth, myDrive.Id, parentFolderId, cloudFolderName);
            if (projectFolder?.Id == null)
                return (false, null, null);

            TargetFolderId = projectFolder.Id;

            await EnsureProjectSubfoldersAsync(CurrentAuth, myDrive.Id, TargetFolderId);

            if (GlobalJson.Data != null)
            {
                GlobalJson.Data.CloudDriveId = myDrive.Id;
                GlobalJson.Data.CloudFolderId = TargetFolderId;
                GlobalJson.SaveToFile();
            }

            await SaveWithSyncCheckAsync();

            // Alle lokalen Mediendateien in die Cloud hochladen
            if (!string.IsNullOrEmpty(localProjectDir))
                await UploadDirectoryRecursiveAsync(myDrive.Id, TargetFolderId, localProjectDir);

            return (true, myDrive.Id, TargetFolderId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Erstellen des Cloud-Projekts: {ex.Message}");
            return (false, null, null);
        }
    }

    // Verknuepft das aktuelle Projekt mit einem SharePoint/OneDrive-Ordner.
    public static async Task LinkProjectToCloudAsync(string driveId, string folderId)
    {
        if (GlobalJson.Data == null) return;

        GlobalJson.Data.CloudDriveId = driveId;
        GlobalJson.Data.CloudFolderId = folderId;

        await SaveWithSyncCheckAsync();
    }

    // ===============================================================
    //  Cloud-Suche und Download
    // ===============================================================

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

            var searchResponse = await CurrentAuth.GraphClient.Drives[myDrive.Id]
                .SearchWithQ(CloudFileName)
                .GetAsSearchWithQGetResponseAsync();

            if (searchResponse?.Value == null)
                return results;

            foreach (var item in searchResponse.Value)
            {
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

    public static async Task<bool> DownloadRemoteProjectAsync(RemoteProjectDto remoteProject)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return false;

        try
        {
            var remoteData = await GetRemoteProjectDataAsync(remoteProject.DriveId, remoteProject.FolderId, remoteProject.FileName);

            string projectName = !string.IsNullOrWhiteSpace(remoteData?.Object_name)
                ? remoteData.Object_name
                : "Unbenanntes_Projekt";

            string localProjectDir = Path.Combine(Settings.DataDirectory, SanitizeName(projectName));
            Directory.CreateDirectory(localProjectDir);

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
                    string localFilePath = Path.Combine(
                        localProjectDir,
                        file.RelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                    string? directory = Path.GetDirectoryName(localFilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    await DownloadCloudFileAsync(remoteProject.DriveId, file.Id, localFilePath);

                    int done = Interlocked.Increment(ref completedFiles);
                    await BusyService.UpdateProgressAsync(done, totalFiles, Path.GetFileName(file.RelativePath));
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

            // JSON-Verknuepfung aktualisieren
            string localJsonPath = Path.Combine(localProjectDir, remoteProject.FileName);
            var projectData = GlobalJson.ReadFromFile(localJsonPath);

            if (projectData != null)
            {
                projectData.CloudDriveId = remoteProject.DriveId;
                projectData.CloudFolderId = remoteProject.FolderId;

                File.WriteAllText(localJsonPath, JsonSerializer.Serialize(projectData, GlobalJson.GetOptions()));
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Herunterladen des Projekts: {ex.Message}");
            return false;
        }
    }

    private static async Task<List<CloudDownloadFile>> GetAllCloudFilesAsync(string driveId, string rootFolderId)
    {
        var result = new List<CloudDownloadFile>();
        await CollectCloudFilesRecursiveAsync(driveId, rootFolderId, "", result);
        return result;
    }

    /// <summary>
    /// Sammelt rekursiv alle Dateien eines Cloud-Ordners - inklusive
    /// aller Ergebnisseiten, damit bei grossen Projekten nichts fehlt.
    /// </summary>
    private static async Task CollectCloudFilesRecursiveAsync(string driveId, string folderId, string relativePath, List<CloudDownloadFile> result)
    {
        if (CurrentAuth?.GraphClient == null)
            return;

        var response = await CurrentAuth.GraphClient
            .Drives[driveId]
            .Items[folderId]
            .Children
            .GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "name", "folder", "file"];
            });

        while (response?.Value != null)
        {
            foreach (var item in response.Value)
            {
                if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Name))
                    continue;

                string itemPath = string.IsNullOrEmpty(relativePath)
                    ? item.Name
                    : $"{relativePath}/{item.Name}";

                if (item.Folder != null)
                {
                    await CollectCloudFilesRecursiveAsync(driveId, item.Id, itemPath, result);
                }
                else if (item.File != null)
                {
                    result.Add(new CloudDownloadFile
                    {
                        Id = item.Id,
                        RelativePath = itemPath
                    });
                }
            }

            if (string.IsNullOrEmpty(response.OdataNextLink))
                break;

            response = await CurrentAuth.GraphClient
                .Drives[driveId]
                .Items[folderId]
                .Children
                .WithUrl(response.OdataNextLink)
                .GetAsync();
        }
    }

    private static async Task DownloadCloudFileAsync(string driveId, string fileId, string localFilePath)
    {
        if (CurrentAuth?.GraphClient == null)
            return;

        using var contentStream = await CurrentAuth.GraphClient
            .Drives[driveId]
            .Items[fileId]
            .Content
            .GetAsync();

        if (contentStream == null)
            return;

        string? directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var fileStream = new FileStream(
            localFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true);

        await contentStream.CopyToAsync(fileStream);
    }

    // ===============================================================
    //  Upload
    // ===============================================================

    private static async Task UploadDirectoryRecursiveAsync(string driveId, string rootFolderId, string localDirPath)
    {
        if (CurrentAuth?.GraphClient == null || !Directory.Exists(localDirPath))
            return;

        var files = new List<(string FilePath, string RelativePath)>();
        CollectLocalFiles(localDirPath, localDirPath, files);

        int totalFiles = files.Count;
        int completedFiles = 0;

        if (totalFiles == 0) return;

        await BusyService.UpdateProgressAsync(0, totalFiles);

        using var semaphore = new SemaphoreSlim(SettingsService.Instance.ParallelUploads);

        var uploadTasks = files.Select(async file =>
        {
            await semaphore.WaitAsync();
            try
            {
                string cloudPath = file.RelativePath.Replace(Path.DirectorySeparatorChar, '/');
                await UploadFileAsync(driveId, rootFolderId, file.FilePath, cloudPath);

                int done = Interlocked.Increment(ref completedFiles);
                await BusyService.UpdateProgressAsync(done, totalFiles, Path.GetFileName(file.RelativePath));
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
            var folder = await GetOrCreateFolderAsync(CurrentAuth, driveId, currentFolderId, parts[i]);

            if (folder?.Id == null)
            {
                Console.WriteLine($"Konnte Cloud-Ordner nicht erstellen: {parts[i]}");
                return;
            }

            currentFolderId = folder.Id;
        }

        await using var fileStream = new FileStream(
            localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);

        await CurrentAuth.GraphClient
            .Drives[driveId]
            .Items[currentFolderId]
            .ItemWithPath(fileName)
            .Content
            .PutAsync(fileStream);
    }

    private static void CollectLocalFiles(string rootDirectory, string currentDirectory, List<(string FilePath, string RelativePath)> files)
    {
        foreach (var filePath in Directory.GetFiles(currentDirectory))
        {
            // JSON wird separat ueber SaveWithSyncCheckAsync hochgeladen
            if (Path.GetFileName(filePath).Equals(CloudFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            files.Add((filePath, Path.GetRelativePath(rootDirectory, filePath)));
        }

        foreach (var directory in Directory.GetDirectories(currentDirectory))
            CollectLocalFiles(rootDirectory, directory, files);
    }

    public static async Task UploadSingleFileAsync(string localFilePath, string subFolder = "")
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;

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

            if (!string.IsNullOrEmpty(subFolder))
            {
                var parts = subFolder.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    var folder = await GetOrCreateFolderAsync(CurrentAuth, driveId, currentFolderId, part);

                    if (folder?.Id == null)
                    {
                        Console.WriteLine($"Konnte Unterordner {part} nicht finden/erstellen.");
                        return;
                    }

                    currentFolderId = folder.Id;
                }
            }

            await using var fileStream = new FileStream(
                localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);

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

    // ===============================================================
    //  Cloud-Status und Polling
    // ===============================================================

    public static async Task<bool> IsCloudVersionNewerAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

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
                    return cloudItem.LastModifiedDateTime > File.GetLastWriteTimeUtc(localFilePath);
            }
        }
        catch (ODataError) { /* Datei existiert in Cloud (noch) nicht */ }
        catch (Exception ex) { Console.WriteLine($"Fehler beim Check-Update: {ex.Message}"); }

        return false;
    }

    public static async Task<bool> SyncJsonOnlyFromCloudAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
            return false;

        await _saveGate.WaitAsync();
        try
        {
            string driveId = GlobalJson.Data.CloudDriveId;
            string targetFolderId = GlobalJson.Data.CloudFolderId;
            string filePath = GlobalJson.GetFilePath();

            var cloudItem = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            var cloudStream = await CurrentAuth.GraphClient.Drives[driveId].Items[targetFolderId]
                .ItemWithPath(CloudFileName)
                .Content
                .GetAsync();

            if (cloudStream == null || cloudItem == null)
                return false;

            var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(cloudStream, GlobalJson.GetOptions());
            if (cloudData == null)
                return false;

            lock (_fileLock)
            {
                MergeModels(GlobalJson.Data, cloudData);
                GlobalJson.SaveToFile();
                _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
            }

            if (cloudItem.LastModifiedDateTime != null)
            {
                _lastKnownCloudSyncTime = cloudItem.LastModifiedDateTime.Value;
                _lastKnownETag = cloudItem.ETag;
            }

            // Nur Dateien herunterladen, die lokal fehlen
            await DownloadMissingProjectFilesAsync(driveId, targetFolderId);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim intelligenten Sync: {ex.Message}");
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private static async Task DownloadMissingProjectFilesAsync(string driveId, string rootFolderId)
    {
        if (CurrentAuth?.GraphClient == null || GlobalJson.Data?.Plans == null) return;

        string? projectDir = Path.GetDirectoryName(GlobalJson.GetFilePath());
        if (string.IsNullOrEmpty(projectDir)) return;

        var project = ProjectItem.Current;

        foreach (var planPair in GlobalJson.Data.Plans)
        {
            var plan = planPair.Value;

            // Plaene pruefen
            if (!string.IsNullOrEmpty(plan.File))
            {
                string localPath = Path.Combine(projectDir, project.PlanFolder, plan.File);

                if (!File.Exists(localPath))
                    await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.PlanFolder}/{plan.File}", localPath);
            }

            if (plan.Pins == null) continue;

            // CustomPins innerhalb des Plans pruefen
            foreach (var pinPair in plan.Pins)
            {
                var pin = pinPair.Value;

                if (pin.IsCustomPin || !string.IsNullOrEmpty(pin.PinIcon))
                {
                    string localPinPath = Path.Combine(projectDir, project.CustomPinsFolder, pin.PinIcon);
                    if (File.Exists(localPinPath)) continue;

                    await DownloadSpecificFileAsync(driveId, rootFolderId,
                        $"{project.CustomPinsFolder}/{pin.PinIcon}", localPinPath);

                    string dataFile = Path.ChangeExtension(pin.PinIcon, ".data");
                    string localDataPath = Path.Combine(projectDir, project.CustomPinsFolder, dataFile);

                    await DownloadSpecificFileAsync(driveId, rootFolderId,
                        $"{project.CustomPinsFolder}/{dataFile}", localDataPath);
                }
                else if (pin.IsCustomIcon && !string.IsNullOrEmpty(pin.PinIcon))
                {
                    await EnsureCustomIconAvailableAsync(driveId, rootFolderId, pin.PinIcon);
                }
            }
        }
    }

    private static async Task EnsureCustomIconAvailableAsync(string driveId, string rootFolderId, string iconFileName)
    {
        // Bereits registriert (z.B. selbst erstellt oder frueherer Sync) - nichts zu tun
        if (IconLookup.Get(iconFileName) != null) return;

        string iconDir = Path.Combine(Settings.DataDirectory, "customicons");
        string localPngPath = Path.Combine(iconDir, iconFileName);
        var project = ProjectItem.Current;
        string metaFileName = Path.ChangeExtension(iconFileName, ".json");
        string localMetaPath = Path.Combine(iconDir, metaFileName);

        Directory.CreateDirectory(iconDir);

        if (!File.Exists(localPngPath))
            await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomIconsFolder}/{iconFileName}", localPngPath);

        await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomIconsFolder}/{metaFileName}", localMetaPath);

        // Download fehlgeschlagen - naechster Sync-Zyklus versucht es erneut
        if (!File.Exists(localPngPath) || !File.Exists(localMetaPath)) return;

        try
        {
            var meta = JsonSerializer.Deserialize<CustomIconMetadata>(await File.ReadAllTextAsync(localMetaPath));
            if (meta == null) return;

            var newIconItem = new IconItem(
            iconFileName,
            meta.DisplayName,
            new Point(meta.AnchorX, meta.AnchorY),
            new Size(meta.SizeWidth, meta.SizeHeight),
            meta.IsRotationLocked,
            meta.IsAutoScaleLocked,
            isCustomIcon: true,
            SKColor.Parse(meta.PinColorHex),
            meta.IconScale,
            meta.Category,
            isDefaultIcon: false);

            // Exakt derselbe Ablauf wie in PopupIconEdit.OnOkClicked
            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), newIconItem);
            IconLookup.AddOrUpdate(newIconItem);

            Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out var iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            IconLookup.Initialize(Settings.IconData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Registrieren des CustomIcons '{iconFileName}': {ex.Message}");
        }
        try { File.Delete(localMetaPath); } catch { /* unkritisch */ }
    }

    private static async Task DownloadSpecificFileAsync(string driveId, string rootFolderId, string relativeCloudPath, string localDestinationPath)
    {
        if (CurrentAuth?.GraphClient == null) return;

        try
        {
            var fileStream = await CurrentAuth.GraphClient.Drives[driveId].Items[rootFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream == null) return;

            string? directory = Path.GetDirectoryName(localDestinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var localFile = File.Create(localDestinationPath);
            await fileStream.CopyToAsync(localFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Nachladen der Datei {relativeCloudPath}: {ex.Message}");
        }
    }

    public static async Task DeleteCloudFileAsync(string relativeCloudPath)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;

        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)) return;

        try
        {
            string cloudPath = relativeCloudPath.Replace("\\", "/");

            await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(cloudPath)
                .DeleteAsync();

            Console.WriteLine($"Erfolgreich in der Cloud geloescht: {cloudPath}");
        }
        catch (ODataError)
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
        StopCloudPolling();

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                    await CheckETagAndSyncAsync();
            }
            catch (OperationCanceledException)
            {
                // Timer wurde regulaer beendet
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
            // Nur Metadaten abrufen - enthaelt den ETag, kostet kaum Datenvolumen
            var cloudItem = await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            if (cloudItem?.ETag == null) return;

            // Wenn noch kein ETag gespeichert ist, initialisieren
            if (string.IsNullOrEmpty(_lastKnownETag))
            {
                _lastKnownETag = cloudItem.ETag;
                return;
            }

            if (cloudItem.ETag != _lastKnownETag)
            {
                _lastKnownETag = cloudItem.ETag;
                await SyncJsonOnlyFromCloudAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Polling-Check fehlgeschlagen: {ex.Message}");
        }
    }

    // ===============================================================
    //  Bedarfs-Download
    // ===============================================================

    public static async Task<bool> DownloadMediaOnDemandAsync(string fileName, string subFolder = "", string? driveId = null, string? folderId = null, string? projectDir = null)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        // Explizit uebergebene Werte oder Fallback auf das aktuell geladene GlobalJson
        string? effectiveDriveId = driveId ?? GlobalJson.Data?.CloudDriveId;
        string? effectiveFolderId = folderId ?? GlobalJson.Data?.CloudFolderId;
        string? effectiveProjectDir = projectDir ?? Path.GetDirectoryName(GlobalJson.GetFilePath());

        if (string.IsNullOrEmpty(effectiveDriveId) ||
            string.IsNullOrEmpty(effectiveFolderId) ||
            string.IsNullOrEmpty(effectiveProjectDir))
            return false;

        try
        {
            string relativeCloudPath = string.IsNullOrWhiteSpace(subFolder)
                ? fileName
                : $"{subFolder.Trim('/', '\\')}/{fileName}".Replace("\\", "/");

            string localDestinationPath = string.IsNullOrWhiteSpace(subFolder)
                ? Path.Combine(effectiveProjectDir, fileName)
                : Path.Combine(effectiveProjectDir, subFolder, fileName);

            // Abbruch, falls die Datei bereits lokal existiert
            if (File.Exists(localDestinationPath)) return true;

            var fileStream = await CurrentAuth.GraphClient.Drives[effectiveDriveId].Items[effectiveFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream == null) return false;

            string? targetDir = Path.GetDirectoryName(localDestinationPath);
            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            using var localFile = File.Create(localDestinationPath);
            await fileStream.CopyToAsync(localFile);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bedarfs-Download fehlgeschlagen fuer {fileName} in '{subFolder}': {ex.Message}");
            return false;
        }
    }

    // Ueberladung fuer Abwaertskompatibilitaet mit bestehendem Foto/Thumbnail-Code
    public static Task<bool> DownloadMediaOnDemandAsync(string fileName, bool isThumbnail)
    {
        var project = ProjectItem.Current;
        return DownloadMediaOnDemandAsync(fileName, isThumbnail ? project.ThumbnailFolder : project.ImageFolder);
    }

    public static async Task<JsonDataModel?> GetRemoteProjectDataAsync(string driveId, string folderId, string jsonFileName)
    {
        if (CurrentAuth?.GraphClient == null ||
            !CurrentAuth.IsLoggedIn ||
            string.IsNullOrWhiteSpace(driveId) ||
            string.IsNullOrWhiteSpace(folderId) ||
            string.IsNullOrWhiteSpace(jsonFileName))
            return null;

        try
        {
            using var stream = await CurrentAuth.GraphClient
                .Drives[driveId]
                .Items[folderId]
                .ItemWithPath(jsonFileName)
                .Content
                .GetAsync();

            if (stream == null)
                return null;

            return await JsonSerializer.DeserializeAsync<JsonDataModel>(stream, GlobalJson.GetOptions());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cloud-JSON konnte nicht gelesen werden ({jsonFileName}): {ex.Message}");
            return null;
        }
    }

    // ===============================================================
    //  Zuruecksetzen
    // ===============================================================

    public static void ResetCloudSync()
    {
        TargetFolderId = null;
        _lastKnownETag = null;
        _lastKnownCloudSyncTime = DateTimeOffset.MinValue;

        // Wichtig: sonst merged das naechste Projekt gegen einen fremden Zeitstempel
        _lastKnownWriteTime = default;

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        _pendingUploadQueue.Clear();
    }

    private static string SanitizeName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}

public class RemoteProjectDto
{
    public string FileName { get; set; } = string.Empty;

    public string DriveId { get; set; } = string.Empty;

    public string FolderId { get; set; } = string.Empty;

    // ID der eigentlichen JSON-Datei
    public string ItemId { get; set; } = string.Empty;

    // Aus Object_name im JSON
    public string ObjectName { get; set; } = string.Empty;

    // Optional: Pfad für Anzeige im Suchdialog
    public string FolderPath { get; set; } = string.Empty;

    public DateTimeOffset LastModified { get; set; }

    public string DisplayName =>
    string.IsNullOrWhiteSpace(ObjectName)
    ? Path.GetFileNameWithoutExtension(FileName)
    : ObjectName;
}

public class CloudDownloadFile
{
    public string Id { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

public class CustomIconMetadata
{
    public string? DisplayName { get; set; }
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public double SizeWidth { get; set; }
    public double SizeHeight { get; set; }
    public bool IsRotationLocked { get; set; }
    public bool IsAutoScaleLocked { get; set; }
    public string? PinColorHex { get; set; }
    public double IconScale { get; set; }
    public string? Category { get; set; }
}