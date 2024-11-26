#nullable disable

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
        fileListView.Footer = foundFiles.Count + " Projekte";
    }

    private async void OnProjectClicked(object sender, EventArgs e)
    {
        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;

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
                    LoadDataToView.ResetApp();

                    Helper.AddMenuItem("Bericht exportieren", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Download, "OnExportClicked");
                    Helper.AddMenuItem("Bericht teilen", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Share, "OnShareClicked");
                    Helper.AddMenuItem("Einstellungen", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Settings, "OnSettingsClicked");
                    Helper.AddDivider();

                    GlobalJson.LoadFromFile(item.FilePath);
                    LoadDataToView.LoadData(new FileResult(item.FilePath));
                    HeaderUpdate();  // UI-Aktualisierung

                    await Shell.Current.GoToAsync("//project_details");
                });
            }
        });

        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {

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
                string[] directories = Directory.GetDirectories(Path.GetDirectoryName(item.FilePath), "*", SearchOption.AllDirectories);
                foreach (var directory in directories)
                    Directory.Delete(directory, true);

                // Root-Verzeichnis löschen
                Directory.Delete(Path.GetDirectoryName(item.FilePath));
            }
        }
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