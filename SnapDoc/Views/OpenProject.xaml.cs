﻿#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Controls;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

#if WINDOWS
using System.Diagnostics;
#endif

namespace SnapDoc.Views;

public partial class OpenProject : ContentPage
{
    private bool _isProcessing = false;

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
        string rootDirectory = Settings.DataDirectory;

        // 1. Lokale JSON-Dateien einlesen
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
                                SettingsService.Instance.DefaultJson)
                            : null;
                foreach (var file in files)
                {
                    string currentFilePath = file;
                    string projectDir = Path.GetDirectoryName(currentFilePath);
                    string thumbPath = "banner_thumbnail.png";
                    string projectName = Path.GetFileNameWithoutExtension(currentFilePath); // Fallback

                    try
                    {
                        // 1. Datei einlesen, um zu pruefen, ob es wirklich ein Projekt ist
                        var projectData = GlobalJson.ReadFromFile(currentFilePath);

                        if (projectData != null && !string.IsNullOrWhiteSpace(projectDir))
                        {
                            // 2. Umbenennungs-Logik: Pruefen ob der Name vom Standard abweicht
                            string currentFileName = Path.GetFileName(currentFilePath);

                            if (!currentFileName.Equals(SettingsService.Instance.DefaultJson, StringComparison.OrdinalIgnoreCase))
                            {
                                string newFilePath = Path.Combine(projectDir, SettingsService.Instance.DefaultJson);

                                // Nur umbenennen, falls am Ziel nicht schon eine Datei liegt
                                if (!File.Exists(newFilePath))
                                {
                                    File.Move(currentFilePath, newFilePath);
                                    currentFilePath = newFilePath; // Ab hier den neuen Pfad nutzen!
                                }
                            }

                            // Object_name aus der JSON als Anzeigename nutzen
                            if (!string.IsNullOrWhiteSpace(projectData.Object_name))
                            {
                                projectName = projectData.Object_name;
                            }
                            else
                            {
                                projectName = Path.GetFileName(projectDir); // Zweiter Fallback
                            }

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
                        IsActive = currentFilePath == activeFilePath
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
        });

        // 2. CollectionView sofort anzeigen
        FileListView.ItemsSource = foundFiles;

        ProjectCounterLabel.Text = $"{foundFiles.Count} {AppResources.projekte}";

        // 3. Cloud-Abgleich im Hintergrund
        _ = Task.Run(async () =>
        {
            if (SaveManager.CurrentAuth?.IsLoggedIn != true)
                return;

            try
            {
                var remoteProjects =
                    await SaveManager.SearchRemoteProjectsAsync();

                if (remoteProjects == null)
                    return;

                foreach (var item in foundFiles)
                {
                    try
                    {
                        var localData = GlobalJson.ReadFromFile(item.FilePath);

                        if (localData == null)
                            continue;

                        string projectDir = Path.GetDirectoryName(item.FilePath);

                        if (string.IsNullOrWhiteSpace(projectDir))
                            continue;

                        // 3.1 Passendes Cloud-Projekt suchen
                        RemoteProjectDto remoteProject = null;

                        if (!string.IsNullOrWhiteSpace(localData.CloudFolderId))
                            remoteProject = remoteProjects.FirstOrDefault(rp => rp.FolderId == localData.CloudFolderId);

                        // 3.2 Keine Cloud-Verknuepfung ueber die globale Index-Suche gefunden
                        if (remoteProject == null)
                        {
                            bool existsInCloud = false;

                            // Direktpruefung per ID: Schuetzt vor Index-Verzoegerungen bei neuen Projekten
                            if (!string.IsNullOrWhiteSpace(localData.CloudDriveId) && !string.IsNullOrWhiteSpace(localData.CloudFolderId))
                            {
                                try
                                {
                                    var folderCheck = await SaveManager.CurrentAuth.GraphClient
                                        .Drives[localData.CloudDriveId]
                                        .Items[localData.CloudFolderId]
                                        .GetAsync();

                                    if (folderCheck != null)
                                    {
                                        existsInCloud = true;
                                        remoteProject = new RemoteProjectDto
                                        {
                                            DriveId = localData.CloudDriveId,
                                            FolderId = localData.CloudFolderId,
                                            FileName = "snapdoc_data.json"
                                        };
                                    }
                                }
                                catch
                                {
                                    // Ordner existiert in der Cloud nicht mehr (404 / NotFound)
                                    existsInCloud = false;
                                }
                            }

                            if (!existsInCloud)
                            {
                                // Projekt existiert wirklich nicht mehr in der Cloud -> Verwaiste IDs lokal loeschen
                                if (!string.IsNullOrEmpty(localData.CloudDriveId) || !string.IsNullOrEmpty(localData.CloudFolderId))
                                {
                                    localData.CloudDriveId = null;
                                    localData.CloudFolderId = null;

                                    string updatedJson = System.Text.Json.JsonSerializer.Serialize(
                                        localData,
                                        GlobalJson.GetOptions());

                                    File.WriteAllText(item.FilePath, updatedJson);
                                }

                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    item.HasCloudSync = false;
                                });

                                continue;
                            }
                        }

                        // 3.3 Cloud-Verknüpfung aktualisieren
                        bool cloudLinkChanged = localData.CloudDriveId != remoteProject.DriveId ||
                                                localData.CloudFolderId != remoteProject.FolderId;

                        if (cloudLinkChanged)
                        {
                            localData.CloudDriveId = remoteProject.DriveId;
                            localData.CloudFolderId = remoteProject.FolderId;

                            string json = System.Text.Json.JsonSerializer.Serialize(
                                          localData,
                                          GlobalJson.GetOptions());

                            File.WriteAllText(item.FilePath, json);
                        }

                        // 3.4 Cloud-JSON lesen
                        var remoteData = await SaveManager.GetRemoteProjectDataAsync(
                                         remoteProject.DriveId,
                                         remoteProject.FolderId,
                                         remoteProject.FileName);

                        if (remoteData != null)
                        {
                            string localTitleImage = !string.IsNullOrWhiteSpace(localData.TitleImage)
                                    ? localData.TitleImage
                                    : "banner_thumbnail.png";

                            string remoteTitleImage = !string.IsNullOrWhiteSpace(remoteData.TitleImage)
                                    ? remoteData.TitleImage
                                    : "banner_thumbnail.png";

                            // 3.5 Titelbild geändert?
                            bool titleImageChanged = !localTitleImage.Equals(
                                    remoteTitleImage,
                                    StringComparison.OrdinalIgnoreCase);

                            if (titleImageChanged)
                            {
                                System.Diagnostics.Debug.WriteLine($"TitleImage geändert: " + $"{item.FileName}: " + $"{localTitleImage} -> {remoteTitleImage}");

                                bool downloaded = await Helper.UpdateProjectTitleImageAsync(
                                        localData,
                                        projectDir,
                                        localTitleImage,
                                        remoteTitleImage);

                                if (downloaded)
                                {
                                    // Lokale JSON auf den Cloud-Stand bringen
                                    localData.TitleImage = remoteTitleImage;
                                    localData.TitleImageSize = remoteData.TitleImageSize;

                                    string json = System.Text.Json.JsonSerializer.Serialize(
                                            localData,
                                            GlobalJson.GetOptions());

                                    File.WriteAllText(item.FilePath, json);
                                }
                            }
                            else
                            {
                                // 3.6 Name gleich, aber Dateien fehlen?
                                string thumbnailFolder = !string.IsNullOrWhiteSpace(
                                        localData.ThumbnailPath)
                                        ? localData.ThumbnailPath
                                        : "thumbnails";

                                string imageFolder = !string.IsNullOrWhiteSpace(
                                        localData.ImagePath)
                                        ? localData.ImagePath
                                        : "images";

                                string thumbPath = Path.Combine(
                                        projectDir,
                                        thumbnailFolder,
                                        remoteTitleImage);

                                string imagePath = Path.Combine(
                                        projectDir,
                                        imageFolder,
                                        remoteTitleImage);

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

                            // 3.7 CollectionView aktualisieren (nur bei tatsächlicher Änderung)
                            string finalThumbnailFolder = !string.IsNullOrWhiteSpace(
                                    localData.ThumbnailPath)
                                    ? localData.ThumbnailPath
                                    : "thumbnails";

                            string finalThumbPath = Path.Combine(projectDir, finalThumbnailFolder, remoteTitleImage);

                            if (File.Exists(finalThumbPath))
                            {
                                // Prüfen, ob sich der Pfad überhaupt geändert hat, um unnötiges Neuladen (Blinken) zu verhindern
                                if (!string.Equals(item.ThumbnailPath, finalThumbPath, StringComparison.OrdinalIgnoreCase))
                                {
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
                        }

                        // 3.8 Cloud-Sync-Status aktualisieren
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            item.HasCloudSync = true;
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cloud-Abgleich für '{item.FileName}' fehlgeschlagen: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud-Sync fehlgeschlagen: {ex}");
            }
        });
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(desc: AppResources.neues_projekt_eroeffnen,
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        // Eingabe säubern
        string sanitizedName = OpenProject.SanitizeFileName(result.Result);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            await SnackbarExtensions.ShowSafeAsync(AppResources.invalid_project_name, includeDelay: true);
            return;
        }

        // Prüfe, ob die Datei existiert und hänge fortlaufend eine Nummer an
        int counter = 1;
        string _result = sanitizedName;
        while (Directory.Exists(Path.Combine(Settings.DataDirectory, _result)))
        {
            _result = $"{sanitizedName} ({counter})";
            counter++;
        }

        string filePath = Path.Combine(Settings.DataDirectory, _result, SettingsService.Instance.DefaultJson);

        SaveManager.ResetCloudSync();
        LoadDataToView.ResetData();

        GlobalJson.CreateNewFile(filePath);
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
        GlobalJson.LoadFromFile(filePath);
        LoadDataToView.LoadData(new FileResult(filePath));
        Helper.HeaderUpdate();  // UI-Aktualisierung

        // save data to file
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
        // Sperre prüfen: Wenn bereits ein Projekt geladen wird, Klick ignorieren!
        if (_isProcessing)
            return;

        var layout = sender as BindableObject;
        if (layout?.BindingContext is not FileItem item)
            return;

        // Wenn das Projekt bereits aktiv ist, nichts tun.
        if (item.IsActive)
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

            GlobalJson.LoadFromFile(item.FilePath);
            SaveManager.Initialize(item.FilePath);

            if (await SaveManager.IsCloudVersionNewerAsync())
            {
                // Ladebildschirm pausieren, damit das Popup bedient werden kann
                await BusyService.HideAsync();

                bool shouldSync = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var popup = new PopupDualResponse(AppResources.neuere_version_cloud_synchronisieren, AppResources.synchronisieren);
                    var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);

                    return result?.Result == DualPopupResult.Ok;
                });

                if (shouldSync)
                {
                    // Ladebildschirm für den Sync wieder aktivieren
                    await BusyService.ShowAsync(AppResources.daten_werden_synchronisiert);

                    bool success = await SaveManager.SyncJsonOnlyFromCloudAsync();

                    if (success)
                        GlobalJson.LoadFromFile(item.FilePath);
                }
                else
                {
                    // Ladebildschirm für den restlichen lokalen Ladevorgang wiederherstellen
                    await BusyService.ShowAsync(AppResources.projekt_wird_geladen);
                }
            }


            LoadDataToView.LoadData(new FileResult(item.FilePath));
            Helper.HeaderUpdate();

            if (GlobalJson.Data.Plans != null)
            {
                var repairCount = false;

                foreach (var plan in GlobalJson.Data.Plans)
                {
                    var i = 0;
                    if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                    {
                        foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            i++;
                    }

                    if (GlobalJson.Data.Plans[plan.Key].PinCount != i)
                    {
                        GlobalJson.Data.Plans[plan.Key].PinCount = i;
                        repairCount = true;
                    }
                }

                if (repairCount)
                    SaveManager.NotifyDataChanged();
            }

            // Overlay vor dem Shell-Seitenwechsel schließen.
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

            var _popup = new PopupProjectEdit(entry: item.FileName, isActive: item.IsActive);
            var _result = await this.ShowPopupAsync<string>(_popup, Settings.PopupOptions);
            if (_result == null || string.IsNullOrEmpty(_result.Result))
                return;

            switch (_result.Result)
            {
                case "Delete":
                    await Task.Delay(200);

                    var popup1 = new PopupDualResponse(AppResources.wollen_sie_dieses_projekt_wirklich_loeschen, okText: AppResources.loeschen, alert: true);
                    var result1 = await this.ShowPopupAsync<DualPopupResult>(popup1, Settings.PopupOptions);

                    if (result1.Result is DualPopupResult.Ok)
                    {
                        string fullPath = item.FilePath;
                        if (string.IsNullOrEmpty(fullPath)) return;

                        string projectDirectoryPath = Path.GetDirectoryName(fullPath);
                        string fileName = Path.GetFileName(fullPath);
                        bool isCurrentProject = !string.IsNullOrEmpty(fileName) &&
                                                 fileName.Equals(SettingsService.Instance.DefaultJson, StringComparison.OrdinalIgnoreCase);

                        // Lösche das Projektverzeichnis und alle enthaltenen Dateien
                        if (!string.IsNullOrEmpty(projectDirectoryPath) && Directory.Exists(projectDirectoryPath))
                            Directory.Delete(projectDirectoryPath, true);

                        // Lösche Plan-Tiles aus dem Cache-Ordner
                        string cacheDir = Path.Combine(FileSystem.AppDataDirectory, "Tiles");
                        if (Directory.Exists(cacheDir) && GlobalJson.Data?.Plans != null)
                        {
                            foreach (var plan in GlobalJson.Data.Plans)
                            {
                                string baseFileName = Path.GetFileNameWithoutExtension(GlobalJson.Data.Plans[plan.Key].File).Replace("_r", "");
                                string searchPattern = $"*{baseFileName}*";
                                var matchingDirectories = Directory.GetDirectories(cacheDir, searchPattern);

                                foreach (var dir in matchingDirectories)
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

                        // Wenn das gelöschte Projekt das aktuell geladene Projekt ist, zurück zum Homescreen navigieren und Daten zurücksetzen
                        if (isCurrentProject)
                        {
                            await Shell.Current.GoToAsync("//homescreen");
                            SettingsService.Instance.IsProjectLoaded = false;
                            LoadDataToView.ResetData();
                            Helper.HeaderUpdate();
                        }

                        LoadJsonFiles();
                    }
                    break;

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
                        return;

                    if (FileListView.ItemsSource is IEnumerable<FileItem> items)
                    {
                        foreach (var f in items)
                            f.IsActive = false;
                    }

                    item.IsActive = true;
                    SettingsService.Instance.IsProjectLoaded = true;

                    LoadDataToView.ResetData();
                    GlobalJson.LoadFromFile(item.FilePath);
                    SaveManager.Initialize(item.FilePath);
                    LoadDataToView.LoadData(new FileResult(item.FilePath));
                    Helper.HeaderUpdate();

                    await Shell.Current.GoToAsync("cloudPickerPage?mode=SelectFolder");
                    break;

                case null:
                    break;

                default:
                    var currentFilePath = item.FilePath;
                    if (File.Exists(currentFilePath))
                    {
                        GlobalJson.LoadFromFile(currentFilePath);
                        GlobalJson.Data.Object_name = _result.Result;
                        GlobalJson.SaveToFile();

                        SaveManager.NotifyDataChanged();

                        // Wenn das umbenannte Projekt gerade aktiv ist, Header aktualisieren
                        if (item.IsActive)
                        {
                            LoadDataToView.ResetData();
                            GlobalJson.LoadFromFile(currentFilePath);
                            LoadDataToView.LoadData(new FileResult(currentFilePath));
                            Helper.HeaderUpdate();
                        }

                        LoadJsonFiles();
                    }
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
