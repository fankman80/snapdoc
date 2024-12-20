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

        CancellationToken cancellationToken = new();
        try
        {
            await ShareFileAsync(outputPath);
            await Toast.Make($"Bericht wurde geteilt").Show(cancellationToken);
        }
        catch
        {
            await Toast.Make($"Bericht wurde nicht geteilt").Show(cancellationToken);
        }
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

        CancellationToken cancellationToken = new();
        var saveStream = File.Open(outputPath, FileMode.Open);
        var fileSaveResult = await FileSaver.Default.SaveAsync(GlobalJson.Data.ProjectPath + ".docx", saveStream, cancellationToken);
        if (fileSaveResult.IsSuccessful)
            await Toast.Make($"Bericht wurde gespeichert").Show(cancellationToken);
        else
            await Toast.Make($"Bericht wurde nicht gespeichert").Show(cancellationToken);
        saveStream.Close();
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
}
