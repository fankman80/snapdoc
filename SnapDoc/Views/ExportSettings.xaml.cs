#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Services;
using SnapDoc.Resources.Languages;

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
        if (String.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupDualResponse(AppResources.exportvorlage_waehlen_oder_importieren);
            var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            if (result.Result != null)
                return;
        }

        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(Settings.DataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = AppResources.bericht_wird_geteilt;
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX(templatePath, outputPath);
        });
        busyOverlay.IsActivityRunning = false;
        busyOverlay.IsOverlayVisible = false;

        try
        {
            await ShareFileAsync(outputPath);
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.bericht_wurde_geteilt, AppResources.ok);
            else
                await Toast.Make(AppResources.bericht_wurde_geteilt).Show();
        }
        catch
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.bericht_wurde_nicht_geteilt, AppResources.ok);
            else
                await Toast.Make(AppResources.bericht_wurde_nicht_geteilt).Show();
        }

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (String.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupAlert(AppResources.exportvorlage_waehlen_oder_importieren);
            await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            return;
        }

        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(Settings.DataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = AppResources.bericht_wird_gespeichert;
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX(templatePath, outputPath);
        });
        busyOverlay.IsActivityRunning = false;
        busyOverlay.IsOverlayVisible = false;

        var saveStream = File.Open(outputPath, FileMode.Open);
        var fileSaveResult = await FileSaver.Default.SaveAsync(GlobalJson.Data.ProjectPath + ".docx", saveStream);
        if (fileSaveResult.IsSuccessful)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.bericht_wurde_gespeichert, AppResources.ok);
            else
                await Toast.Make(AppResources.bericht_wurde_gespeichert).Show();
        }
        else
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.bericht_wurde_nicht_gespeichert, AppResources.ok);
            else
                await Toast.Make(AppResources.bericht_wurde_nicht_gespeichert).Show();
        }
        saveStream.Close();

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await Shell.Current.GoToAsync("//homescreen");
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
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
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
    }

    private async void OnHelpClicked(object sender, EventArgs e)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("export_placeholder.txt");
        if (stream == null)
            return;

        using var reader = new StreamReader(stream);
        string stringTxt = await reader.ReadToEndAsync();
        
        await Shell.Current.GoToAsync($"xmleditor?string={stringTxt}&fileMode=R");
    }
}
