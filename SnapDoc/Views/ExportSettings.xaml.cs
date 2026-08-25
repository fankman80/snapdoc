#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Controls;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class ExportSettings : ContentPage
{
    private static readonly string[] iOSFileTypes = ["com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document"];
    private static readonly string[] AndroidFileTypes = ["application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];
    private static readonly string[] WinUIFileTypes = [".doc", ".docx"];

    public ExportSettings()
    {
        InitializeComponent();
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadDocuments();

        if (String.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate) && SettingsService.Instance.Templates.Count > 0)
            SettingsService.Instance.SelectedTemplate = SettingsService.Instance.Templates.First();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SettingsService.Instance.SaveSettings();
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupAlert(
                AppResources.exportvorlage_waehlen_oder_importieren);

            await this.ShowPopupAsync<string>(
                popup,
                Settings.PopupOptions);

            return;
        }

        string outputPath = Path.Combine(
            Settings.DataDirectory,
            GlobalJson.Data.ProjectPath,
            GlobalJson.Data.ProjectPath + ".docx");

        string templatePath = Path.Combine(
            Settings.DataDirectory,
            "templates",
            SettingsService.Instance.SelectedTemplate);

        try
        {
            // Bericht erstellen
            await BusyService.ShowAsync(AppResources.bericht_wird_geteilt);

            await Task.Run(async () =>
            {
                await ExportReport.DocX(
                    templatePath,
                    outputPath);
            });

            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();

            // Datei teilen
            bool isShared = false;

            try
            {
                await ShareFileAsync(outputPath);
                isShared = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Fehler beim Teilen des Berichts: {ex}");
            }
            finally
            {
                // Temporäre DOCX-Datei immer löschen
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }

            // Zur vorherigen Seite zurück
            await Shell.Current.GoToAsync("..");

            // Ergebnis anzeigen

            if (isShared)
            {
                _ = SnackbarExtensions.ShowSafeAsync(
                    AppResources.bericht_wurde_geteilt,
                    includeDelay: true);
            }
            else
            {
                _ = SnackbarExtensions.ShowSafeAsync(
                    AppResources.bericht_wurde_nicht_geteilt,
                    includeDelay: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Fehler beim Erstellen des Berichts: {ex}");

            await SnackbarExtensions.ShowSafeAsync(
                AppResources.bericht_wurde_nicht_geteilt,
                includeDelay: true);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupAlert(
                AppResources.exportvorlage_waehlen_oder_importieren);

            await this.ShowPopupAsync<string>(
                popup,
                Settings.PopupOptions);

            return;
        }

        string outputPath = Path.Combine(
            Settings.DataDirectory,
            GlobalJson.Data.ProjectPath,
            GlobalJson.Data.ProjectPath + ".docx");

        string templatePath = Path.Combine(
            Settings.DataDirectory,
            "templates",
            SettingsService.Instance.SelectedTemplate);

        try
        {
            // Bericht erstellen
            await BusyService.ShowAsync(AppResources.bericht_wird_gespeichert);

            await Task.Run(async () =>
            {
                await ExportReport.DocX(
                    templatePath,
                    outputPath);
            });

            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();


            // Datei speichern
            bool isSaved = false;

            if (File.Exists(outputPath))
            {
                try
                {
                    using var saveStream = File.Open(
                        outputPath,
                        FileMode.Open);

                    var fileSaveResult =
                        await FileSaver.Default.SaveAsync(
                            GlobalJson.Data.ProjectPath + ".docx",
                            saveStream);

                    isSaved = fileSaveResult.IsSuccessful;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Fehler beim Speichern: {ex}");
                }
                finally
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
            }


            await Shell.Current.GoToAsync("..");

            if (isSaved)
            {
                _ = SnackbarExtensions.ShowSafeAsync(
                    AppResources.bericht_wurde_gespeichert,
                    includeDelay: true);
            }
            else
            {
                _ = SnackbarExtensions.ShowSafeAsync(
                    AppResources.bericht_wurde_nicht_gespeichert,
                    includeDelay: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Fehler beim Erstellen des Berichts: {ex}");

            await SnackbarExtensions.ShowSafeAsync(
                AppResources.bericht_wurde_nicht_gespeichert,
                includeDelay: true);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
    }

    private static async Task ShareFileAsync(string filePath)
    {
        var file = new ShareFile(filePath);
        await Share.RequestAsync(new ShareFileRequest
        {
            File = file,
            Title = "Teilen"
        });
    }

    private static void LoadDocuments()
    {
        SettingsService.Instance.Templates.Clear();
        var files = Directory.GetFiles(Settings.TemplateDirectory, "*.docx");
        foreach (var file in files)
        {
            SettingsService.Instance.Templates.Add(Path.GetFileName(file));
        }
    }

    private async void OnAddDocument(object sender, EventArgs e)
    {
        var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, iOSFileTypes },          // Verwende das readonly Array für iOS
            { DevicePlatform.Android, AndroidFileTypes },  // Verwende das readonly Array für Android
            { DevicePlatform.WinUI, WinUIFileTypes }       // Verwende das readonly Array für WinUI
        });

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = AppResources.waehle_word_dokument,
            FileTypes = customFileType
        });

        if (result != null)
        {
            var destinationPath = Path.Combine(Settings.TemplateDirectory, result.FileName);
            using (var stream = await result.OpenReadAsync())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            SettingsService.Instance.Templates.Add(result.FileName);
            LoadDocuments();
            SettingsService.Instance.SelectedTemplate = result.FileName;
        }
    }

    private async void OnDeleteDocument(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diese_vorlage_wirklich_loeschen);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result?.Result is not DualPopupResult.Ok) return;

        if (!string.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var filePath = Path.Combine(Settings.TemplateDirectory, SettingsService.Instance.SelectedTemplate);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                SettingsService.Instance.Templates.Remove(SettingsService.Instance.SelectedTemplate);
            }
        }
    }

    private async void OnHelpClicked(object sender, EventArgs e)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("export_placeholder.txt");

        if (stream == null) return;

        using var reader = new StreamReader(stream);
        string stringTxt = await reader.ReadToEndAsync();
        
        await Shell.Current.GoToAsync($"xmleditor?string={stringTxt}&fileMode=R");
    }
}
