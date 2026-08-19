using Microsoft.Graph;
using SnapDoc.Models;
using System.Text.Json;

namespace SnapDoc.Services;

public static class SaveManager
{
    public static AuthService? CurrentAuth { get; set; }
    private static CancellationTokenSource? _debounceCts;
    private static DateTime _lastKnownWriteTime;
    private static readonly Lock _fileLock = new();
    private static string CloudFileName => GlobalJson.Data?.JsonFile ?? "snapdoc_data.json";
    private static DateTimeOffset _lastKnownCloudSyncTime = DateTimeOffset.MinValue;
    private static string? _lastKnownETag;

    public static void Initialize(string filePath)
    {
        GlobalJson.LoadFromFile(filePath);
        if (File.Exists(filePath))
        {
            _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
        }
    }

    public static void NotifyDataChanged(int delayMilliseconds = 2000)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Delay(delayMilliseconds, token).ContinueWith(async task =>
        {
            if (!task.IsCanceled)
            {
                await SaveWithSyncCheckAsync();
            }
        }, TaskScheduler.Default);
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
            try
            {
                var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
                if (myDrive != null && !string.IsNullOrEmpty(myDrive.Id))
                {
                    string projectName = GlobalJson.Data?.ProjectPath ?? "DefaultProject";
                    string? targetFolderId = await EnsureCloudFolderStructureAsync(CurrentAuth, myDrive.Id, projectName);

                    if (!string.IsNullOrEmpty(targetFolderId))
                    {
                        await EnsureProjectSubfoldersAsync(CurrentAuth, myDrive.Id, targetFolderId);

                        try
                        {
                            var cloudItem = await CurrentAuth.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                                .ItemWithPath(CloudFileName)
                                .GetAsync();

                            if (cloudItem?.LastModifiedDateTime != null && cloudItem.LastModifiedDateTime > _lastKnownCloudSyncTime)
                            {
                                // Jemand anderes hat die Datei verändert! Herunterladen und mergen.
                                var cloudStream = await CurrentAuth.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
                                    .ItemWithPath(CloudFileName)
                                    .Content
                                    .GetAsync();

                                if (cloudStream != null)
                                {
                                    var cloudData = await JsonSerializer.DeserializeAsync<JsonDataModel>(cloudStream, GlobalJson.GetOptions());
                                    if (cloudData != null)
                                    {
                                        if (GlobalJson.Data == null)
                                            GlobalJson.Data = cloudData;
                                        else
                                            MergeModels(GlobalJson.Data, cloudData);

                                        GlobalJson.SaveToFile(); // Lokal speichern mit den neuen Daten
                                        _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
                                    }
                                }
                            }
                        }
                        catch (Microsoft.Graph.Models.ODataErrors.ODataError)
                        {
                            // Datei existiert noch nicht in der Cloud (404 Not Found), das ist ok für den ersten Upload.
                        }

                        string json = GlobalJson.ToJson();
                        byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(json);
                        using var stream = new MemoryStream(byteArray);

                        try
                        {
                            // Upload mit ETag-Sicherung
                            var uploadedItem = await CurrentAuth.GraphClient.Drives[myDrive.Id].Items[targetFolderId]
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
                                _lastKnownETag = uploadedItem.ETag; // Neuen ETag nach unserem erfolgreichen Upload merken
                            }
                        }
                        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 412 || ex.Error?.Code == "conditionNotMet")
                        {
                            Console.WriteLine("Konflikt beim Upload! ETag stimmt nicht mehr überein.");
                            await SaveWithSyncCheckAsync();
                            return; // Aktuellen (fehlgeschlagenen) Durchlauf abbrechen
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud-Upload fehlgeschlagen: {ex.Message}");
            }
        }
    }

    public static async Task LoadDataAsync(AuthService authService, string localFilePath)
    {
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
            {
                MergeModels(GlobalJson.Data, externalData);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Mergen: {ex.Message}");
        }
    }

    private static void MergeModels(JsonDataModel local, JsonDataModel external)
    {
        // Hier definierst du, wie Eigenschaften zusammengeführt werden.
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
            "thumbnails"
            ];

        foreach (var subfolder in subfolders)
        {
            var parts = subfolder.Split('/');
            string currentParentId = projectFolderId;

            foreach (var part in parts)
            {
                var folderItem = await GetOrCreateFolderAsync(authService, driveId, currentParentId, part);
                if (folderItem != null && !string.IsNullOrEmpty(folderItem.Id))
                {
                    currentParentId = folderItem.Id;
                }
                else
                {
                    break; // Abbruch bei diesem Pfad, falls etwas schiefging
                }
            }
        }
    }
}


