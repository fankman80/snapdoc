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

    /// <summary>
    /// Stempel fuer Altprojekte ohne Sync-Metadaten. MUSS auf allen Geraeten identisch sein.
    /// </summary>
    private static readonly DateTimeOffset LegacySeed = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly Lock _fileLock = new();
    private static readonly Lock _debounceLock = new();
    private static readonly SemaphoreSlim _saveGate = new(1, 1);
    private static CancellationTokenSource? _pollingCts;
    private static CancellationTokenSource? _debounceCts;
    private static DateTime _lastKnownWriteTime;
    private static string? _lastKnownETag;
    private static long _localVersion;
    private static bool _connectivityHooked;
    private static string CloudFileName => SettingsService.DefaultJson;
    private static readonly ConcurrentDictionary<string, byte> _verifiedCloudIcons = new(StringComparer.OrdinalIgnoreCase);
    private static volatile bool _missingFilesPending;

    // Merge-Nebenwirkungen: veraltete Bilddateien + UI-Hinweis fuer Fotolisten
    private static readonly ConcurrentQueue<(string File, bool DropOriginal)> _staleMedia = new();
    private static volatile bool _fotosChanged;

    public static string? TargetFolderId { get; set; }
    public static AuthService? CurrentAuth { get; set; }

    // ===============================================================
    //  Initialisierung und Migration
    // ===============================================================

    public static void Initialize(string filePath)
    {
        GlobalJson.LoadFromFile(filePath);
        SyncState.Bind(filePath);
        PrepareLoadedData();

        _lastKnownWriteTime = File.Exists(filePath)
            ? File.GetLastWriteTimeUtc(filePath)
            : default;
    }

    /// <summary>
    /// Fehlende Stempel nachziehen, Dateien geloeschter Objekte lokal entfernen
    /// und abgelaufene Tombstones aufraeumen.
    /// </summary>
    private static void PrepareLoadedData()
    {
        if (GlobalJson.Data == null) return;

        bool backfilled = BackfillStamps(GlobalJson.Data);

        // VOR dem Purge, sonst sind die Dateinamen der Tombstones weg
        PurgeLocalTombstoneFiles();

        bool purged = SyncOps.PurgeTombstones(GlobalJson.Data);

        if (backfilled || purged)
            GlobalJson.SaveToFile();
    }

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
    //  Aenderungen melden / Debounce
    // ===============================================================

    public static void NotifyDataChanged(int delayMilliseconds = 2000)
        => NotifyDataChanged([], delayMilliseconds);

    public static void NotifyDataChanged(string localFilePath, string subFolder, int delayMilliseconds = 2000)
        => NotifyDataChanged([(localFilePath, subFolder)], delayMilliseconds);

    public static void NotifyDataChanged(IEnumerable<(string LocalFilePath, string SubFolder)> files, int delayMilliseconds = 2000)
    {
        MarkLocalChange();

        // Uploads nur fuer verknuepfte Projekte vormerken. Beim spaeteren
        // Verknuepfen laedt UploadDirectoryRecursiveAsync ohnehin alles hoch.
        bool linked = !string.IsNullOrEmpty(GlobalJson.Data?.CloudFolderId);
        if (linked)
        {
            foreach (var (localFilePath, subFolder) in files)
            {
                if (!string.IsNullOrEmpty(localFilePath))
                    SyncState.AddUpload(localFilePath, subFolder);
            }
        }

        ScheduleSync(delayMilliseconds);
    }

    /// <summary>
    /// Merkt sich persistent, dass lokal etwas geaendert wurde. Erst ein
    /// erfolgreicher Upload setzt das Flag zurueck - auch nach App-Neustart.
    /// </summary>
    private static void MarkLocalChange()
    {
        string path = GlobalJson.GetFilePath();
        if (string.IsNullOrEmpty(path)) return;

        SyncState.Bind(path);
        Interlocked.Increment(ref _localVersion);
        SyncState.MarkDirty();
    }

    /// <summary>Sync anstossen, ohne eine lokale Aenderung zu melden.</summary>
    private static void ScheduleSync(int delayMilliseconds = 2000)
    {
        CancellationToken token;
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

    public static async Task SaveWithSyncCheckAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            await SyncCoreAsync();
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>
    /// Ablauf: lokal speichern -> Cloud-Stand mergen (immer, wenn das ETag
    /// abweicht) -> falls lokal etwas offen ist hochladen -> erst danach
    /// Binaerdateien geloeschter Objekte entfernen.
    /// </summary>
    /// <returns>true, wenn lokal und Cloud danach konsistent sind.</returns>
    private static async Task<bool> SyncCoreAsync()
    {
        string filePath = GlobalJson.GetFilePath();
        if (string.IsNullOrEmpty(filePath) || GlobalJson.Data == null)
            return false;

        SyncState.Bind(filePath);

        // 1. IMMER lokal speichern
        SaveLocal(filePath);
        PurgeLocalTombstoneFiles();

        var data = GlobalJson.Data;
        if (string.IsNullOrEmpty(data.CloudDriveId) || string.IsNullOrEmpty(data.CloudFolderId))
            return false;

        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn)
            return false;

        string driveId = data.CloudDriveId;
        string folderId = data.CloudFolderId;
        var graph = CurrentAuth.GraphClient;

        try
        {
            bool synced = false;

            for (int attempt = 0; attempt <= MaxSaveRetries && !synced; attempt++)
            {
                long versionAtStart = Interlocked.Read(ref _localVersion);

                // 2. Cloud-Stand pruefen und bei Abweichung IMMER mergen
                var cloudItem = await GetCloudItemOrNullAsync(driveId, folderId, CloudFileName);

                if (cloudItem != null && cloudItem.ETag != _lastKnownETag)
                {
                    if (!await PullAndMergeAsync(driveId, folderId, filePath))
                        return false;

                    _lastKnownETag = cloudItem.ETag;
                }

                // 3. Nichts Lokales offen -> kein Upload noetig (verhindert Ping-Pong)
                if (!SyncState.IsDirty && cloudItem != null)
                {
                    synced = true;
                    break;
                }

                // 4. Binaerdateien VOR der JSON hochladen, damit das andere
                //    Geraet beim Lesen der neuen JSON die Dateien bereits findet.
                await ProcessPendingUploadsAsync();
                await EnsureCustomIconsInCloudAsync(driveId, folderId);

                string payload;
                lock (_fileLock)
                    payload = GlobalJson.ToJson();

                try
                {
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
                    var uploaded = await graph.Drives[driveId].Items[folderId]
                        .ItemWithPath(CloudFileName)
                        .Content
                        .PutAsync(stream, rc =>
                        {
                            if (cloudItem != null && !string.IsNullOrEmpty(_lastKnownETag))
                                rc.Headers.Add("If-Match", _lastKnownETag);
                        });

                    _lastKnownETag = uploaded?.ETag;

                    // Nur zuruecksetzen, wenn waehrend des Uploads nichts Neues dazukam
                    if (Interlocked.Read(ref _localVersion) == versionAtStart)
                        SyncState.ClearDirty();

                    synced = true;
                }
                catch (ODataError ex) when (IsPreconditionFailed(ex))
                {
                    Console.WriteLine($"ETag-Konflikt (Versuch {attempt + 1}/{MaxSaveRetries + 1}) - merge erneut.");
                    _lastKnownETag = null; // erzwingt Merge im naechsten Durchlauf
                    await Task.Delay(500 * (attempt + 1));
                }
            }

            if (!synced)
            {
                Console.WriteLine("Cloud-Sync nach mehreren ETag-Konflikten abgebrochen - naechster Zyklus versucht es erneut.");
                return false;
            }

            // 5. Erst jetzt sind alle Tombstones sicher in der Cloud.
            if (!SyncState.IsDirty)
            {
                QueueCloudDeletesForTombstones();
                await ProcessPendingCloudDeletionsAsync();
            }

            // Uploads, die waehrend des Syncs dazukamen oder zuvor fehlschlugen
            await ProcessPendingUploadsAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Offline, Token abgelaufen, ... -> Dirty bleibt gesetzt, Polling versucht es erneut
            Console.WriteLine($"Cloud-Sync fehlgeschlagen: {ex.Message}");
            return false;
        }
    }

    private static void SaveLocal(string filePath)
    {
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

        ApplyMergeSideEffects();
    }

    private static async Task<bool> PullAndMergeAsync(string driveId, string folderId, string filePath)
    {
        using var cloudStream = await CurrentAuth!.GraphClient!.Drives[driveId].Items[folderId]
            .ItemWithPath(CloudFileName)
            .Content
            .GetAsync();

        if (cloudStream == null) return false;

        var cloudData = await GlobalJson.DeserializeAsync(cloudStream);
        if (cloudData == null) return false; // Defekte Cloud-JSON nie ueberschreiben

        // Cloud-Stand eines noch nicht migrierten Geraets angleichen
        BackfillStamps(cloudData);

        lock (_fileLock)
        {
            // Projekt wurde waehrenddessen gewechselt
            if (GlobalJson.GetFilePath() != filePath) return false;

            MergeModels(GlobalJson.Data, cloudData);
            GlobalJson.SaveToFile();
            _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
        }

        // Tombstones vom anderen Geraet -> lokale Dateileichen entfernen,
        // veraltete Overlay-Bilder verwerfen, Fotolisten informieren
        PurgeLocalTombstoneFiles();
        ApplyMergeSideEffects();
        return true;
    }

    private static async Task<DriveItem?> GetCloudItemOrNullAsync(string driveId, string folderId, string relativePath)
    {
        try
        {
            return await CurrentAuth!.GraphClient!.Drives[driveId].Items[folderId]
                .ItemWithPath(relativePath)
                .GetAsync();
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
    }

    private static bool IsPreconditionFailed(ODataError ex)
        => ex.ResponseStatusCode == 412 || ex.Error?.Code == "conditionNotMet";

    public static async Task LoadDataAsync(AuthService authService, string localFilePath)
    {
        ResetCloudSync();
        CurrentAuth ??= authService;

        // Lokalen Stand laden und mergen - nie ersetzen, sonst gehen
        // offline gesetzte Tombstones verloren.
        Initialize(localFilePath);

        if (!authService.IsLoggedIn || authService.GraphClient == null)
            return;

        try
        {
            var myDrive = await authService.GraphClient.Me.Drive.GetAsync();
            if (myDrive == null || string.IsNullOrEmpty(myDrive.Id))
                return;

            if (GlobalJson.Data != null &&
                (string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) || string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)))
            {
                string? projectDirectory = Path.GetDirectoryName(localFilePath);
                string projectName = !string.IsNullOrWhiteSpace(GlobalJson.Data.Object_name)
                    ? GlobalJson.Data.Object_name
                    : (!string.IsNullOrEmpty(projectDirectory) ? Path.GetFileName(projectDirectory) : "DefaultProject");

                string? targetFolderId = await EnsureCloudFolderStructureAsync(authService, myDrive.Id, SanitizeName(projectName));
                if (string.IsNullOrEmpty(targetFolderId))
                    return;

                GlobalJson.Data.CloudDriveId = myDrive.Id;
                GlobalJson.Data.CloudFolderId = targetFolderId;
            }

            await SyncJsonOnlyFromCloudAsync();
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
    /// Bei zwei Loeschzeitpunkten gewinnt der fruehere, damit beide Geraete
    /// unabhaengig voneinander zum selben Ergebnis kommen.
    /// </summary>
    private static DateTimeOffset? EarliestDeletion(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : (a < b ? a : b);

    /// <summary>
    /// Vereinigt den lokalen Stand mit dem Cloud-Stand.
    ///  - Fehlender Schluessel = dem anderen Geraet noch unbekannt -> behalten.
    ///  - Eigenschaften: juengerer Stempel gewinnt.
    ///  - Loeschung gewinnt IMMER (Delete-Wins).
    /// </summary>
    public static void MergeModels(JsonDataModel? local, JsonDataModel? cloud)
    {
        if (local == null || cloud == null) return;

        using var _ = SyncStampGate.Suspend();

        bool titleImageChanged = local.TitleImage != cloud.TitleImage;
        string oldTitleImage = local.TitleImage;
        bool projectDetailsChanged = false;

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

        foreach (var planId in local.Plans.Keys.Union(cloud.Plans.Keys).ToList())
        {
            bool hasLocal = local.Plans.TryGetValue(planId, out var localPlan);
            bool hasCloud = cloud.Plans.TryGetValue(planId, out var cloudPlan);

            if (hasLocal && !hasCloud)
                continue;

            if (!hasLocal && hasCloud && cloudPlan != null)
            {
                local.Plans.Add(planId, cloudPlan);
                if (cloudPlan.IsLive())
                {
                    planStructureChanged = true;
                    if (HasLiveFotos(cloudPlan)) _fotosChanged = true;
                }
                continue;
            }

            if (localPlan is null || cloudPlan is null) continue;

            bool wasVisible = localPlan.IsLive();
            MergePlan(planId, localPlan, cloudPlan);

            if (wasVisible != localPlan.IsLive())
            {
                planStructureChanged = true;
                _fotosChanged = true;
                if (!localPlan.IsLive())
                    WeakReferenceMessenger.Default.Send(new PlanDeletedMessage(planId));
            }
        }

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

        if (projectDetailsChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.ProjectDetailsUpdated));

        if (planStructureChanged)
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.PlanListUpdated));
    }

    private static bool HasLiveFotos(Plan plan)
        => SyncOps.LivePins(plan).Any(p => SyncOps.LiveFotos(p.Value).Any());

    private static void MergePlan(string planId, Plan localPlan, Plan cloudPlan)
    {
        var localDeletedAt = localPlan.DeletedAt;

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

                // Planname erscheint in der Galerie
                if (nameOrExportChanged) _fotosChanged = true;
            }
        }

        // Delete-Wins
        var mergedDeletedAt = EarliestDeletion(localDeletedAt, cloudPlan.DeletedAt);
        if (localPlan.DeletedAt != mergedDeletedAt)
            localPlan.DeletedAt = mergedDeletedAt;

        localPlan.Pins ??= [];
        var cloudPins = cloudPlan.Pins ?? [];

        foreach (var pinId in localPlan.Pins.Keys.Union(cloudPins.Keys).ToList())
        {
            bool hasLocal = localPlan.Pins.TryGetValue(pinId, out var localPin);
            bool hasCloud = cloudPins.TryGetValue(pinId, out var cloudPin);

            if (hasLocal && !hasCloud)
                continue;

            if (!hasLocal && hasCloud && cloudPin != null)
            {
                localPlan.Pins.Add(pinId, cloudPin);
                if (cloudPin.IsLive())
                {
                    WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
                    if (SyncOps.LiveFotos(cloudPin).Any()) _fotosChanged = true;
                }
                continue;
            }

            if (localPin is null || cloudPin is null) continue;

            bool wasVisible = localPin.IsLive();
            var localPinDeletedAt = localPin.DeletedAt;
            bool uiNeedsRedraw = false;

            if (SyncClock.Compare(cloudPin, localPin) > 0)
            {
                uiNeedsRedraw = localPin.Pos != cloudPin.Pos ||
                                localPin.PinRotation != cloudPin.PinRotation ||
                                localPin.PinIcon != cloudPin.PinIcon ||
                                localPin.PinColor != cloudPin.PinColor ||
                                localPin.PinScale != cloudPin.PinScale ||
                                localPin.IsLockAutoScale != cloudPin.IsLockAutoScale ||
                                localPin.IsLockRotate != cloudPin.IsLockRotate;

                CopyPinValues(localPin, cloudPin);
            }

            // Delete-Wins
            var mergedPinDeletedAt = EarliestDeletion(localPinDeletedAt, cloudPin.DeletedAt);
            if (localPin.DeletedAt != mergedPinDeletedAt)
                localPin.DeletedAt = mergedPinDeletedAt;

            // Fotos IMMER mergen - unabhaengig davon, welcher Pin-Stand neuer ist
            MergeFotos(localPin, cloudPin);

            if (wasVisible != localPin.IsLive())
                _fotosChanged = true;

            if (wasVisible && !localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinDeletedMessage(pinId));
            else if (!wasVisible && localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinAddedMessage((planId, pinId)));
            else if (uiNeedsRedraw && localPin.IsLive())
                WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinId));
        }

        int liveCount = SyncOps.LivePinCount(localPlan);
        if (localPlan.PinCount != liveCount)
            localPlan.PinCount = liveCount;
    }

    /// <summary>
    /// Uebertraegt alle Pin-Nutzdaten inkl. Sync-Metadaten.
    /// </summary>
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

            // Offline aufgenommen, noch nicht hochgeladen -> behalten
            if (hasLocal && !hasCloud) continue;

            if (!hasLocal && hasCloud && cloudFoto != null)
            {
                localPin.Fotos.Add(fotoId, cloudFoto);
                if (cloudFoto.IsLive()) _fotosChanged = true;
                continue;
            }

            if (localFoto is null || cloudFoto is null) continue;

            bool wasLive = localFoto.IsLive();
            var localDeletedAt = localFoto.DeletedAt;

            if (SyncClock.Compare(cloudFoto, localFoto) > 0)
            {
                // Bildinhalt kann sich geaendert haben (Overlay gezeichnet/entfernt),
                // obwohl der Dateiname gleich bleibt -> lokale Kopie verwerfen,
                // damit sie beim naechsten Anzeigen neu geladen wird.
                bool imageChanged = localFoto.HasOverlay != cloudFoto.HasOverlay ||
                                    localFoto.File != cloudFoto.File ||
                                    cloudFoto.HasOverlay; // erneut uebermalt

                if (imageChanged && cloudFoto.IsLive())
                {
                    string file = string.IsNullOrEmpty(localFoto.File) ? fotoId : localFoto.File;
                    // Overlay entfernt -> lokales Original ist ab jetzt verwaist
                    _staleMedia.Enqueue((file, DropOriginal: !cloudFoto.HasOverlay));
                }

                localFoto.AllowExport = cloudFoto.AllowExport;
                localFoto.File = cloudFoto.File;
                localFoto.HasOverlay = cloudFoto.HasOverlay;
                localFoto.DateTime = cloudFoto.DateTime;
                localFoto.ImageSize = cloudFoto.ImageSize;
                localFoto.DeletedAt = cloudFoto.DeletedAt;
                localFoto.ModifiedAt = cloudFoto.ModifiedAt;
                localFoto.ModifiedBy = cloudFoto.ModifiedBy;
                _fotosChanged = true;
            }

            // Delete-Wins
            var merged = EarliestDeletion(localDeletedAt, cloudFoto.DeletedAt);
            if (localFoto.DeletedAt != merged)
                localFoto.DeletedAt = merged;

            if (wasLive != localFoto.IsLive())
                _fotosChanged = true;
        }
    }

    /// <summary>
    /// Veraltete Bild-/Thumbnail-Dateien loeschen und Fotolisten informieren.
    /// Ausserhalb des Merge-Locks aufrufen.
    /// </summary>
    private static void ApplyMergeSideEffects()
    {
        string? projectDir = CurrentProjectDir();

        if (projectDir != null && GlobalJson.Data != null)
        {
            var (img, thumb, _, _) = Folders(GlobalJson.Data);

            while (_staleMedia.TryDequeue(out var stale))
            {
                var paths = new List<string> { $"{img}/{stale.File}", $"{thumb}/{stale.File}" };
                if (stale.DropOriginal)
                    paths.Add($"{img}/originals/{stale.File}");

                foreach (var rel in paths)
                {
                    string full = ToLocalPath(projectDir, rel);
                    try
                    {
                        if (File.Exists(full)) File.Delete(full);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Veraltete Datei nicht geloescht ({rel}): {ex.Message}");
                    }
                }
            }
        }
        else
        {
            while (_staleMedia.TryDequeue(out _)) { }
        }

        if (_fotosChanged)
        {
            _fotosChanged = false;
            WeakReferenceMessenger.Default.Send(new RemoteDataChangedMessage(RemoteChangeType.FotosUpdated));
        }
    }

    // ===============================================================
    //  Tombstones -> Binaerdateien
    // ===============================================================

    private static (string Img, string Thumb, string Plan, string CustomPins) Folders(JsonDataModel d)
    {
        static string Or(string? v, string fallback)
            => string.IsNullOrWhiteSpace(v) ? fallback : v.Replace('\\', '/').Trim('/');

        return (Or(d.ImagePath, "images"), Or(d.ThumbnailPath, "thumbnails"),
                Or(d.PlanPath, "plans"), Or(d.CustomPinsPath, "custompins"));
    }

    /// <summary>
    /// Ermittelt relative Pfade aller Dateien lebender bzw. geloeschter Objekte.
    /// Ein Objekt ist tot, wenn es selbst ODER ein Elternobjekt eine Tombstone
    /// traegt. Dateien, die noch ein lebendes Objekt nutzt, sind nie "tot".
    /// </summary>
    private static (HashSet<string> Live, HashSet<string> Dead) CollectFileSets(JsonDataModel? data)
    {
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dead = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (data?.Plans == null) return (live, dead);

        var (img, thumb, planDir, customPins) = Folders(data);

        foreach (var plan in data.Plans.Values.ToList())
        {
            if (plan == null) continue;
            bool planLive = plan.IsLive();

            if (!string.IsNullOrEmpty(plan.File))
            {
                var target = planLive ? live : dead;
                target.Add($"{planDir}/{plan.File}");
                target.Add($"{planDir}/thumbnails/{plan.File}");
            }

            foreach (var pin in plan.Pins?.Values.ToList() ?? [])
            {
                if (pin == null) continue;
                bool pinLive = planLive && pin.IsLive();

                if (pin.IsCustomPin && !string.IsNullOrEmpty(pin.PinIcon))
                {
                    var target = pinLive ? live : dead;
                    target.Add($"{customPins}/{pin.PinIcon}");
                    target.Add($"{customPins}/{Path.ChangeExtension(pin.PinIcon, ".data")}");
                }

                foreach (var (key, foto) in pin.Fotos?.ToList() ?? [])
                {
                    if (foto == null) continue;
                    string file = string.IsNullOrEmpty(foto.File) ? key : foto.File;
                    if (string.IsNullOrEmpty(file)) continue;

                    if (pinLive && foto.IsLive())
                    {
                        live.Add($"{img}/{file}");
                        live.Add($"{thumb}/{file}");
                        if (foto.HasOverlay)
                            live.Add($"{img}/originals/{file}");
                    }
                    else
                    {
                        dead.Add($"{img}/{file}");
                        dead.Add($"{thumb}/{file}");
                        dead.Add($"{img}/originals/{file}");
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(data.TitleImage))
        {
            live.Add($"{img}/{data.TitleImage}");
            live.Add($"{thumb}/{data.TitleImage}");
        }

        dead.ExceptWith(live);
        return (live, dead);
    }

    private static string? CurrentProjectDir()
    {
        string path = GlobalJson.GetFilePath();
        return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
    }

    private static string ToLocalPath(string projectDir, string relativeCloudPath)
        => Path.Combine(projectDir, relativeCloudPath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Entfernt lokale Dateien geloeschter Objekte. Idempotent - raeumt auch
    /// Leichen auf, deren Tombstone von einem anderen Geraet kam.
    /// </summary>
    public static void PurgeLocalTombstoneFiles()
    {
        try
        {
            string? projectDir = CurrentProjectDir();
            if (string.IsNullOrEmpty(projectDir)) return;

            var (_, dead) = CollectFileSets(GlobalJson.Data);
            foreach (var rel in dead)
            {
                string full = ToLocalPath(projectDir, rel);
                if (!File.Exists(full)) continue;
                try
                {
                    File.Delete(full);
                    Console.WriteLine($"Lokale Dateileiche entfernt: {rel}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lokale Datei konnte nicht geloescht werden ({rel}): {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lokales Tombstone-Cleanup fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Plant Cloud-Loeschungen fuer alle Tombstone-Dateien. Nur aufrufen,
    /// wenn die Tombstones nachweislich in der Cloud-JSON stehen.
    /// </summary>
    private static void QueueCloudDeletesForTombstones()
    {
        try
        {
            var (_, dead) = CollectFileSets(GlobalJson.Data);
            SyncState.AddDeletes(dead);
            SyncState.RetainPurged(dead);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cloud-Tombstone-Cleanup konnte nicht geplant werden: {ex.Message}");
        }
    }

    // ===============================================================
    //  Cloud-Loeschungen
    // ===============================================================

    /// <summary>
    /// Merkt eine Cloud-Datei persistent zum Loeschen vor. Ausgefuehrt wird
    /// erst nach erfolgreichem JSON-Sync.
    /// </summary>
    public static void QueueCloudDelete(string relativeCloudPath)
    {
        if (string.IsNullOrEmpty(relativeCloudPath)) return;

        string path = GlobalJson.GetFilePath();
        if (!string.IsNullOrEmpty(path))
            SyncState.Bind(path);

        SyncState.AddDelete(relativeCloudPath);
        ScheduleSync();
    }

    /// <summary>
    /// Kompatibilitaet: bestehende Aufrufe loeschen nicht mehr sofort,
    /// sondern werden sicher eingereiht.
    /// </summary>
    public static Task DeleteCloudFileAsync(string relativeCloudPath)
    {
        QueueCloudDelete(relativeCloudPath);
        return Task.CompletedTask;
    }

    private static async Task ProcessPendingCloudDeletionsAsync()
    {
        var pending = SyncState.GetPendingDeletes();
        if (pending.Count == 0) return;
        if (CurrentAuth is not { IsLoggedIn: true }) return;

        string? projectDir = CurrentProjectDir();
        var (live, _) = CollectFileSets(GlobalJson.Data);

        foreach (var rel in pending)
        {
            // Sicherheitsnetz: Datei wird (wieder) verwendet -> nicht loeschen
            bool inUse = live.Contains(rel) ||
                         (projectDir != null && File.Exists(ToLocalPath(projectDir, rel)));
            if (inUse)
            {
                SyncState.DropDelete(rel);
                continue;
            }

            if (await DeleteCloudFileNowAsync(rel))
                SyncState.CompleteDelete(rel);
        }
    }

    /// <returns>true bei Erfolg oder wenn die Datei bereits fehlt (404).</returns>
    private static async Task<bool> DeleteCloudFileNowAsync(string relativeCloudPath)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)) return false;

        string cloudPath = SyncState.NormalizeCloudPath(relativeCloudPath);
        try
        {
            await CurrentAuth.GraphClient.Drives[GlobalJson.Data.CloudDriveId]
                .Items[GlobalJson.Data.CloudFolderId]
                .ItemWithPath(cloudPath)
                .DeleteAsync();

            Console.WriteLine($"Erfolgreich in der Cloud geloescht: {cloudPath}");
            return true;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return true; // war bereits weg
        }
        catch (Exception ex)
        {
            // 401/423/429/Netzwerk -> bleibt in der Queue
            Console.WriteLine($"Cloud-Loeschung fehlgeschlagen ({cloudPath}): {ex.Message}");
            return false;
        }
    }

    // ===============================================================
    //  Ordnerstruktur
    // ===============================================================

    private static async Task<string?> EnsureCloudFolderStructureAsync(AuthService authService, string driveId, string projectName)
    {
        try
        {
            var snapDocItem = await GetOrCreateFolderAsync(authService, driveId, "root", "SnapDoc");
            if (snapDocItem == null || string.IsNullOrEmpty(snapDocItem.Id)) return null;

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
    /// wenn er sicher nicht existiert.
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

                response = await authService.GraphClient.Drives[driveId].Items[parentId].Children
                    .WithUrl(response.OdataNextLink)
                    .GetAsync();
            }
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            Console.WriteLine($"Elternordner nicht gefunden: {parentId}");
            return null;
        }
        catch (Exception ex)
        {
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

            MarkLocalChange();
            await SaveWithSyncCheckAsync();

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

            MarkLocalChange();
            await SaveWithSyncCheckAsync();

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

    public static async Task LinkProjectToCloudAsync(string driveId, string folderId)
    {
        if (GlobalJson.Data == null) return;

        GlobalJson.Data.CloudDriveId = driveId;
        GlobalJson.Data.CloudFolderId = folderId;

        MarkLocalChange();
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

            // Dateien geloeschter Objekte gar nicht erst herunterladen
            if (remoteData != null)
            {
                var (_, dead) = CollectFileSets(remoteData);
                files = [.. files.Where(f => !dead.Contains(f.RelativePath))];
            }

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

        await WriteStreamAtomicAsync(contentStream, localFilePath);
    }

    /// <summary>
    /// Schreibt in eine .part-Datei und benennt dann um. Ein abgebrochener
    /// Download hinterlaesst so keine kaputte Datei.
    /// </summary>
    private static async Task WriteStreamAtomicAsync(Stream source, string destination)
    {
        string? dir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string tmp = destination + ".part";
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None,
                                                 bufferSize: 64 * 1024, useAsync: true))
            {
                await source.CopyToAsync(fs);
            }
            File.Move(tmp, destination, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* unkritisch */ }
            throw;
        }
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
            string name = Path.GetFileName(filePath);

            // JSON separat, Sync-Zustand und Teil-Downloads nie hochladen
            if (name.Equals(CloudFileName, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(SyncState.FileName, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                continue;

            files.Add((filePath, Path.GetRelativePath(rootDirectory, filePath)));
        }

        foreach (var directory in Directory.GetDirectories(currentDirectory))
            CollectLocalFiles(rootDirectory, directory, files);
    }

    /// <returns>true bei erfolgreichem Upload.</returns>
    public static async Task<bool> UploadSingleFileAsync(string localFilePath, string subFolder = "")
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId))
            return false;

        if (!File.Exists(localFilePath)) return false;

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
                        return false;
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
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Upload von {localFilePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Arbeitet die persistente Upload-Queue ab. Fehlgeschlagene Eintraege
    /// bleiben stehen, nicht mehr vorhandene Dateien fallen raus.
    /// </summary>
    private static async Task ProcessPendingUploadsAsync()
    {
        if (CurrentAuth is not { IsLoggedIn: true }) return;

        foreach (var (entry, fullPath) in SyncState.GetUploads())
        {
            if (!File.Exists(fullPath))
            {
                SyncState.RemoveUpload(entry);
                continue;
            }

            if (await UploadSingleFileAsync(fullPath, entry.SubFolder))
                SyncState.RemoveUpload(entry);
        }
    }

    // ===============================================================
    //  Cloud-Status und Polling
    // ===============================================================

    /// <summary>
    /// Merge + ggf. Upload + Nachladen fehlender Projektdateien.
    /// (Name aus Kompatibilitaetsgruenden beibehalten.)
    /// </summary>
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
            bool ok = await SyncCoreAsync();

            if (ok && GlobalJson.Data is { } d &&
                !string.IsNullOrEmpty(d.CloudDriveId) && !string.IsNullOrEmpty(d.CloudFolderId))
            {
                await DownloadMissingProjectFilesAsync(d.CloudDriveId, d.CloudFolderId);
            }

            return ok;
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

        _missingFilesPending = false;
        var project = ProjectItem.Current;

        foreach (var planPair in SyncOps.LivePlans(GlobalJson.Data).ToList())
        {
            var plan = planPair.Value;

            if (!string.IsNullOrEmpty(plan.File))
            {
                string localPath = Path.Combine(projectDir, project.PlanFolder, plan.File);
                if (!File.Exists(localPath))
                    await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.PlanFolder}/{plan.File}", localPath);
            }

            foreach (var pinPair in SyncOps.LivePins(plan).ToList())
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
                    string localIcon = Path.Combine(Settings.DataDirectory, "customicons", pin.PinIcon);
                    bool alreadyThere = File.Exists(localIcon) && IconLookup.Get(pin.PinIcon)?.FileName == pin.PinIcon;
                    if (alreadyThere) continue;

                    if (await EnsureCustomIconAvailableAsync(driveId, rootFolderId, pin.PinIcon))
                        WeakReferenceMessenger.Default.Send(new PinChangedMessage(pinPair.Key));
                    else
                        _missingFilesPending = true;
                }
            }
        }
    }

    private static async Task<bool> EnsureCustomIconAvailableAsync(string driveId, string rootFolderId, string iconFileName)
    {
        string iconDir = Path.Combine(Settings.DataDirectory, "customicons");
        string localPngPath = Path.Combine(iconDir, iconFileName);

        if (File.Exists(localPngPath) && IconLookup.Get(iconFileName)?.FileName == iconFileName)
            return true;

        var project = ProjectItem.Current;
        string metaFileName = Path.ChangeExtension(iconFileName, ".json");
        string localMetaPath = Path.Combine(iconDir, metaFileName);

        Directory.CreateDirectory(iconDir);

        if (!File.Exists(localPngPath))
            await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomIconsFolder}/{iconFileName}", localPngPath);

        await DownloadSpecificFileAsync(driveId, rootFolderId, $"{project.CustomIconsFolder}/{metaFileName}", localMetaPath);

        if (!File.Exists(localPngPath) || !File.Exists(localMetaPath)) return false;

        try
        {
            var meta = JsonSerializer.Deserialize<CustomIconMetadata>(await File.ReadAllTextAsync(localMetaPath));
            if (meta == null)
            {
                try { File.Delete(localMetaPath); } catch { /* unkritisch */ }
                return false;
            }

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

            Helper.UpdateIconItem(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), newIconItem);
            IconLookup.AddOrUpdate(newIconItem);
            Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out var iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            IconLookup.Initialize(Settings.IconData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Registrieren des CustomIcons '{iconFileName}': {ex.Message}");
            return false;
        }

        try { File.Delete(localMetaPath); } catch { /* unkritisch */ }
        return true;
    }

    private static async Task DownloadSpecificFileAsync(string driveId, string rootFolderId, string relativeCloudPath, string localDestinationPath)
    {
        if (CurrentAuth?.GraphClient == null) return;

        try
        {
            using var fileStream = await CurrentAuth.GraphClient.Drives[driveId].Items[rootFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream == null) return;

            await WriteStreamAtomicAsync(fileStream, localDestinationPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Nachladen der Datei {relativeCloudPath}: {ex.Message}");
        }
    }

    public static void StartCloudPolling(int intervalSeconds = 12)
    {
        StopCloudPolling();

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        if (!_connectivityHooked)
        {
            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
            _connectivityHooked = true;
        }

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

        if (_connectivityHooked)
        {
            Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
            _connectivityHooked = false;
        }
    }

    /// <summary>Wieder online -> offene Arbeit sofort nachholen.</summary>
    private static void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet && SyncState.HasPendingWork)
            ScheduleSync(1000);
    }

    private static async Task CheckETagAndSyncAsync()
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return;
        if (GlobalJson.Data == null ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudDriveId) ||
            string.IsNullOrEmpty(GlobalJson.Data.CloudFolderId)) return;

        try
        {
            // Offline angefallene Aenderungen, Uploads oder Loeschungen nachholen
            if (SyncState.HasPendingWork)
            {
                await SaveWithSyncCheckAsync();
                return;
            }

            var cloudItem = await GetCloudItemOrNullAsync(
                GlobalJson.Data.CloudDriveId, GlobalJson.Data.CloudFolderId, CloudFileName);

            if (cloudItem?.ETag == null) return;

            // _lastKnownETag wird hier NICHT gesetzt - das darf nur der Sync selbst
            if (cloudItem.ETag != _lastKnownETag)
            {
                await SyncJsonOnlyFromCloudAsync();
            }
            else if (_missingFilesPending)
            {
                await _saveGate.WaitAsync();
                try { await DownloadMissingProjectFilesAsync(GlobalJson.Data.CloudDriveId, GlobalJson.Data.CloudFolderId); }
                finally { _saveGate.Release(); }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Polling-Check fehlgeschlagen: {ex.Message}");
        }
    }

    // ===============================================================
    //  CustomIcons projektbezogen in die Cloud spiegeln
    // ===============================================================

    private static async Task EnsureCustomIconsInCloudAsync(string driveId, string rootFolderId)
    {
        if (CurrentAuth?.GraphClient == null || GlobalJson.Data == null) return;

        string iconFolder = ProjectItem.Current.CustomIconsFolder;

        var iconNames = SyncOps.LivePlans(GlobalJson.Data)
            .SelectMany(p => SyncOps.LivePins(p.Value))
            .Select(p => p.Value)
            .Where(p => p.IsCustomIcon && !string.IsNullOrEmpty(p.PinIcon))
            .Select(p => p.PinIcon)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => !_verifiedCloudIcons.ContainsKey(n))
            .ToList();

        foreach (var iconFileName in iconNames)
        {
            try
            {
                string metaFileName = Path.ChangeExtension(iconFileName, ".json");

                if (await CloudFileExistsAsync(driveId, rootFolderId, $"{iconFolder}/{iconFileName}") &&
                    await CloudFileExistsAsync(driveId, rootFolderId, $"{iconFolder}/{metaFileName}"))
                {
                    _verifiedCloudIcons.TryAdd(iconFileName, 0);
                    continue;
                }

                string localPng = Path.Combine(Settings.DataDirectory, "customicons", iconFileName);
                var item = IconLookup.Get(iconFileName);

                if (!File.Exists(localPng) || item == null) continue;

                await UploadFileAsync(driveId, rootFolderId, localPng, $"{iconFolder}/{iconFileName}");

                var meta = new CustomIconMetadata
                {
                    DisplayName = item.DisplayName,
                    AnchorX = item.AnchorPoint.X,
                    AnchorY = item.AnchorPoint.Y,
                    SizeWidth = item.IconSize.Width,
                    SizeHeight = item.IconSize.Height,
                    IsRotationLocked = item.IsRotationLocked,
                    IsAutoScaleLocked = item.IsAutoScaleLocked,
                    PinColorHex = item.PinColor.ToString(),
                    IconScale = item.IconScale,
                    Category = item.Category
                };

                string tempMeta = Path.Combine(FileSystem.CacheDirectory, metaFileName);
                await File.WriteAllTextAsync(tempMeta, JsonSerializer.Serialize(meta));
                await UploadFileAsync(driveId, rootFolderId, tempMeta, $"{iconFolder}/{metaFileName}");
                try { File.Delete(tempMeta); } catch { /* unkritisch */ }

                _verifiedCloudIcons.TryAdd(iconFileName, 0);
                Console.WriteLine($"CustomIcon in Projekt-Cloud gespiegelt: {iconFileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CustomIcon-Upload fehlgeschlagen ({iconFileName}): {ex.Message}");
            }
        }
    }

    private static async Task<bool> CloudFileExistsAsync(string driveId, string rootFolderId, string relativePath)
    {
        try
        {
            var item = await CurrentAuth!.GraphClient!.Drives[driveId].Items[rootFolderId]
                .ItemWithPath(relativePath)
                .GetAsync();
            return item?.File != null;
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return false;
        }
    }

    // ===============================================================
    //  Bedarfs-Download
    // ===============================================================

    public static async Task<bool> DownloadMediaOnDemandAsync(string fileName, string subFolder = "", string? driveId = null, string? folderId = null, string? projectDir = null)
    {
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

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

            if (File.Exists(localDestinationPath)) return true;

            using var fileStream = await CurrentAuth.GraphClient.Drives[effectiveDriveId].Items[effectiveFolderId]
                .ItemWithPath(relativeCloudPath)
                .Content
                .GetAsync();

            if (fileStream == null) return false;

            await WriteStreamAtomicAsync(fileStream, localDestinationPath);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bedarfs-Download fehlgeschlagen fuer {fileName} in '{subFolder}': {ex.Message}");
            return false;
        }
    }

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

    /// <summary>
    /// Setzt nur den Laufzeit-Zustand zurueck. Offene Uploads/Loeschungen
    /// bleiben in ".snapdoc_sync" erhalten und werden beim naechsten Oeffnen
    /// des Projekts nachgeholt.
    /// </summary>
    public static void ResetCloudSync()
    {
        TargetFolderId = null;
        _lastKnownETag = null;
        _lastKnownWriteTime = default;

        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }

        SyncState.Unbind();
        _verifiedCloudIcons.Clear();
        _missingFilesPending = false;
        while (_staleMedia.TryDequeue(out _)) { }
        _fotosChanged = false;
    }

    private static string SanitizeName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}

public class RemoteProjectDto
{
    public string FileName { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
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
