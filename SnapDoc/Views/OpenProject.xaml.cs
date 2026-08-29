#nullable disable
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
    private CancellationTokenSource _syncCancellationTokenSource;

    public OpenProject()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadJsonFiles();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Abbrechen, wenn die Seite verlassen wird
        CancelPendingCloudSync();
    }

    private void CancelPendingCloudSync()
    {
        if (_syncCancellationTokenSource != null)
        {
            _syncCancellationTokenSource.Cancel();
            _syncCancellationTokenSource.Dispose();
            _syncCancellationTokenSource = null;
        }
    }

    private async void LoadJsonFiles()
    {
        CancelPendingCloudSync();
        _syncCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _syncCancellationTokenSource.Token;

        string rootDirectory = Settings.DataDirectory;

        // 1. Lokale JSON-Dateien einlesen
        var foundFiles = await Task.Run(() =>
        {
            List<FileItem> items = [];

            try
            {
                var files = Directory.EnumerateFiles(rootDirectory, "*.json", SearchOption.AllDirectories);
                string activeFilePath = GlobalJson.Data?.JsonFile != null
                    ? Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile)
                    : null;

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string projectDir = Path.GetDirectoryName(file);
                    string thumbPath = "banner_thumbnail.png";

                    try
                    {
                        var projectData = GlobalJson.ReadFromFile(file);

                        if (projectData != null && !string.IsNullOrWhiteSpace(projectDir))
                        {
                            string titleImageName = !string.IsNullOrWhiteSpace(projectData.TitleImage)
                                    ? projectData.TitleImage
                                    : "banner_thumbnail.png";

                            string thumbnailFolder = !string.IsNullOrWhiteSpace(projectData.ThumbnailPath)
                                    ? projectData.ThumbnailPath
                                    : "thumbnails";

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
                        FileName = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        FileDate = File.GetLastWriteTime(file),
                        ImagePath = thumbPath,
                        ThumbnailPath = thumbPath,
                        IsActive = file == activeFilePath
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Abbruch ignorieren, leere/teilweise Liste wird unten abgefangen
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Einlesen der Projekte: {ex.Message}");
            }

            return items.OrderByDescending(f => f.FileDate).ToList();
        }, cancellationToken);

        if (cancellationToken.IsCancellationRequested) return;

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
                // Token an Netzwerk-Methode durchreichen
                var remoteProjects = await SaveManager.SearchRemoteProjectsAsync(cancellationToken);

                if (remoteProjects == null)
                    return;

                foreach (var item in foundFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var localData = GlobalJson.ReadFromFile(item.FilePath);
                        if (localData == null) continue;

                        string projectDir = Path.GetDirectoryName(item.FilePath);
                        if (string.IsNullOrWhiteSpace(projectDir)) continue;

                        RemoteProjectDto remoteProject = null;

                        if (!string.IsNullOrWhiteSpace(localData.CloudFolderId))
                            remoteProject = remoteProjects.FirstOrDefault(rp => rp.FolderId == localData.CloudFolderId);

                        if (remoteProject == null)
                        {
                            string expectedJsonName = item.FileName + ".json";
                            remoteProject = remoteProjects.FirstOrDefault(
                                rp => rp.FileName.Equals(expectedJsonName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (remoteProject == null)
                        {
                            if (!string.IsNullOrWhiteSpace(localData.CloudFolderId))
                            {
                                localData.CloudDriveId = null;
                                localData.CloudFolderId = null;

                                string json = System.Text.Json.JsonSerializer.Serialize(localData, GlobalJson.GetOptions());
                                File.WriteAllText(item.FilePath, json);
                            }

                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                    item.HasCloudSync = false;
                            });
                            continue;
                        }

                        bool cloudLinkChanged = localData.CloudDriveId != remoteProject.DriveId ||
                                                localData.CloudFolderId != remoteProject.FolderId;

                        if (cloudLinkChanged)
                        {
                            localData.CloudDriveId = remoteProject.DriveId;
                            localData.CloudFolderId = remoteProject.FolderId;

                            string json = System.Text.Json.JsonSerializer.Serialize(localData, GlobalJson.GetOptions());
                            File.WriteAllText(item.FilePath, json);
                        }

                        // Token an Netzwerk-Methode durchreichen
                        var remoteData = await SaveManager.GetRemoteProjectDataAsync(
                                remoteProject.DriveId,
                                remoteProject.FolderId,
                                remoteProject.FileName,
                                cancellationToken);

                        if (remoteData != null)
                        {
                            string localTitleImage = !string.IsNullOrWhiteSpace(localData.TitleImage) ? localData.TitleImage : "banner_thumbnail.png";
                            string remoteTitleImage = !string.IsNullOrWhiteSpace(remoteData.TitleImage) ? remoteData.TitleImage : "banner_thumbnail.png";

                            bool titleImageChanged = !localTitleImage.Equals(remoteTitleImage, StringComparison.OrdinalIgnoreCase);

                            if (titleImageChanged)
                            {
                                System.Diagnostics.Debug.WriteLine($"TitleImage geändert: {item.FileName}: {localTitleImage} -> {remoteTitleImage}");

                                // Token durchreichen
                                bool downloaded = await Helper.UpdateProjectTitleImageAsync(
                                        localData, projectDir, localTitleImage, remoteTitleImage, cancellationToken);

                                if (downloaded)
                                {
                                    localData.TitleImage = remoteTitleImage;
                                    localData.TitleImageSize = remoteData.TitleImageSize;
                                    string json = System.Text.Json.JsonSerializer.Serialize(localData, GlobalJson.GetOptions());
                                    File.WriteAllText(item.FilePath, json);
                                }
                            }
                            else
                            {
                                string thumbnailFolder = !string.IsNullOrWhiteSpace(localData.ThumbnailPath) ? localData.ThumbnailPath : "thumbnails";
                                string imageFolder = !string.IsNullOrWhiteSpace(localData.ImagePath) ? localData.ImagePath : "images";

                                string thumbPath = Path.Combine(projectDir, thumbnailFolder, remoteTitleImage);
                                string imagePath = Path.Combine(projectDir, imageFolder, remoteTitleImage);

                                // Token durchreichen
                                if (!File.Exists(thumbPath))
                                {
                                    await SaveManager.DownloadMediaOnDemandAsync(
                                        fileName: remoteTitleImage, subFolder: thumbnailFolder,
                                        driveId: remoteProject.DriveId, folderId: remoteProject.FolderId,
                                        projectDir: projectDir, cancellationToken);
                                }

                                if (!File.Exists(imagePath))
                                {
                                    await SaveManager.DownloadMediaOnDemandAsync(
                                        fileName: remoteTitleImage, subFolder: imageFolder,
                                        driveId: remoteProject.DriveId, folderId: remoteProject.FolderId,
                                        projectDir: projectDir, cancellationToken);
                                }
                            }

                            string finalThumbnailFolder = !string.IsNullOrWhiteSpace(localData.ThumbnailPath) ? localData.ThumbnailPath : "thumbnails";
                            string finalThumbPath = Path.Combine(projectDir, finalThumbnailFolder, remoteTitleImage);

                            if (File.Exists(finalThumbPath))
                            {
                                if (!string.Equals(item.ThumbnailPath, finalThumbPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    MainThread.BeginInvokeOnMainThread(async () =>
                                    {
                                        if (cancellationToken.IsCancellationRequested) return;
                                        item.ImagePath = null;
                                        item.ThumbnailPath = null;
                                        await Task.Delay(50, cancellationToken);
                                        item.ImagePath = finalThumbPath;
                                        item.ThumbnailPath = finalThumbPath;
                                    });
                                }
                            }
                        }

                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                                item.HasCloudSync = true;
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        // Wird von äusserem Block gefangen
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cloud-Abgleich für '{item.FileName}' fehlgeschlagen: {ex}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("Cloud-Sync wurde regulär abgebrochen.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud-Sync fehlgeschlagen: {ex}");
            }
        }, cancellationToken);
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(desc: AppResources.neues_projekt_eroeffnen, title: AppResources.plan_name, okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        string sanitizedName = OpenProject.SanitizeFileName(result.Result);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            await SnackbarExtensions.ShowSafeAsync(AppResources.invalid_project_name, includeDelay: true);
            return;
        }

        int counter = 1;
        string _result = sanitizedName;
        while (Directory.Exists(Path.Combine(Settings.DataDirectory, _result)))
        {
            _result = $"{sanitizedName} ({counter})";
            counter++;
        }

        string filePath = Path.Combine(Settings.DataDirectory, _result, _result + ".json");

        SaveManager.ResetCloudSync();
        LoadDataToView.ResetData();

        GlobalJson.CreateNewFile(filePath);
        GlobalJson.Data.Client_name = "";
        GlobalJson.Data.Object_address = "";
        GlobalJson.Data.Working_title = "";
        GlobalJson.Data.Project_nr = "";
        GlobalJson.Data.Object_name = "";
        GlobalJson.Data.Creation_date = DateTime.Now;
        GlobalJson.Data.Project_manager = "";
        GlobalJson.Data.ProjectPath = _result;
        GlobalJson.Data.JsonFile = _result + ".json";
        GlobalJson.Data.PlanPath = "plans";
        GlobalJson.Data.ImagePath = "images";
        GlobalJson.Data.ThumbnailPath = "thumbnails";
        GlobalJson.Data.CustomPinsPath = "custompins";
        GlobalJson.Data.TitleImage = "banner_thumbnail.png";

        SettingsService.Instance.IsProjectLoaded = true;
        GlobalJson.LoadFromFile(filePath);
        LoadDataToView.LoadData(new FileResult(filePath));
        Helper.HeaderUpdate();

        SaveManager.NotifyDataChanged();
        LoadJsonFiles();

        await Shell.Current.GoToAsync("project_details");
#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = false;
#endif
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        string cleanName = string.Concat(fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        cleanName = cleanName.Replace("/", "_").Replace("\\", "_").Replace("$", "").Replace("{", "").Replace("}", "");

        if (cleanName.Length > 100) cleanName = cleanName[..100];

        return cleanName;
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = AppResources.bitte_waehle_zip });
            if (fileResult == null) return;

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
                    if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
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
        if (_isProcessing) return;

        var layout = sender as BindableObject;
        if (layout?.BindingContext is not FileItem item || item.IsActive)
            return;

        _isProcessing = true;
        CancelPendingCloudSync();
        await BusyService.ShowAsync(AppResources.projekt_wird_geladen);

        try
        {
            await Task.Delay(150);

            SaveManager.ResetCloudSync();

            if (FileListView.ItemsSource is IEnumerable<FileItem> items)
            {
                foreach (var f in items) f.IsActive = false;
                item.IsActive = true;
            }

            SettingsService.Instance.IsProjectLoaded = true;
            LoadDataToView.ResetData();

            GlobalJson.LoadFromFile(item.FilePath);
            SaveManager.Initialize(item.FilePath);

            if (await SaveManager.IsCloudVersionNewerAsync())
            {
                bool shouldSync = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var popup = new PopupDualResponse(AppResources.neuere_version_cloud_synchronisieren, AppResources.synchronisieren);
                    var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
                    return result?.Result == DualPopupResult.Ok;
                });

                if (shouldSync)
                {
                    await BusyService.SetMessageAsync(AppResources.daten_werden_synchronisiert);
                    bool success = await SaveManager.SyncJsonOnlyFromCloudAsync();
                    if (success) GlobalJson.LoadFromFile(item.FilePath);
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
                        foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins) i++;
                    }

                    if (GlobalJson.Data.Plans[plan.Key].PinCount != i)
                    {
                        GlobalJson.Data.Plans[plan.Key].PinCount = i;
                        repairCount = true;
                    }
                }

                if (repairCount) SaveManager.NotifyDataChanged();
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

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var button = sender as Button;
            if (button?.BindingContext is not FileItem item) return;

            var _popup = new PopupProjectEdit(entry: item.FileName, isActive: item.IsActive);
            var _result = await this.ShowPopupAsync<string>(_popup, Settings.PopupOptions);
            if (_result == null || string.IsNullOrEmpty(_result.Result)) return;

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
                        string currentActiveJson = GlobalJson.Data?.JsonFile;
                        bool isCurrentProject = !string.IsNullOrEmpty(fileName) &&
                                                 fileName.Equals(currentActiveJson, StringComparison.OrdinalIgnoreCase);

                        if (!string.IsNullOrEmpty(projectDirectoryPath) && Directory.Exists(projectDirectoryPath))
                            Directory.Delete(projectDirectoryPath, true);

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
                                    try { Directory.Delete(dir, true); }
                                    catch (IOException) { }
                                    catch (UnauthorizedAccessException) { }
                                }
                            }
                        }

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
                            await BusyService.ShowAsync(AppResources.daten_werden_komprimiert);
                            await Task.Run(() => { Helper.PackDirectory(sourceDirectory, outputPath); });
                        }
                        finally
                        {
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

                    if (item.HasCloudSync) return;

                    if (!item.IsActive)
                    {
                        if (FileListView.ItemsSource is IEnumerable<FileItem> items)
                        {
                            foreach (var f in items) f.IsActive = false;
                        }

                        item.IsActive = true;
                        SettingsService.Instance.IsProjectLoaded = true;

                        LoadDataToView.ResetData();
                        GlobalJson.LoadFromFile(item.FilePath);
                        LoadDataToView.LoadData(new FileResult(item.FilePath));
                        Helper.HeaderUpdate();
                    }

                    await Shell.Current.GoToAsync("cloudPickerPage?mode=SelectFolder");
                    break;

                case null:
                    break;

                default:
                    if (Directory.Exists(Path.GetDirectoryName(item.FilePath)))
                    {
                        var newFilePath = Path.Combine(Settings.DataDirectory, _result.Result, _result.Result + ".json");
                        var oldFilePath = item.FilePath;

                        GlobalJson.LoadFromFile(oldFilePath);
                        GlobalJson.Data.ProjectPath = _result.Result;
                        GlobalJson.Data.JsonFile = _result.Result + ".json";
                        GlobalJson.Data.PlanPath = "plans";
                        GlobalJson.Data.ImagePath = "images";
                        GlobalJson.Data.ThumbnailPath = "thumbnails";
                        GlobalJson.Data.CustomPinsPath = "custompins";

                        SaveManager.NotifyDataChanged();
                        Directory.Move(Path.GetDirectoryName(oldFilePath), Path.GetDirectoryName(newFilePath));

                        File.Move(Path.Combine(Path.GetDirectoryName(newFilePath), item.FileName + ".json"),
                                  Path.Combine(Path.GetDirectoryName(newFilePath), _result.Result + ".json"));

                        GlobalJson.UpdateFilePath(newFilePath);

                        if (item.FileName == Path.GetFileName(Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile)))
                        {
                            LoadDataToView.ResetData();
                            GlobalJson.LoadFromFile(newFilePath);
                            LoadDataToView.LoadData(new FileResult(newFilePath));
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