using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using SkiaSharp;
using SnapDoc.Messages;
using SnapDoc.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using static SnapDoc.Helper;
using static SnapDoc.Models.SyncStampExtensions;

namespace SnapDoc.Services;

public static class SaveManager
{
    private const int MaxSaveRetries = 3;

    /// <summary>Stempel fuer Altprojekte ohne Sync-Metadaten. MUSS auf allen
    /// Geraeten identisch sein, sonst gewinnt zufaellig das Geraet, das zuletzt
    /// geoeffnet hat.</summary>
    private static readonly DateTimeOffset LegacySeed =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly ConcurrentDictionary<(string LocalFilePath, string SubFolder), byte> _pendingUploadQueue = new();
    private static readonly ConcurrentDictionary<string, byte> _pendingCloudDeletes = new();

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

    // ===============================================================
    //  Initialisierung und Migration
    // ===============================================================

    public static void Initialize(string filePath)
    {
        GlobalJson.LoadFromFile(filePath);
        PrepareLoadedData();

        _lastKnownWriteTime = File.Exists(filePath)
            ? File.GetLastWriteTimeUtc(filePath)
            : default;
    }

    /// <summary>
    /// Bringt frisch geladene Daten in einen sync-faehigen Zustand:
    /// fehlende Stempel nachziehen und abgelaufene Tombstones entfernen.
    /// </summary>
    private static void PrepareLoadedData()
    {
        // ACHTUNG: beide Aufrufe muessen laufen. Mit "||" wuerde der zweite
        // uebersprungen, sobald der erste true liefert.
        bool backfilled = BackfillStamps(GlobalJson.Data);
        bool purged = SyncOps.PurgeTombstones(GlobalJson.Data);

        if (backfilled || purged)
            GlobalJson.SaveToFile();
    }

