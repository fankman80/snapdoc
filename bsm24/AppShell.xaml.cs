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
    }

    public async void OnExportClicked(object sender, EventArgs e)
    {
        var popup = new PopupExportSettings();
        await MopupService.Instance.PushAsync(popup);
    }
}
