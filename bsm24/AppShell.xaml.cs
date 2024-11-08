#nullable disable

using bsm24.Services;
using bsm24.Views;
using Mopups.Services;
using System.Windows.Input;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Maui.Alerts;
using System.Threading;
using System.Globalization;

namespace bsm24;

public partial class AppShell : Shell
{
    public Dictionary<string, Type> Routes { get; private set; } = [];

    public static ICommand HelpCommand => new Command<string>(async (url) => await Launcher.OpenAsync(url));

    public AppShell()
    {
        InitializeComponent();
        BindingContext = SettingsService.Instance;
        Routing.RegisterRoute("icongallery", typeof(IconGallery));
        Routing.RegisterRoute("setpin", typeof(SetPin));
        Routing.RegisterRoute("imageview", typeof(ImageViewPage));
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry("Neues Projekt anlegen?", "Erstellen");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
            string filePath = Path.Combine(FileSystem.AppDataDirectory, result, result + ".json");

            await new LoadDataToView().ResetApp();

            GlobalJson.CreateNewFile(filePath);
            GlobalJson.Data.client_name = "";
            GlobalJson.Data.object_address = "";
            GlobalJson.Data.working_title = "";
            GlobalJson.Data.object_name = "";
            GlobalJson.Data.creation_date = DateTime.Parse(DateTime.Now.Date.ToString("d", new CultureInfo("de-DE"))).ToString();
            GlobalJson.Data.project_manager = "";
            GlobalJson.Data.projectPath = Path.Combine(result);
            GlobalJson.Data.jsonFile = Path.Combine(result, result + ".json");
            GlobalJson.Data.planPath = Path.Combine(result, "plans");
            GlobalJson.Data.imagePath = Path.Combine(result, "images");
            GlobalJson.Data.imageOverlayPath = Path.Combine(result, "images", "overlays");
            GlobalJson.Data.thumbnailPath = Path.Combine(result, "thumbnails");

            // save data to file
            GlobalJson.SaveToFile();

            await Shell.Current.GoToAsync("//project_details");
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupSettings();
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {

        }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.projectPath, GlobalJson.Data.projectPath + ".docx");
        await ExportReport.DocX("template.docx", outputPath);

        CancellationToken cancellationToken = new();
        try
        {
            await ShareFileAsync(outputPath);
            await Toast.Make($"File is exported").Show(cancellationToken);
        }
        catch
        {
            await Toast.Make($"File is not exported").Show(cancellationToken);
        }
        File.Delete(outputPath);
    }

    private async void OnExportClicked(object sender, EventArgs e)
    { 
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.projectPath, GlobalJson.Data.projectPath + ".docx");
        await ExportReport.DocX("template.docx", outputPath);

        CancellationToken cancellationToken = new();
        var saveStream = File.Open(outputPath, FileMode.Open);
        var fileSaveResult = await FileSaver.Default.SaveAsync(GlobalJson.Data.projectPath + ".docx", saveStream, cancellationToken);
        if (fileSaveResult.IsSuccessful)
            await Toast.Make($"File is saved: {fileSaveResult.FilePath}").Show(cancellationToken);
        else
            await Toast.Make($"File is not saved, {fileSaveResult.Exception.Message}").Show(cancellationToken);
        saveStream.Close();
        File.Delete(outputPath);
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
}