    /// <summary>
    /// Setzt fehlende Zeitstempel auf einen festen Seed. Ohne das liefert
    /// SyncClock.Compare ueberall 0 und der Merge uebernimmt nichts.
    /// Bewusst ohne Touch(), damit jede echte spaetere Aenderung gewinnt.
    /// </summary>
    private static bool BackfillStamps(JsonDataModel? data)
    {
        if (data == null) return false;
        bool changed = false;

        if (data.ModifiedAt == default)
        {
            data.ModifiedAt = LegacySeed;
            data.ModifiedBy = "legacy";
            changed = true;
        }

        foreach (var plan in data.Plans?.Values ?? Enumerable.Empty<Plan>())
        {
            if (plan.ModifiedAt == default)
            {
                plan.ModifiedAt = LegacySeed;
                plan.ModifiedBy = "legacy";
                changed = true;
            }

            foreach (var pin in plan.Pins?.Values ?? Enumerable.Empty<Pin>())
            {
                if (pin.ModifiedAt == default)
                {
                    pin.ModifiedAt = LegacySeed;
                    pin.ModifiedBy = "legacy";
                    changed = true;
                }

                foreach (var foto in pin.Fotos?.Values ?? Enumerable.Empty<Foto>())
                {
                    if (foto.ModifiedAt == default)
                    {
                        foto.ModifiedAt = LegacySeed;
                        foto.ModifiedBy = "legacy";
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    // ===============================================================
    //  Cloud-Loeschungen
    // ===============================================================

    /// <summary>
    /// Merkt eine Cloud-Datei zum Loeschen vor. Sie wird erst entfernt, wenn
    /// die zugehoerige Tombstone erfolgreich hochgeladen wurde - sonst kennt
    /// das andere Geraet den Pin noch als lebendig und laedt ins Leere.
    /// </summary>
    public static void QueueCloudDelete(string relativeCloudPath)
    {
        if (!string.IsNullOrEmpty(relativeCloudPath))
            _pendingCloudDeletes.TryAdd(relativeCloudPath.Replace("\\", "/"), 0);
    }

    private static async Task ProcessPendingCloudDeletionsAsync()
    {
        if (_pendingCloudDeletes.IsEmpty) return;
        if (CurrentAuth is not { IsLoggedIn: true }) return;

        foreach (var path in _pendingCloudDeletes.Keys.ToList())
            if (_pendingCloudDeletes.TryRemove(path, out _))
                await DeleteCloudFileAsync(path);
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
                            var cloudData = await GlobalJson.DeserializeAsync(cloudStream);
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

        // Erst jetzt duerfen Binaerdateien verschwinden - die Tombstones
        // liegen nach dem erfolgreichen Upload sicher in der Cloud.
        await ProcessPendingCloudDeletionsAsync();

        // Nach dem JSON-Sync alle angesammelten Dateien abarbeiten
        if (!_pendingUploadQueue.IsEmpty && CurrentAuth is { IsLoggedIn: true })
        {
            foreach (var fileItem in _pendingUploadQueue.Keys.ToList())
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
        {
            PrepareLoadedData();
            return;
        }

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

            var cloudData = await GlobalJson.DeserializeAsync(stream);
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

            // Das Cloud-Projekt kann von einem noch nicht migrierten Geraet
            // stammen - Stempel nachziehen, dann Tombstones aufraeumen.
            PrepareLoadedData();
            _lastKnownWriteTime = File.GetLastWriteTimeUtc(localFilePath);

            // Fehlende Projektdateien beim Oeffnen nachladen
            await DownloadMissingProjectFilesAsync(myDrive.Id, targetFolderId);
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
            var externalData = GlobalJson.ReadFromFile(filePath);
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

    /// <summary>
    /// Vereinigt den lokalen Stand mit dem Cloud-Stand. Geloescht wird nur,
    /// was eine Tombstone traegt - fehlende Schluessel gelten als "dem anderen
    /// Geraet noch unbekannt" und bleiben erhalten.
    /// </summary>
    public static void MergeModels(JsonDataModel? local, JsonDataModel? cloud)
    {
        if (local == null || cloud == null) return;

        // Ohne das Gate stempelt jede Zuweisung unten das Objekt mit DIESEM
        // Geraet - empfangene Cloud-Werte gaelten dann als lokale Aenderung.
        using var _ = SyncStampGate.Suspend();

        bool titleImageChanged = local.TitleImage != cloud.TitleImage;
        string oldTitleImage = local.TitleImage;
        bool projectDetailsChanged = false;

        // --- Projektdetails: nur uebernehmen, wenn die Cloud neuer ist ----
        if (SyncClock.Compare(cloud, local) > 0)
        {
            projectDetailsChanged =
                local.Client_name != cloud.Client_name ||
                local.Working_title != cloud.Working_title ||
                local.Object_address != cloud.Object_address ||
                local.Project_nr != cloud.Project_nr ||
                local.Object_name != cloud.Object_name ||
                local.Project_manager != cloud.Project_manager ||
                local.Creation_date != cloud.Creation_date ||
                titleImageChanged;

            if (projectDetailsChanged)
            {
                local.Client_name = cloud.Client_name;
                local.Working_title = cloud.Working_title;
                local.Object_address = cloud.Object_address;
                local.Project_nr = cloud.Project_nr;
                local.Object_name = cloud.Object_name;
                local.Project_manager = cloud.Project_manager;
                local.Creation_date = cloud.Creation_date;

                local.ModifiedAt = cloud.ModifiedAt;
                local.ModifiedBy = cloud.ModifiedBy;
            }

            if (titleImageChanged)
            {
                local.TitleImage = cloud.TitleImage;
                local.TitleImageSize = cloud.TitleImageSize;
                WeakReferenceMessenger.Default.Send(
                    new TitleImageChangedMessage(oldTitleImage, cloud.TitleImage));
            }
        }

        if (cloud.Plans == null)
        {
            if (projectDetailsChanged)
                WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));
            return;
        }

        local.Plans ??= [];
        bool planStructureChanged = false;

        // --- Plaene: Vereinigung beider Schluesselmengen ------------------
        foreach (var planId in local.Plans.Keys.Union(cloud.Plans.Keys).ToList())
        {
            bool hasLocal = local.Plans.TryGetValue(planId, out var localPlan);
            bool hasCloud = cloud.Plans.TryGetValue(planId, out var cloudPlan);

            // Nur lokal => offline angelegt, der Cloud noch unbekannt.
            // BEHALTEN. Der Upload direkt nach diesem Merge schickt ihn hoch.
            // Kein planStructureChanged: der Plan ist lokal laengst sichtbar,
            // sonst wuerde jeder Poll einen kompletten Shell-Reload ausloesen.
            if (hasLocal && !hasCloud)
                continue;

            // Nur in der Cloud => von einem anderen Geraet.
            if (!hasLocal && hasCloud && cloudPlan != null)
            {
                local.Plans.Add(planId, cloudPlan);
                if (cloudPlan.IsLive()) planStructureChanged = true;
                continue;
            }

            if (localPlan is null || cloudPlan is null) continue;

            bool wasVisible = localPlan.IsLive();

            MergePlan(planId, localPlan, cloudPlan);

            // Sichtbarkeit gewechselt => Planliste muss neu aufgebaut werden
            if (wasVisible != localPlan.IsLive())
            {
                planStructureChanged = true;
                if (!localPlan.IsLive())
                    WeakReferenceMessenger.Default.Send(new PlanDeletedMessage(planId));
            }
        }

        // Reihenfolge nach der Cloud ausrichten, lokale Neuzugaenge hinten
        // anhaengen (die Cloud kennt sie noch nicht).
        var orderedKeys = cloud.Plans.Keys.Where(local.Plans.ContainsKey)
            .Concat(local.Plans.Keys.Where(k => !cloud.Plans.ContainsKey(k)))
            .ToList();

        if (!local.Plans.Keys.SequenceEqual(orderedKeys))
        {
            var ordered = new Dictionary<string, Plan>();
            foreach (var key in orderedKeys)
                ordered.Add(key, local.Plans[key]);
            local.Plans = ordered;
            planStructureChanged = true;
        }

        // --- UI-Benachrichtigungen ---------------------------------------
        if (projectDetailsChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));

        // Nur bei echter Strukturaenderung ein Shell-Reload ausloesen
        if (planStructureChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.PlanListUpdated));
    }

    private static void MergePlan(string planId, Plan localPlan, Plan cloudPlan)
    {
        // --- Plan-Eigenschaften: juengerer Stand gewinnt ------------------
        if (SyncClock.Compare(cloudPlan, localPlan) > 0)
        {
            bool nameOrExportChanged = localPlan.Name != cloudPlan.Name ||
                                       localPlan.AllowExport != cloudPlan.AllowExport;

            bool detailsChanged = localPlan.Description != cloudPlan.Description ||
                                  localPlan.IsGrayscale != cloudPlan.IsGrayscale ||
                                  localPlan.PlanColor != cloudPlan.PlanColor ||
                                  nameOrExportChanged;

            if (detailsChanged ||
                localPlan.File != cloudPlan.File ||
                localPlan.ImageSize != cloudPlan.ImageSize ||
                localPlan.DeletedAt != cloudPlan.DeletedAt)
            {
                localPlan.Name = cloudPlan.Name;
                localPlan.File = cloudPlan.File;
                localPlan.Description = cloudPlan.Description;
                localPlan.ImageSize = cloudPlan.ImageSize;
                localPlan.IsGrayscale = cloudPlan.IsGrayscale;
                localPlan.PlanColor = cloudPlan.PlanColor;
                localPlan.AllowExport = cloudPlan.AllowExport;

                localPlan.DeletedAt = cloudPlan.DeletedAt;
                localPlan.ModifiedAt = cloudPlan.ModifiedAt;
                localPlan.ModifiedBy = cloudPlan.ModifiedBy;

                if (detailsChanged && localPlan.IsLive())
                    WeakReferenceMessenger.Default.Send(
                        new PlanDetailsChangedMessage((planId, cloudPlan.Name,
                            cloudPlan.Description, cloudPlan.IsGrayscale, cloudPlan.PlanColor)));
            }
        }

        // --- Pins: Vereinigung beider Schluesselmengen --------------------
        localPlan.Pins ??= [];
        var cloudPins = cloudPlan.Pins ?? [];

        foreach (var pinId in localPlan.Pins.Keys.Union(cloudPins.Keys).ToList())
        {
            bool hasLocal = localPlan.Pins.TryGetValue(pinId, out var localPin);
            bool hasCloud = cloudPins.TryGetValue(pinId, out var cloudPin);

            // Nur lokal => offline angelegt, noch nicht hochgeladen.
            // NICHT loeschen - das war der urspruengliche Fehler.
            if (hasLocal && !hasCloud)
                continue;

            // Nur in der Cloud => von einem anderen Geraet.
            if (!hasLocal && hasCloud && cloudPin != null)
            {
                localPlan.Pins.Add(pinId, cloudPin);
                if (cloudPin.IsLive())
                    WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
                continue;
            }

            if (localPin is null || cloudPin is null) continue;

            // Beide kennen ihn => aelterer Stand verliert.
            if (SyncClock.Compare(cloudPin, localPin) <= 0)
                continue;

            bool wasVisible = localPin.IsLive();

            bool uiNeedsRedraw = localPin.Pos != cloudPin.Pos ||
                                 localPin.PinRotation != cloudPin.PinRotation ||
                                 localPin.PinIcon != cloudPin.PinIcon ||
                                 localPin.PinColor != cloudPin.PinColor ||
                                 localPin.PinScale != cloudPin.PinScale ||
                                 localPin.IsLockAutoScale != cloudPin.IsLockAutoScale ||
                                 localPin.IsLockRotate != cloudPin.IsLockRotate;

            CopyPinValues(localPin, cloudPin);
            MergeFotos(localPin, cloudPin);

            if (wasVisible && !localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinDeletedMessage(pinId));
            else if (!wasVisible && localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
            else if (uiNeedsRedraw && localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinId));
        }

        // Zaehler beruecksichtigt nur sichtbare Pins
        int liveCount = SyncOps.LivePinCount(localPlan);
        if (localPlan.PinCount != liveCount)
            localPlan.PinCount = liveCount;
    }

    /// <summary>Uebertraegt alle Pin-Nutzdaten inkl. Sync-Metadaten.</summary>
    private static void CopyPinValues(Pin target, Pin source)
    {
        target.Anchor = source.Anchor;
        target.DateTime = source.DateTime;
        target.IsWebMapPin = source.IsWebMapPin;
        target.IsCustomPin = source.IsCustomPin;
        target.IsCustomIcon = source.IsCustomIcon;
        target.Pos = source.Pos;
        target.PinPriority = source.PinPriority;
        target.GeoLocation = source.GeoLocation;
        target.IsAllowExport = source.IsAllowExport;
        target.IsLockAutoScale = source.IsLockAutoScale;
        target.IsLockPosition = source.IsLockPosition;
        target.IsLockRotate = source.IsLockRotate;
        target.OnPlanId = source.OnPlanId;
        target.PinColor = source.PinColor;
        target.PinIcon = source.PinIcon;
        target.PinName = source.PinName;
        target.PinDesc = source.PinDesc;
        target.Size = source.Size;
        target.SelfId = source.SelfId;
        target.PinScale = source.PinScale;
        target.PinLocation = source.PinLocation;
        target.PinRotation = source.PinRotation;

        target.DeletedAt = source.DeletedAt;
        target.ModifiedAt = source.ModifiedAt;
        target.ModifiedBy = source.ModifiedBy;
    }

    private static void MergeFotos(Pin localPin, Pin cloudPin)
    {
        localPin.Fotos ??= [];
        var cloudFotos = cloudPin.Fotos ?? [];

        foreach (var fotoId in localPin.Fotos.Keys.Union(cloudFotos.Keys).ToList())
        {
            bool hasLocal = localPin.Fotos.TryGetValue(fotoId, out var localFoto);
            bool hasCloud = cloudFotos.TryGetValue(fotoId, out var cloudFoto);

            // Offline aufgenommen, noch nicht hochgeladen => behalten.
            if (hasLocal && !hasCloud) continue;

            if (!hasLocal && hasCloud && cloudFoto != null)
            {
                localPin.Fotos.Add(fotoId, cloudFoto);
                continue;
            }

            if (localFoto is null || cloudFoto is null) continue;
            if (SyncClock.Compare(cloudFoto, localFoto) <= 0) continue;

            localFoto.AllowExport = cloudFoto.AllowExport;
            localFoto.File = cloudFoto.File;
            localFoto.HasOverlay = cloudFoto.HasOverlay;
            localFoto.DateTime = cloudFoto.DateTime;
            localFoto.ImageSize = cloudFoto.ImageSize;

            localFoto.DeletedAt = cloudFoto.DeletedAt;
            localFoto.ModifiedAt = cloudFoto.ModifiedAt;
            localFoto.ModifiedBy = cloudFoto.ModifiedBy;
        }
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

            var cloudData = await GlobalJson.DeserializeAsync(cloudStream);
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

        // Nur lebende Objekte: fuer geloeschte Pins waere der Download sinnlos.
        foreach (var planPair in SyncOps.LivePlans(GlobalJson.Data))
        {
            var plan = planPair.Value;

            // Plaene pruefen
            if (!string.IsNullOrEmpty(plan.File))
            {
                string localPath = Path.Combine(projectDir, project.PlanFolder, plan.File);
                if (!File.Exists(localPath))
                    await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.PlanFolder}/{plan.File}", localPath);
            }

            // CustomPins innerhalb des Plans pruefen
            foreach (var pinPair in SyncOps.LivePins(plan))
            {
                var pin = pinPair.Value;

                if (pin.IsCustomPin && !string.IsNullOrEmpty(pin.PinIcon))
                {
                    string localPinPath = Path.Combine(projectDir, project.CustomPinsFolder, pin.PinIcon);
                    if (File.Exists(localPinPath)) continue;

                    await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomPinsFolder}/{pin.PinIcon}", localPinPath);

                    string dataFile = Path.ChangeExtension(pin.PinIcon, ".data");
                    string localDataPath = Path.Combine(projectDir, project.CustomPinsFolder, dataFile);
                    await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomPinsFolder}/{dataFile}", localDataPath);
                }
                else if (pin.IsCustomIcon && !string.IsNullOrEmpty(pin.PinIcon))
                {
                    await EnsureCustomIconAvailableAsync(driveId, rootFolderId, pin.PinIcon);
                    WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinPair.Key));
                }
            }
        }
    }

    private static async Task EnsureCustomIconAvailableAsync(string driveId, string rootFolderId, string iconFileName)
    {
        string iconDir = Path.Combine(Settings.DataDirectory, "customicons");
        string localPngPath = Path.Combine(iconDir, iconFileName);

        // Bereits vorhanden UND registriert - nichts zu tun
        if (File.Exists(localPngPath) && IconLookup.Get(iconFileName)?.FileName == iconFileName)
            return;

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
            var cloudItem = await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(CloudFileName)
                .GetAsync();

            if (cloudItem?.ETag == null) return;

            bool baselineMissing = string.IsNullOrEmpty(_lastKnownETag);
            bool etagChanged = !baselineMissing && cloudItem.ETag != _lastKnownETag;

            if (baselineMissing || etagChanged)
            {
                _lastKnownETag = cloudItem.ETag;

                // Auch beim ERSTEN Poll nach Login/Projektstart synchronisieren,
                // damit Aenderungen aus der Offline-Phase nachgeholt werden.
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

            return await GlobalJson.DeserializeAsync(stream);
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
        _pendingCloudDeletes.Clear();
    }

    private static string SanitizeName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}

public class RemoteProjectDto
{
    public string FileName { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;

    // Aus Object_name im JSON
    public string ObjectName { get; set; } = string.Empty;

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
