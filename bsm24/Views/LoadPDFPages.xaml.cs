#nullable disable

using System.Globalization;
using UraniumUI.Pages;


namespace bsm24.Views;
public partial class LoadPDFPages : UraniumContentPage
{
    public LoadPDFPages()
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


}
