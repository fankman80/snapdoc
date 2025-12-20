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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadJsonFiles();
    }

    private void LoadJsonFiles()
    {
        // Hauptverzeichnis, in dem die Suche beginnen soll (z.B. das App-Datenverzeichnis)
        string rootDirectory = Settings.DataDirectory;
        List<FileItem> foundFiles = [];
        string searchPattern = "*.json"; // Alle JSON-Dateien suchen

        // Prüfen welche Datei schon geladen ist
        string activeFilePath = GlobalJson.Data?.JsonFile != null
                                ? Path.Combine(Settings.DataDirectory,
                                               GlobalJson.Data.ProjectPath,
                                               GlobalJson.Data.JsonFile)
                                : null;

        // Alle Unterverzeichnisse und das Hauptverzeichnis durchsuchen
        try
        {
            // Rekursive Suche in allen Unterverzeichnissen
            string[] files = Directory.GetFiles(rootDirectory, searchPattern, SearchOption.AllDirectories);

            // Gefundene Dateien zur Liste hinzufügen
            foreach (var file in files)
            {
                string[] _thumbImg = Directory.GetFiles(Path.GetDirectoryName(file), "title_*.jpg", SearchOption.AllDirectories);
                string thumbImg = _thumbImg.FirstOrDefault();

                thumbImg ??= "banner_thumbnail.png";

                foundFiles.Add(new FileItem
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
        catch (Exception ex)
        {
            // Fehlerbehandlung, z.B. falls keine Zugriffsrechte auf bestimmte Verzeichnisse vorhanden sind
            Console.WriteLine("Fehler beim Durchsuchen der Verzeichnisse: " + ex.Message);
        }

        // Liste der JSON-Dateien dem ListView zuweisen        
        FileListView.ItemsSource = foundFiles;
        ProjectCounterLabel.Text = "Projekte: " + foundFiles.Count ;

        // nach Datum sortieren
        FileListView.ItemsSource = foundFiles.OrderByDescending(f => f.FileDate).ToList();
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(title: "Neues Projekt eröffnen...", okText: "Erstellen");
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
            // Prüfe, ob die Datei existiert und hänge fortlaufend eine Nummer an
            int counter = 1;
            string _result = result.Result;
            while (Directory.Exists(Path.Combine(Settings.DataDirectory, _result)))
            {
                _result = Path.Combine($"{result.Result} ({counter})");
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

            await Shell.Current.GoToAsync("project_details");
#if ANDROID
            Shell.Current.FlyoutIsPresented = false;
#endif
        }
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        // 1. BusyIndicator SOFORT einschalten
        // Damit ist er sichtbar, sobald der Picker sich schließt und der Download läuft
        busyOverlay.BusyMessage = "Warte auf Datei...";
        busyOverlay.IsActivityRunning = true;
        busyOverlay.IsOverlayVisible = true;

        try
        {
            // 2. FilePicker öffnen
            // Der Code "wartet" hier, während der User wählt UND während das OS herunterlädt
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Bitte wähle eine Zip-Datei aus"
            });

            if (fileResult != null)
            {
                // Optional: Nachricht aktualisieren, dass der Download fertig ist und nun verarbeitet wird
                busyOverlay.BusyMessage = "Projekt wird importiert...";

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
                await Application.Current.Windows[0].Page.DisplayAlertAsync("Fehler", "Die Datei konnte nicht importiert werden.", "OK");
            else
                await Toast.Make($"Die Datei konnte nicht importiert werden.").Show();
        }
        finally
        {
            // 3. Aufräumen (Wird IMMER ausgeführt, auch bei Fehler oder Abbrechen)
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
        }
    }

    private async void OnProjectClicked(object sender, EventArgs e)
    {
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "Projekt wird geladen...";

        try
        {
            var button = sender as Button;
            if (button?.BindingContext is not FileItem item)
                return;

            if (item.IsActive)
            {
                busyOverlay.IsActivityRunning = false;
                busyOverlay.IsOverlayVisible = false;
                return;
            }

            // ⭐ Aktives Projekt setzen
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

            // Repair-Check (unverändert)
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

#if ANDROID
            Shell.Current.FlyoutIsPresented = false;
#endif
        }
        finally
        {
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
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
                    List<FileItem> tmp_list = (List<FileItem>)FileListView.ItemsSource;
                    tmp_list.Remove(item);
                    FileListView.ItemsSource = null;
                    FileListView.ItemsSource = tmp_list;
                    FileListView.Footer = tmp_list.Count + " " + AppResources.projekte;

                    // Rekursives Löschen von Dateien in allen Unterverzeichnissen
                    string[] files = Directory.GetFiles(Path.GetDirectoryName(item.FilePath), "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                        File.Delete(file);

                    // Rekursives Löschen von Verzeichnissen
                    string[] directories = Directory.GetDirectories(Path.GetDirectoryName(item.FilePath), "*", SearchOption.TopDirectoryOnly);
                    foreach (var directory in directories)
                        Directory.Delete(directory, true);

                    // Root-Verzeichnis löschen
                    Directory.Delete(Path.GetDirectoryName(item.FilePath));

                    LoadJsonFiles();
                }
                break;

            case "Zip":
                var popup2 = new PopupDualResponse("Wollen Sie dieses Projekt wirklich als Zip exportieren?");
                var result2 = await this.ShowPopupAsync<string>(popup2, Settings.PopupOptions);
                if (result2.Result == "Ok")
                {
                    string sourceDirectory = Path.GetDirectoryName(item.FilePath);
                    string outputPath = Path.Combine(Settings.DataDirectory, Path.GetFileNameWithoutExtension(item.FileName) + ".zip");

                    busyOverlay.IsOverlayVisible = true;
                    busyOverlay.IsActivityRunning = true;
                    busyOverlay.BusyMessage = "Daten werden komprimiert...";

                    // Hintergrundoperation (nicht UI-Operationen)
                    await Task.Run(() => { Helper.PackDirectory(sourceDirectory, outputPath); });

                    busyOverlay.IsActivityRunning = false;
                    busyOverlay.IsOverlayVisible = false;

                    var saveStream = File.Open(outputPath, FileMode.Open);
                    try
                    {
                        var fileSaveResult = await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(item.FileName) + ".zip", saveStream);
                        if (DeviceInfo.Platform == DevicePlatform.WinUI)
                            await Application.Current.Windows[0].Page.DisplayAlertAsync("", "Zip wurde exportiert.", "OK");
                        else
                            await Toast.Make($"Zip wurde exportiert.").Show();
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
