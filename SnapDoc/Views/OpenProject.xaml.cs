#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Services;
using SnapDoc.Resources.Languages;

#if WINDOWS
using System.Diagnostics;
#endif

namespace SnapDoc.Views;
public partial class OpenProject : ContentPage
{
    public OpenProject()
    {
        InitializeComponent();

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
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(desc: AppResources.neues_projekt_eroeffnen,
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
            // Eingabe säubern
            string sanitizedName = OpenProject.SanitizeFileName(result.Result);
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                await DisplayAlertAsync(AppResources.fehler, AppResources.invalid_project_name, AppResources.ok);
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
            GlobalJson.SaveToFile();

            LoadJsonFiles();

            await Shell.Current.GoToAsync("project_details");
#if ANDROID || IOS
            Shell.Current.FlyoutIsPresented = false;
#endif
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var invalidChars = Path.GetInvalidFileNameChars();
        string cleanName = string.Concat(fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        cleanName = cleanName.Replace("/", "_").Replace("\\", "_").Replace("$", "").Replace("{", "").Replace("}", "");

        if (cleanName.Length > 100)
            cleanName = cleanName.Substring(0, 100);

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

            if (fileResult != null)
            {
                // Zeige Busy-Overlay
                var busyPopup = new MyBusyPage(AppResources.projekt_wird_importiert);
                await Mopups.Services.MopupService.Instance.PushAsync(busyPopup);

                var targetDirectory = Settings.DataDirectory;

                // Hintergrundoperation
                await Task.Run(() =>
                {
                    Helper.UnpackDirectory(fileResult.FullPath, targetDirectory);
                });

                LoadJsonFiles();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Auswählen der Datei: {ex.Message}");
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync(AppResources.fehler, AppResources.datei_konnte_nicht_importiert_werden, AppResources.ok);
            else
                await Toast.Make(AppResources.datei_konnte_nicht_importiert_werden).Show();
        }
        finally
        {
            // Busy-Overlay entfernen
            if (Mopups.Services.MopupService.Instance.PopupStack.Any())
                await Mopups.Services.MopupService.Instance.PopAsync();
        }
    }

    private async void OnProjectClicked(object sender, TappedEventArgs e)
    {
        var layout = sender as BindableObject;
        if (layout?.BindingContext is not FileItem item)
            return;

        if (item == null)
            return;

        // Zeige Busy-Overlay
        var busyPopup = new MyBusyPage(AppResources.projekt_wird_geladen);
        await Mopups.Services.MopupService.Instance.PushAsync(busyPopup);

        await Task.Delay(100);

        try
        {
            if (item.IsActive)
            {
                // Busy-Overlay entfernen
                await Mopups.Services.MopupService.Instance.PopAsync();
                return;
            }

            // Aktives Projekt setzen
            if (FileListView.ItemsSource is IEnumerable<FileItem> items)
            {
                foreach (var f in items)
                    f.IsActive = false;

                item.IsActive = true;
            }

            SettingsService.Instance.IsProjectLoaded = true;
            LoadDataToView.ResetData();

            // Laden kann im UI-Thread bleiben
            GlobalJson.LoadFromFile(item.FilePath);
            LoadDataToView.LoadData(new FileResult(item.FilePath));
            Helper.HeaderUpdate();

            // Repair-Check
            if (GlobalJson.Data.Plans != null)
            {
                var repairCount = false;
                foreach (var plan in GlobalJson.Data.Plans)
                {
                    var i = 0;
                    if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                        foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            i++;

                    if (GlobalJson.Data.Plans[plan.Key].PinCount != i)
                    {
                        GlobalJson.Data.Plans[plan.Key].PinCount = i;
                        repairCount = true;
                    }
                }
                if (repairCount)
                    GlobalJson.SaveToFile();
            }

            await Shell.Current.GoToAsync("project_details");

#if ANDROID || IOS
            Shell.Current.FlyoutIsPresented = false;
#endif
        }
        finally
        {
            // Busy-Overlay entfernen
            if (Mopups.Services.MopupService.Instance.PopupStack.Any())
                await Mopups.Services.MopupService.Instance.PopAsync();
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        FileItem item = (FileItem)button.BindingContext;

        var _popup = new PopupProjectEdit(entry: item.FileName);
        var _result = await this.ShowPopupAsync<string>(_popup, Settings.PopupOptions);

        switch (_result.Result)
        {
            case "Delete":
                var popup1 = new PopupDualResponse(AppResources.wollen_sie_dieses_projekt_wirklich_loeschen, okText: AppResources.loeschen, alert: true);
                var result1 = await this.ShowPopupAsync<string>(popup1, Settings.PopupOptions);

                if (result1.Result == "Ok")
                {
                    string fullPath = item?.FilePath;
                    if (string.IsNullOrEmpty(fullPath))
                        return;

                    string projectDirectoryPath = Path.GetDirectoryName(fullPath);
                    string fileName = Path.GetFileName(fullPath);
                    string currentActiveJson = GlobalJson.Data.JsonFile;
                    bool isCurrentProject = !string.IsNullOrEmpty(fileName) &&
                                             fileName.Equals(currentActiveJson, StringComparison.OrdinalIgnoreCase);

                    var tmp_list = (List<FileItem>)FileListView.ItemsSource;
                    tmp_list.Remove(item);
                    FileListView.ItemsSource = null;
                    FileListView.ItemsSource = tmp_list;
                    ProjectCounterLabel.Text = $"{tmp_list.Count} {AppResources.projekte}";

                    // Wenn das gelöschte Projekt das aktuell geladene Projekt ist, zurück zum Homescreen navigieren und Daten zurücksetzen
                    if (isCurrentProject)
                    {
                        await Shell.Current.GoToAsync("//homescreen");
                        SettingsService.Instance.IsProjectLoaded = false;
                        LoadDataToView.ResetData();
                        Helper.HeaderUpdate();
                    }

                    if (!string.IsNullOrEmpty(projectDirectoryPath) && Directory.Exists(projectDirectoryPath))
                        Directory.Delete(projectDirectoryPath, true);

                    LoadJsonFiles();
                }
                break;

            case "Zip":
                var popup2 = new PopupDualResponse(AppResources.wollen_sie_projekt_als_zip_exportieren);
                var result2 = await this.ShowPopupAsync<string>(popup2, Settings.PopupOptions);
                if (result2.Result == "Ok")
                {
                    string sourceDirectory = Path.GetDirectoryName(item.FilePath);
                    string outputPath = Path.Combine(Settings.DataDirectory, Path.GetFileNameWithoutExtension(item.FileName) + ".zip");

                    // Zeige Busy-Overlay
                    var busyPopup = new MyBusyPage(AppResources.daten_werden_komprimiert);
                    await Mopups.Services.MopupService.Instance.PushAsync(busyPopup);

                    // Hintergrundoperation (nicht UI-Operationen)
                    await Task.Run(() => { Helper.PackDirectory(sourceDirectory, outputPath); });

                    // Busy-Overlay entfernen
                    await Mopups.Services.MopupService.Instance.PopAsync();

                    var saveStream = File.Open(outputPath, FileMode.Open);
                    try
                    {
                        var fileSaveResult = await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(item.FileName) + ".zip", saveStream);
                        if (DeviceInfo.Platform == DevicePlatform.WinUI)
                            await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.zip_wurde_exportiert, AppResources.ok);
                        else
                            await Toast.Make(AppResources.zip_wurde_exportiert).Show();
                    }
                    finally
                    {
                        saveStream.Close();  // Schließt den Stream sicher
                    }

                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                break;

            case "Folder":
                var directoryPath = Path.GetDirectoryName((Path.Combine(Settings.DataDirectory,item.FilePath)));
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
                    GlobalJson.SaveToFile();

                    // Verzeichnis an die neue Stelle verschieben (umbenennen)
                    Directory.Move(Path.GetDirectoryName(oldFilePath), Path.GetDirectoryName(newFilePath));

                    // Json verschieben (umbenennen)
                    Directory.Move(Path.Combine(Path.GetDirectoryName(newFilePath), item.FileName + ".json"),
                                    Path.Combine(Path.GetDirectoryName(newFilePath), _result.Result + ".json"));

                    GlobalJson.UpdateFilePath(newFilePath);

                    if (item.FileName == Path.GetFileName(Path.Combine(GlobalJson.Data.ProjectPath,GlobalJson.Data.JsonFile)))
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
}
