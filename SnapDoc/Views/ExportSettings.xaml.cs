#nullable disable
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Reporting;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class ExportSettings : ContentPage
{
    private static readonly string[] iOSFileTypes = ["com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document"];
    private static readonly string[] AndroidFileTypes = ["application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];
    private static readonly string[] WinUIFileTypes = [".doc", ".docx"];
    private bool _isCloudUploading;

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
            var popup = new PopupAlert(AppResources.exportvorlage_waehlen_oder_importieren);
            await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            return;
        }

        string outputPath = Path.Combine(
            Settings.DataDirectory,
            SettingsService.Instance.ProjectPath,
            SettingsService.Instance.ProjectPath + ".docx");

        string templatePath = Path.Combine(
            Settings.DataDirectory,
            "templates",
            SettingsService.Instance.SelectedTemplate);

        try
        {
            // Bericht erstellen
            await BusyService.ShowAsync(AppResources.bericht_wird_geteilt);

            DeleteTemporaryReport(outputPath);
            ReportExportResult exportResult = await CreateReportAsync(templatePath, outputPath);

            if (!exportResult.Success)
            {
                DeleteTemporaryReport(outputPath);

                await BusyService.HideAsync();
                await ShowExportErrorAsync(exportResult);

                return;
            }

            if (!File.Exists(outputPath))
            {
                await BusyService.HideAsync();
                await ShowMissingExportFileAsync(outputPath);
                return;
            }

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
                System.Diagnostics.Debug.WriteLine($"Fehler beim Teilen des Berichts: {ex}");
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
                await this.DisplaySnackbar(AppResources.bericht_wurde_geteilt, actionButtonText: AppResources.ok);
            else
                await this.DisplaySnackbar(AppResources.bericht_wurde_nicht_geteilt, actionButtonText: AppResources.ok);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen des Berichts: {ex}");
            await this.DisplaySnackbar(AppResources.bericht_wurde_nicht_geteilt, actionButtonText: AppResources.ok);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            await BusyService.HideAsync();
        }
    }

    private async void OnSaveClicked(
        object sender,
        EventArgs e)
    {
        if (string.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupAlert(AppResources.exportvorlage_waehlen_oder_importieren);
            await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            return;
        }

        string outputPath = Path.Combine(
            Settings.DataDirectory,
            SettingsService.Instance.ProjectPath,
            SettingsService.Instance.ProjectPath + ".docx");

        string templatePath = Path.Combine(
            Settings.DataDirectory,
            "templates",
            SettingsService.Instance.SelectedTemplate);

        try
        {
            await BusyService.ShowAsync(AppResources.bericht_wird_gespeichert);

            DeleteTemporaryReport(outputPath);
            ReportExportResult exportResult = await CreateReportAsync(templatePath, outputPath);

            await BusyService.HideAsync();

            if (!exportResult.Success)
            {
                DeleteTemporaryReport(outputPath);

                await BusyService.HideAsync();
                await ShowExportErrorAsync(exportResult);

                return;
            }

            if (!File.Exists(outputPath))
            {
                await ShowMissingExportFileAsync(outputPath);
                return;
            }

            bool isSaved;

            try
            {
                await using var saveStream = new FileStream(
                    outputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                var fileSaveResult = await FileSaver.Default.SaveAsync(SettingsService.Instance.ProjectPath + ".docx", saveStream);

                isSaved = fileSaveResult.IsSuccessful;

                if (!isSaved && fileSaveResult.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"FileSaver-Fehler: " + $"{fileSaveResult.Exception}");
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }

            await Shell.Current.GoToAsync("..");

            if (isSaved)
            {
                await this.DisplaySnackbar(AppResources.bericht_wurde_gespeichert, actionButtonText: AppResources.ok);
            }
            else
            {
                await this.DisplaySnackbar(AppResources.bericht_wurde_nicht_gespeichert, actionButtonText: AppResources.ok);
            }
        }
        catch (Exception ex)
        {
            DeleteTemporaryReport(outputPath);

            System.Diagnostics.Debug.WriteLine($"Fehler beim Erstellen oder Speichern des Berichts: {ex}");
            await DisplayAlertAsync("Bericht konnte nicht gespeichert werden", $"{ex.GetType().Name}: {ex.Message}", AppResources.ok);
        }
        finally
        {
            await BusyService.HideAsync();
        }
    }

    private async void OnCloudUploadClicked(object sender, EventArgs e)
    {
        if (_isCloudUploading)
            return;
        _isCloudUploading = true;

        try
        {
            if (string.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
            {
                var popup = new PopupAlert(AppResources.exportvorlage_waehlen_oder_importieren);
                await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
                return;
            }

            // 1. Zielordner + Dateiname wählen (null = abgebrochen)
            string defaultName = SettingsService.Instance.ProjectPath + ".docx";
            var target = await CloudPickerPage.PickExportFolderAsync(defaultName);
            if (target == null)
                return;

            // Lokale Datei bleibt beim Projektnamen, hochgeladen wird mit dem gewählten Namen
            string localFileName = SettingsService.Instance.ProjectPath + ".docx";
            string outputPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, localFileName);
            string templatePath = Path.Combine(Settings.DataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

            string uploadError = null;
            try
            {
                await BusyService.ShowAsync(AppResources.bericht_wird_hochgeladen);

                DeleteTemporaryReport(outputPath);
                ReportExportResult exportResult = await CreateReportAsync(templatePath, outputPath);

                if (!exportResult.Success)
                {
                    await BusyService.HideAsync();
                    await ShowExportErrorAsync(exportResult);
                    return;
                }

                // 3. Hochladen
                uploadError = File.Exists(outputPath)
                    ? await SaveManager.UploadFileToCloudAsync(target.DriveId, target.FolderId, outputPath, target.FileName)
                    : $"Bericht wurde nicht erstellt: {outputPath}";
            }
            catch (Exception ex)
            {
                uploadError = $"{ex.GetType().Name} – {ex.Message}";
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                await BusyService.HideAsync();
            }

            if (uploadError != null)
            {
                System.Diagnostics.Debug.WriteLine($"Cloud-Upload fehlgeschlagen: {uploadError}");
                await Shell.Current.GoToAsync("..");
                await this.DisplaySnackbar(AppResources.bericht_wurde_nicht_hochgeladen, actionButtonText: AppResources.ok);
                return;
            }

            await Shell.Current.GoToAsync("..");
            await this.DisplaySnackbar(string.Format(AppResources.bericht_wurde_hochgeladen, target.FolderName), actionButtonText: AppResources.ok);
        }
        finally
        {
            _isCloudUploading = false;
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

    private static async Task<ReportExportResult> CreateReportAsync(string templatePath, string outputPath)
    {
        return await Task.Run(
            () => ExportReport.DocX(
                templatePath,
                outputPath));
    }

    private static string FormatExportMessages(ReportExportResult result)
    {
        var relevantMessages = result.Messages
            .Where(message =>
                message.Severity is
                    ReportMessageSeverity.Error or
                    ReportMessageSeverity.Warning)
            .OrderBy(message =>
                message.Severity == ReportMessageSeverity.Error
                    ? 0
                    : 1)
            .ToList();

        if (relevantMessages.Count == 0)
            return result.Exception?.Message ?? "Der Bericht konnte nicht erstellt werden.";

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            relevantMessages.Select(message =>
            {
                string severity = message.Severity switch
                {
                    ReportMessageSeverity.Error => "Fehler",
                    ReportMessageSeverity.Warning => "Warnung",
                    _ => "Hinweis"
                };

                return string.IsNullOrWhiteSpace(message.Location)
                    ? $"{severity}: {message.Text}"
                    : $"{severity} – {message.Location}:{Environment.NewLine}" +
                      message.Text;
            }));
    }

    private async Task ShowExportErrorAsync(ReportExportResult result)
    {
        string message = FormatExportMessages(result);

        System.Diagnostics.Debug.WriteLine(
            $"Report-Export fehlgeschlagen:{Environment.NewLine}{message}");

        var popup = new PopupAlert(message);

        await this.ShowPopupAsync<string>(
            popup,
            Settings.PopupOptions);
    }

    private static void DeleteTemporaryReport(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Temporäre Reportdatei konnte nicht gelöscht werden: {ex}");
        }
    }

    private async Task ShowMissingExportFileAsync(string outputPath)
    {
        var result = new ReportExportResult(
            Success: false,
            OutputPath: null,
            Messages: new ReportMessage[]
            {
            new(
                ReportMessageSeverity.Error,
                "Der Export wurde als erfolgreich gemeldet, " +
                "die erzeugte DOCX-Datei wurde jedoch nicht gefunden.",
                outputPath)
            });

        await ShowExportErrorAsync(result);
    }
}
