#nullable disable

using CommunityToolkit.Maui.Storage;
using Mopups.Pages;
using Mopups.Services;
using CommunityToolkit.Maui.Alerts;

namespace bsm24.Views;

public partial class PopupExportSettings : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }

    public PopupExportSettings()
	{
		InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
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

        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Bericht wird geteilt...";
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX("template_ebbe.docx", outputPath);
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


        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Bericht wird gespeichert...";
        // Hintergrundoperation (nicht UI-Operationen)
        await Task.Run(async () =>
        {
            await ExportReport.DocX("template_ebbe.docx", outputPath);
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

    private async void OnTemplateManagerClicked(object sender, EventArgs e)
    {
        var popup = new PopupTemplateManager();
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
        }
    }
}
