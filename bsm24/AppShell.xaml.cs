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

            LoadDataToView.ResetApp();

            Helper.AddMenuItem("Bericht exportieren", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Download, "OnExportClicked");
            Helper.AddMenuItem("Bericht teilen", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Share, "OnShareClicked");
            Helper.AddMenuItem("Einstellungen", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Settings, "OnSettingsClicked");
            Helper.AddDivider();

            GlobalJson.CreateNewFile(filePath);
            GlobalJson.Data.Client_name = "";
            GlobalJson.Data.Object_address = "";
            GlobalJson.Data.Working_title = "";
            GlobalJson.Data.Object_name = "";
            GlobalJson.Data.Creation_date = DateTime.Parse(DateTime.Now.Date.ToString("d", new CultureInfo("de-DE"))).ToString();
            GlobalJson.Data.Project_manager = "";
            GlobalJson.Data.ProjectPath = Path.Combine(result);
            GlobalJson.Data.JsonFile = Path.Combine(result, result + ".json");
            GlobalJson.Data.PlanPath = Path.Combine(result, "plans");
            GlobalJson.Data.ImagePath = Path.Combine(result, "images");
            GlobalJson.Data.ImageOverlayPath = Path.Combine(result, "images", "originals");
            GlobalJson.Data.ThumbnailPath = Path.Combine(result, "thumbnails");

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
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        await ExportReport.DocX("template.docx", outputPath);

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
    }

    public async void OnExportClicked(object sender, EventArgs e)
    { 
        string outputPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".docx");
        await ExportReport.DocX("template.docx", outputPath);

        CancellationToken cancellationToken = new();
        var saveStream = File.Open(outputPath, FileMode.Open);
        var fileSaveResult = await FileSaver.Default.SaveAsync(GlobalJson.Data.ProjectPath + ".docx", saveStream, cancellationToken);
        if (fileSaveResult.IsSuccessful)
            await Toast.Make($"Bericht wurde gespeichert").Show(cancellationToken);
        else
            await Toast.Make($"Bericht wurde nicht gespeichert").Show(cancellationToken);
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
