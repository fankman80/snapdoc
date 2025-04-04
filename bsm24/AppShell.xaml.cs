#nullable disable

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
        Routing.RegisterRoute("open_project", typeof(OpenProject));
        Routing.RegisterRoute("icongallery", typeof(IconGallery));
        Routing.RegisterRoute("setpin", typeof(SetPin));
        Routing.RegisterRoute("imageview", typeof(ImageViewPage));
        Routing.RegisterRoute("project_details", typeof(ProjectDetails));
        Routing.RegisterRoute("loadPdfImages", typeof(LoadPDFPages));
        Routing.RegisterRoute("pinList", typeof(PinList));
        Routing.RegisterRoute("exportSettings", typeof(ExportSettings));
        Routing.RegisterRoute("mapview", typeof(MapView));
        BindingContext = this;
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupSettings();
        await MopupService.Instance.PushAsync(popup);
    }

    public void OnTitleClicked(object sender, EventArgs e)
    {
        if (GlobalJson.Data.JsonFile != null)
        {
            var projectDetails = new ProjectDetails();
            projectDetails.OnTitleCaptureClicked(null, null);
        }
    }

    public void OnUpArrowClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button && button.BindingContext is FlyoutItem flyoutItem)
        {
            Helper.MoveItem(flyoutItem.AutomationId, -1);
        }
    }

    public void OnDownArrowClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button && button.BindingContext is FlyoutItem flyoutItem)
        {
            Helper.MoveItem(flyoutItem.AutomationId, 1);
        }
    }
}