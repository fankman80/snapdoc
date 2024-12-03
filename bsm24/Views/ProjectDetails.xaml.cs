#nullable disable

using UraniumUI.Pages;

namespace bsm24.Views;

public partial class ProjectDetails : UraniumContentPage
{
    bool isPdfChanged = false;

    public ProjectDetails()
    {
        InitializeComponent();
        isPdfChanged = false;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        client_name.Text = GlobalJson.Data.Client_name;
        object_address.Text = GlobalJson.Data.Object_address;
        working_title.Text = GlobalJson.Data.Working_title;
        object_name.Text = GlobalJson.Data.Object_name;
        project_manager.Text = GlobalJson.Data.Project_manager;
        creation_date.Date = GlobalJson.Data.Creation_date;

        HeaderUpdate();
        

    }

    private async void OnOkayClicked(object sender, EventArgs e)
    {
        GlobalJson.Data.Client_name = client_name.Text;
        GlobalJson.Data.Object_address = object_address.Text;
        GlobalJson.Data.Working_title = working_title.Text;
        GlobalJson.Data.Object_name = object_name.Text;
        GlobalJson.Data.Project_manager = project_manager.Text;
        GlobalJson.Data.Creation_date = creation_date.Date.Value;

        // save data to file
        GlobalJson.SaveToFile();

        if (isPdfChanged)
        {
            LoadDataToView.ResetFlyoutItems();
            LoadDataToView.LoadData(new FileResult(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.JsonFile)));
        }

        HeaderUpdate();

        // Entferne die aktuelle Seite aus dem Stack
        var currentPage = Shell.Current.CurrentPage;
        Shell.Current.Navigation.RemovePage(currentPage);

        await Shell.Current.GoToAsync("//homescreen");
#if ANDROID
        Shell.Current.FlyoutIsPresented = true;
#endif
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//homescreen");
    }

    private async void OnThumbCaptureClicked(object sender, EventArgs e)
    {
        await CapturePicture.Capture(null, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg");

        HeaderUpdate();
    }

    private async void OnAddPdfClicked(object sender, EventArgs e)
    {
        GlobalJson.Data.Client_name = client_name.Text;
        GlobalJson.Data.Object_address = object_address.Text;
        GlobalJson.Data.Working_title = working_title.Text;
        GlobalJson.Data.Object_name = object_name.Text;
        GlobalJson.Data.Project_manager = project_manager.Text;
        GlobalJson.Data.Creation_date = creation_date.Date.Value;

        // save data to file
        GlobalJson.SaveToFile();

        await Shell.Current.GoToAsync("loadPdfImages");

        isPdfChanged = true;
    }

    private static void HeaderUpdate()
    {
        // aktualisiere den Header Text
        Services.SettingsService.Instance.FlyoutHeaderTitle = GlobalJson.Data.Object_name;
        Services.SettingsService.Instance.FlyoutHeaderDesc = GlobalJson.Data.Client_name;

        // aktualisiere das Thumbnail Bild
        if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg")))
            Services.SettingsService.Instance.FlyoutHeaderImage = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg");
        else
            Services.SettingsService.Instance.FlyoutHeaderImage = "banner_thumbnail.png";
    }
}
