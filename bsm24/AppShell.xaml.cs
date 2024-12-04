#nullable disable

using bsm24.Services;
using bsm24.Views;
using Mopups.Services;
using System.Windows.Input;

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
        Routing.RegisterRoute("project_details", typeof(ProjectDetails));
        Routing.RegisterRoute("loadPdfImages", typeof(LoadPDFPages));
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry("Neues Projekt anlegen?", "Erstellen");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {
            // Prüfe, ob die Datei existiert und hänge fortlaufend eine Nummer an
            int counter = 1;
            string _result = result;
            while (Directory.Exists(Path.Combine(FileSystem.AppDataDirectory, _result)))
            {
                _result = Path.Combine($"{result} ({counter})");
                counter++;
            }
            result = _result;

            string filePath = Path.Combine(FileSystem.AppDataDirectory, result, result + ".json");

            LoadDataToView.ResetFlyoutItems();
            LoadDataToView.ResetData();

            GlobalJson.CreateNewFile(filePath);
            GlobalJson.Data.Client_name = "";
            GlobalJson.Data.Object_address = "";
            GlobalJson.Data.Working_title = "";
            GlobalJson.Data.Object_name = "";
            GlobalJson.Data.Creation_date = DateTime.Now;
            GlobalJson.Data.Project_manager = "";
            GlobalJson.Data.ProjectPath = Path.Combine(result);
            GlobalJson.Data.JsonFile = Path.Combine(result, result + ".json");
            GlobalJson.Data.PlanPath = Path.Combine(result, "plans");
            GlobalJson.Data.ImagePath = Path.Combine(result, "images");
            GlobalJson.Data.ImageOverlayPath = Path.Combine(result, "images", "originals");
            GlobalJson.Data.ThumbnailPath = Path.Combine(result, "thumbnails");

            // save data to file
            GlobalJson.SaveToFile();

            await Shell.Current.GoToAsync("project_details");
#if ANDROID
            Shell.Current.FlyoutIsPresented = false;
#endif
        }
    }

    private async void OnProjectDetailsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("project_details");
#if ANDROID
        Shell.Current.FlyoutIsPresented = false;
#endif
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

    public async void OnExportClicked(object sender, EventArgs e)
    {
        // Show Settings Page
        var popup = new PopupExportSettings();
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {

        }
    }
}
