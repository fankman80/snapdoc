using Microsoft.Graph;
using SnapDoc.Models;
using System.Text.Json;

namespace SnapDoc.Services;

public static class SaveManager
{
    public static AuthService? CurrentAuth { get; set; }
    public static string? TargetFolderId { get; set; }
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

    // Synchronisiert die aktuelle Datei mit einem bestehenden Ordner in der Cloud.
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

            // NEU: Lokales Projektverzeichnis ebenfalls rekursiv hochladen
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
                                MergeModels(GlobalJson.Data, cloudData);
                                GlobalJson.SaveToFile();
                                _lastKnownWriteTime = File.GetLastWriteTimeUtc(filePath);
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
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return results;

        try
        {
            var myDrive = await CurrentAuth.GraphClient.Me.Drive.GetAsync();
            if (myDrive?.Id == null) return results;

            // Sucht nach allen *.json Dateien im Laufwerk
            var searchResponse = await CurrentAuth.GraphClient.Drives[myDrive.Id]
                .SearchWithQ(".json")
                .GetAsSearchWithQGetResponseAsync();

            if (searchResponse?.Value == null) return results;

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
        if (CurrentAuth?.GraphClient == null || !CurrentAuth.IsLoggedIn) return false;

        try
        {
            string projectName = Path.GetFileNameWithoutExtension(remoteProject.FileName);
            string localProjectDir = Path.Combine(Settings.DataDirectory, projectName);

            // Lokale Ordnerstruktur vorbereiten
            Directory.CreateDirectory(localProjectDir);
            Directory.CreateDirectory(Path.Combine(localProjectDir, "images"));
            Directory.CreateDirectory(Path.Combine(localProjectDir, "images", "originals"));
            Directory.CreateDirectory(Path.Combine(localProjectDir, "plans"));
            Directory.CreateDirectory(Path.Combine(localProjectDir, "plans", "thumbnails"));
            Directory.CreateDirectory(Path.Combine(localProjectDir, "thumbnails"));

            // Rekursive Funktion zum Herunterladen aller Cloud-Inhalte
            async static Task DownloadFolderRecursiveAsync(string driveId, string cloudFolderId, string localDir)
            {
                var children = await CurrentAuth.GraphClient.Drives[driveId].Items[cloudFolderId].Children.GetAsync();
                if (children?.Value == null) return;

                foreach (var item in children.Value)
                {
                    if (item.Folder != null)
                    {
                        string subLocalDir = Path.Combine(localDir, item.Name!);
                        Directory.CreateDirectory(subLocalDir);
                        await DownloadFolderRecursiveAsync(driveId, item.Id!, subLocalDir);
                    }
                    else if (item.File != null)
                    {
                        string localFilePath = Path.Combine(localDir, item.Name!);
                        using var contentStream = await CurrentAuth.GraphClient.Drives[driveId].Items[item.Id!].Content.GetAsync();
                        if (contentStream != null)
                        {
                            using var fileStream = File.Create(localFilePath);
                            await contentStream.CopyToAsync(fileStream);
                        }
                    }
                }
            }

            // Starte den Download des gesamten Cloud-Projektordners
            await DownloadFolderRecursiveAsync(remoteProject.DriveId, remoteProject.FolderId, localProjectDir);

            // JSON-Verknüpfung aktualisieren
            string localJsonPath = Path.Combine(localProjectDir, remoteProject.FileName);
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

    private static async Task UploadDirectoryRecursiveAsync(string driveId, string rootFolderId, string localDirPath)
    {
        if (CurrentAuth?.GraphClient == null || !Directory.Exists(localDirPath)) return;

        // 1. Alle Dateien im aktuellen lokalen Verzeichnis hochladen
        foreach (var filePath in Directory.GetFiles(localDirPath))
        {
            string fileName = Path.GetFileName(filePath);
            // Die JSON selbst wird separat über SaveWithSyncCheckAsync gehandhabt
            if (fileName.Equals(CloudFileName, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await CurrentAuth.GraphClient.Drives[driveId].Items[rootFolderId]
                    .ItemWithPath(fileName)
                    .Content
                    .PutAsync(fileStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Hochladen der Datei {fileName}: {ex.Message}");
            }
        }

        // 2. Alle Unterordner durchgehen und rekursiv verarbeiten
        foreach (var subDirPath in Directory.GetDirectories(localDirPath))
        {
            string subFolderName = Path.GetFileName(subDirPath);

            // Entsprechenden Unterordner in der Cloud finden oder erstellen
            var cloudSubFolder = await GetOrCreateFolderAsync(CurrentAuth, driveId, rootFolderId, subFolderName);
            if (cloudSubFolder?.Id != null)
            {
                await UploadDirectoryRecursiveAsync(driveId, cloudSubFolder.Id, subDirPath);
            }
        }
    }

    public static void ResetCloudSync()
    {
        TargetFolderId = null;
    }
}

public class RemoteProjectDto
{
    public string FileName { get; set; } = string.Empty;
    public string DriveId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public DateTimeOffset LastModified { get; set; }
}