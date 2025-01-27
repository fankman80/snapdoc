#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Mopups.Pages;
using Mopups.Services;
using bsm24.Services;

namespace bsm24.Views;

public partial class PopupExportSettings : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }

    private static readonly string[] iOSFileTypes = ["com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document"];
    private static readonly string[] AndroidFileTypes = ["application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];
    private static readonly string[] WinUIFileTypes = [".doc", ".docx"];

    public PopupExportSettings()
	{
		InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LoadDocuments();

        if (SettingsService.Instance.SelectedTemplate == null)
            SettingsService.Instance.SelectedTemplate = SettingsService.Instance.Templates.First();

        _taskCompletionSource = new TaskCompletionSource<string>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(FileSystem.AppDataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Bericht wird geteilt...";
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX(templatePath, outputPath);
        });
        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;

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

        await MopupService.Instance.PopAsync();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        string templatePath = Path.Combine(FileSystem.AppDataDirectory, "templates", SettingsService.Instance.SelectedTemplate);

        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Bericht wird gespeichert...";
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX(templatePath, outputPath);
        });
        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;

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

        await MopupService.Instance.PopAsync();
    }

    private void OnColorPickClicked(object sender, EventArgs e)
    {

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
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
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
