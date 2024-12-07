#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using ICSharpCode.SharpZipLib.Zip;
using Mopups.Services;
using System.Globalization;
using UraniumUI.Pages;


namespace bsm24.Views;
public partial class OpenProject : UraniumContentPage
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
        string rootDirectory = FileSystem.AppDataDirectory;

        // Liste zum Speichern der gefundenen Dateien
        List<FileItem> foundFiles = [];

        string searchPattern = "*.json"; // Alle JSON-Dateien suchen

        // Alle Unterverzeichnisse und das Hauptverzeichnis durchsuchen
        try
        {
            // Rekursive Suche in allen Unterverzeichnissen
            string[] files = Directory.GetFiles(rootDirectory, searchPattern, SearchOption.AllDirectories);

            // Gefundene Dateien zur Liste hinzufügen
            foreach (var file in files)
            {
                string thumbImg = "banner_thumbnail.png";

                if (File.Exists(Path.Combine(Path.GetDirectoryName(file), "title_thumbnail.jpg")))
                    thumbImg = Path.Combine(Path.GetDirectoryName(file), "title_thumbnail.jpg");

                foundFiles.Add(new FileItem
                {
                    FileName = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    FileDate = "Geändert am:\n" + File.GetLastWriteTime(file).Date.ToString("d", new CultureInfo("de-DE")),
                    ImagePath = thumbImg
                });
            }
        }
        catch (Exception ex)
        {
            // Fehlerbehandlung, z.B. falls keine Zugriffsrechte auf bestimmte Verzeichnisse vorhanden sind
            Console.WriteLine("Fehler beim Durchsuchen der Verzeichnisse: " + ex.Message);
        }

        // Liste der JSON-Dateien dem ListView zuweisen        
        fileListView.ItemsSource = foundFiles;
        fileListView.Footer = foundFiles.Count + " Projekt(e)";
    }

    private async void OnProjectClicked(object sender, EventArgs e)
    {
        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Projekt wird geladen...";
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(() =>
        {
            var button = sender as Button;
            if (button.BindingContext is FileItem item)
            {
                // Alle UI-Änderungen im Haupt-Thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // Daten laden und verarbeiten (nicht UI-bezogen)
                    LoadDataToView.ResetFlyoutItems();
                    LoadDataToView.ResetData();

                    GlobalJson.LoadFromFile(item.FilePath);
                    LoadDataToView.LoadData(new FileResult(item.FilePath));
                    HeaderUpdate();  // UI-Aktualisierung

                    await Shell.Current.GoToAsync("project_details");
#if ANDROID
                    Shell.Current.FlyoutIsPresented = false;
#endif
                });
            }
        });
        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Projekt wirklich als Zip exportieren?");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
            var button = sender as Button;
            if (button.BindingContext is FileItem item)
            {
                string sourceDirectory = Path.GetDirectoryName(item.FilePath); // Pfad zum zu zippenden Ordner
                string outputPath = Path.Combine(FileSystem.AppDataDirectory, Path.GetFileNameWithoutExtension(item.FileName) + ".zip");


                busyOverlay.IsVisible = true;
                activityIndicator.IsRunning = true;
                busyText.Text = "Daten werden komprimiert...";

                // Hintergrundoperation (nicht UI-Operationen)
                await Task.Run(() => { ZipDirectory(sourceDirectory, outputPath); });

                activityIndicator.IsRunning = false;
                busyOverlay.IsVisible = false;


                CancellationToken cancellationToken = new();
                var saveStream = File.Open(outputPath, FileMode.Open);
                var fileSaveResult = await FileSaver.Default.SaveAsync(Path.GetFileNameWithoutExtension(item.FileName) + ".zip", saveStream, cancellationToken);
                if (fileSaveResult.IsSuccessful)
                    await Toast.Make($"Zip wurde exportiert").Show(cancellationToken);
                else
                    await Toast.Make($"Zip wurde nicht exportiert").Show(cancellationToken);
                saveStream.Close();
                File.Delete(outputPath);
            }
        }
    }

    public static void ZipDirectory(string sourceDirectory, string zipFilePath)
    {
        try
        {
            using var fsOut = File.Create(zipFilePath);
            using var zipOutputStream = new ZipOutputStream(fsOut);
            zipOutputStream.SetLevel(9); // Set compression level (0-9)

            // Use recursive method to compress folder
            OpenProject.CompressFolder(sourceDirectory, zipOutputStream, sourceDirectory.Length + 1);

            zipOutputStream.Finish();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error zipping directory: {ex.Message}");
        }
    }

    private static void CompressFolder(string path, ZipOutputStream zipStream, int folderOffset)
    {
        var files = Directory.GetFiles(path);

        foreach (var filename in files)
        {
            var fileInfo = new FileInfo(filename);

            // Calculate the relative path within the zip file
            string entryName = filename[folderOffset..];
            entryName = ZipEntry.CleanName(entryName); // Clean the entry name

            var newEntry = new ZipEntry(entryName)
            {
                DateTime = fileInfo.LastWriteTime, // Use the file's last write time
                Size = fileInfo.Length
            };

            zipStream.PutNextEntry(newEntry);

            // Write the file to the zip stream
            using (var fileStream = File.OpenRead(filename))
            {
                fileStream.CopyTo(zipStream);
            }

            zipStream.CloseEntry();
        }

        // Get directories within this folder and recurse
        var folders = Directory.GetDirectories(path);
        foreach (var folder in folders)
        {
            OpenProject.CompressFolder(folder, zipStream, folderOffset);
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie dieses Projekt wirklich löschen?");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
            var button = sender as Button;
            if (button.BindingContext is FileItem item)
            {
                List<FileItem> tmp_list = (List<FileItem>)fileListView.ItemsSource;
                tmp_list.Remove(item);
                fileListView.ItemsSource = null;
                fileListView.ItemsSource = tmp_list;
                fileListView.Footer = tmp_list.Count + " Projekte";

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
            }
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var popup= new PopupEntry(GlobalJson.Data.Plans[PlanId].Name);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
    }

    private static void HeaderUpdate()
    {
        // aktualisiere den Header Text
        Services.SettingsService.Instance.FlyoutHeaderTitle = GlobalJson.Data.Object_name;
        Services.SettingsService.Instance.FlyoutHeaderDesc = GlobalJson.Data.Client_name;

        // aktualisiere das Thumbnail Bild
        if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg")))
            Services.SettingsService.Instance.FlyoutHeaderImage = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg");
        else
            Services.SettingsService.Instance.FlyoutHeaderImage = "banner_thumbnail.png";
    }
}
