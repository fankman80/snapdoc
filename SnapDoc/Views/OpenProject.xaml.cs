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
                    string projectDir = Path.GetDirectoryName(file);
                    string thumbImg = Directory.EnumerateFiles(projectDir, "title_*.jpg").FirstOrDefault()
                                     ?? "banner_thumbnail.png";

                    items.Add(new FileItem
                    {
                        FileName = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        FileDate = File.GetLastWriteTime(file),
                        ImagePath = thumbImg,
                        ThumbnailPath = thumbImg,
                        IsActive = file == activeFilePath
                    });
                }
            }
            catch { /* Fehlerbehandlung */ }
            return items.OrderByDescending(f => f.FileDate).ToList();
        });

        FileListView.ItemsSource = foundFiles;
        ProjectCounterLabel.Text = $"{foundFiles.Count} {AppResources.projekte}";

        // Hintergrundpruefung fuer lokale Projekte
        if (SaveManager.CurrentAuth?.IsLoggedIn == true)
        {
            _ = Task.Run(async () =>
            {
                // Hole einmalig alle Cloud-Projekte (schneller als Einzelabfragen)
                var remoteProjects = await SaveManager.SearchRemoteProjectsAsync();

                foreach (var item in foundFiles)
                {
                    // Lokales JSON auslesen, um an die Cloud-IDs zu kommen
                    var projectData = GlobalJson.ReadFromFile(item.FilePath);
                    if (projectData == null) continue;

                    bool hasValidCloudLink = false;

                    // FALL A: Das Projekt hat bereits eine Cloud-Verknuepfung (ID) gespeichert
                    if (!string.IsNullOrEmpty(projectData.CloudFolderId))
                    {
                        // Pruefen, ob exakt dieser Ordner online noch existiert
                        var matchingById = remoteProjects.FirstOrDefault(rp => rp.FolderId == projectData.CloudFolderId);

                        if (matchingById != null)
                        {
                            hasValidCloudLink = true;
                        }
                        else
                        {
                            // Die Cloud-IDs lokal entfernen, da das Projekt online geloescht wurde
                            projectData.CloudDriveId = null;
                            projectData.CloudFolderId = null;

                            string updatedJson = System.Text.Json.JsonSerializer.Serialize(projectData, GlobalJson.GetOptions());
                            File.WriteAllText(item.FilePath, updatedJson);

                            item.HasCloudSync = false;
                        }
                    }
                    // FALL B: Legacy-Fallback fuer Projekte, die noch keine IDs haben (Namensabgleich beim ersten Mal)
                    else
                    {
                        string expectedJsonName = item.FileName + ".json";
                        var matchingByName = remoteProjects.FirstOrDefault(rp =>
                            rp.FileName.Equals(expectedJsonName, StringComparison.OrdinalIgnoreCase));

                        if (matchingByName != null)
                        {
                            hasValidCloudLink = true;

                            // 3. Ergaenze fehlende Cloud-IDs lokal, damit ab sofort Fall A greift
                            projectData.CloudDriveId = matchingByName.DriveId;
                            projectData.CloudFolderId = matchingByName.FolderId;

                            string updatedJson = System.Text.Json.JsonSerializer.Serialize(projectData, GlobalJson.GetOptions());
                            File.WriteAllText(item.FilePath, updatedJson);
                        }
                    }
                    // Automatische Aktualisierung der XAML
                    item.HasCloudSync = hasValidCloudLink;
                }
            });
        }
    }

    private async void OnDownloadFromCloudClicked(object sender, EventArgs e)
    {
        if (SaveManager.CurrentAuth == null ||
            !SaveManager.CurrentAuth.IsLoggedIn)
        {
            await DisplayAlertAsync(
                AppResources.info,
                AppResources.bitte_zuerst_anmelden,
                AppResources.ok);

            return;
        }

        try
        {
            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.projekte_werden_gesucht);

            // 1. Projekte aus der Cloud suchen
            var remoteProjects =
                await SaveManager.SearchRemoteProjectsAsync();

            await BusyService.HideAsync();

            // 2. Prüfen, ob Projekte vorhanden sind
            if (remoteProjects.Count == 0)
            {
                await DisplayAlertAsync(
                    AppResources.info,
                    AppResources.keine_projekte_in_cloud_gefunden,
                    AppResources.ok);

                return;
            }

            // 3. Projekt auswählen lassen
            var popup = new PopupCloudProjects(remoteProjects);

            var result =
                await this.ShowPopupAsync<RemoteProjectDto>(
                    popup,
                    Settings.PopupOptions);

            if (result?.Result == null)
                return;

            // 4. Gewähltes Projekt herunterladen
            var selectedProject = result.Result;

            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.projekt_wird_heruntergeladen);

            bool success =
                await SaveManager.DownloadRemoteProjectAsync(
                    selectedProject);

            // 5. Ergebnis verarbeiten
            if (success)
            {
                LoadJsonFiles();

                await BusyService.HideAsync();

                await DisplayAlertAsync(
                    AppResources.info,
                    AppResources.projekt_erfolgreich_heruntergeladen,
                    AppResources.ok);
            }
            else
            {
                await BusyService.HideAsync();

                await DisplayAlertAsync(
                    AppResources.fehler,
                    AppResources.fehler_beim_herunterladen_des_projekts,
                    AppResources.ok);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Fehler beim Cloud-Download: {ex}");

            await DisplayAlertAsync(
                AppResources.fehler,
                ex.Message,
                AppResources.ok);
        }
        finally
        {
            await BusyService.HideAsync();
        }
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

        string filePath = Path.Combine(Settings.DataDirectory, _result, _result + ".json");

        // Cloud-Verknüpfung im SaveManager zurücksetzen
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

            if (fileResult == null) return;

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

            if (await SaveManager.IsCloudVersionNewerAsync())
            {
                bool shouldSync = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var popup = new PopupDualResponse(
                        "In der Cloud existiert eine neuere Version dieses Projekts. Möchten Sie die neuesten Daten jetzt synchronisieren?",
                        "Synchronisieren");

                    var result = await this.ShowPopupAsync<string>(
                        popup,
                        Settings.PopupOptions);

                    return result?.Result == "Ok";
                });

                if (shouldSync)
                {
                    await BusyService.SetMessageAsync("Daten werden synchronisiert...");

                    bool success =
                        await SaveManager.SyncJsonOnlyFromCloudAsync();

                    if (success)
                        GlobalJson.LoadFromFile(item.FilePath);
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
            System.Diagnostics.Debug.WriteLine(
                $"Cloud Sync oder Lade-Fehler: {ex}");

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlertAsync(
                    "Fehler beim Laden",
                    "Das Projekt konnte aufgrund eines Problems nicht geladen werden.",
                    "OK");
            });
        }
        finally
        {
            await BusyService.HideAsync();
            _isProcessing = false;
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            var button = sender as Button;
            if (button?.BindingContext is not FileItem item) return;

            var _popup = new PopupProjectEdit(entry: item.FileName);
            var _result = await this.ShowPopupAsync<string>(_popup, Settings.PopupOptions);
            if (_result == null || string.IsNullOrEmpty(_result.Result)) return;

            switch (_result.Result)
            {
                case "Delete":
                    await Task.Delay(200);

                    var popup1 = new PopupDualResponse(AppResources.wollen_sie_dieses_projekt_wirklich_loeschen, okText: AppResources.loeschen, alert: true);
                    var result1 = await this.ShowPopupAsync<string>(popup1, Settings.PopupOptions);

                    if (result1.Result == "Ok")
                    {
                        string fullPath = item.FilePath;
                        if (string.IsNullOrEmpty(fullPath)) return;

                        string projectDirectoryPath = Path.GetDirectoryName(fullPath);
                        string fileName = Path.GetFileName(fullPath);
                        string currentActiveJson = GlobalJson.Data?.JsonFile;
                        bool isCurrentProject = !string.IsNullOrEmpty(fileName) &&
                                                 fileName.Equals(currentActiveJson, StringComparison.OrdinalIgnoreCase);

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
                    var result2 = await this.ShowPopupAsync<string>(popup2, Settings.PopupOptions);

                    if (result2.Result == "Ok")
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
                    if (SaveManager.CurrentAuth == null ||
                        !SaveManager.CurrentAuth.IsLoggedIn)
                    {
                        await DisplayAlertAsync(
                            AppResources.info,
                            AppResources.bitte_zuerst_anmelden,
                            AppResources.ok);

                        return;
                    }

                    if (item.HasCloudSync)
                        return;

                    if (!item.IsActive)
                    {
                        if (FileListView.ItemsSource is IEnumerable<FileItem> items)
                        {
                            foreach (var f in items)
                                f.IsActive = false;
                        }

                        item.IsActive = true;

                        SettingsService.Instance.IsProjectLoaded = true;

                        LoadDataToView.ResetData();
                        GlobalJson.LoadFromFile(item.FilePath);
                        LoadDataToView.LoadData(new FileResult(item.FilePath));
                        Helper.HeaderUpdate();
                    }

                    await BusyService.HideAsync();
                    await Navigation.PushAsync(new CloudPickerPage(CloudPickerMode.SelectFolder));

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

                        // Save data to file
                        SaveManager.NotifyDataChanged();

                        // Verzeichnis an die neue Stelle verschieben (umbenennen)
                        Directory.Move(Path.GetDirectoryName(oldFilePath), Path.GetDirectoryName(newFilePath));

                        // Json verschieben (umbenennen)
                        File.Move(Path.Combine(Path.GetDirectoryName(newFilePath), item.FileName + ".json"),
                                  Path.Combine(Path.GetDirectoryName(newFilePath), _result.Result + ".json"));

                        GlobalJson.UpdateFilePath(newFilePath);

                        if (item.FileName == Path.GetFileName(Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile)))
                        {
                            // Daten laden und verarbeiten (nicht UI-bezogen)
                            LoadDataToView.ResetData();
                            GlobalJson.LoadFromFile(newFilePath);
                            LoadDataToView.LoadData(new FileResult(newFilePath));
                            Helper.HeaderUpdate();  // UI-Aktualisierung
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
