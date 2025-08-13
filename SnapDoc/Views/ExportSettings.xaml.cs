#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
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
        if (String.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupDualResponse("Wählen Sie eine Exportvorlage oder importieren Sie eine neue.");
            var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            if (result.Result != null)
                return;
        }

        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(Settings.DataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "Bericht wird geteilt...";
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
                await Application.Current.Windows[0].Page.DisplayAlert("", "Bericht wurde geteilt", "OK");
            else
                await Toast.Make($"Bericht wurde geteilt").Show();
        }
        catch
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlert("", "Bericht wurde nicht geteilt", "OK");
            else
                await Toast.Make($"Bericht wurde nicht geteilt").Show();
        }

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await Shell.Current.GoToAsync("..");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (String.IsNullOrEmpty(SettingsService.Instance.SelectedTemplate))
        {
            var popup = new PopupAlert("Wählen Sie eine Exportvorlage oder importieren Sie eine neue.");
            await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            return;
        }

        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(Settings.DataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "Bericht wird gespeichert...";
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
                await Application.Current.Windows[0].Page.DisplayAlert("", "Bericht wurde gespeichert", "OK");
            else
                await Toast.Make($"Bericht wurde gespeichert").Show();
        }
        else
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlert("", "Bericht wurde nicht gespeichert", "OK");
            else
                await Toast.Make($"Bericht wurde nicht gespeichert").Show();
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

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//homescreen");
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
            PickerTitle = "Wähle ein Word-Dokument",
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

    private void OnDeleteDocument(object sender, EventArgs e)
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
