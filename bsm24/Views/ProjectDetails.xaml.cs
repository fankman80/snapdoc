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

        Helper.HeaderUpdate();
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

        Helper.HeaderUpdate();

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

    private async void OnTitleCaptureClicked(object sender, EventArgs e)
    {
        await CapturePicture.Capture(GlobalJson.Data.ImagePath, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg");

        Helper.HeaderUpdate();
    }

    private async void OnTitleOpenClicked(object sender, EventArgs e)
    {
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Bitte wähle ein Bild aus...",
                FileTypes = FilePickerFileType.Jpeg
            });
            
            if (fileResult != null)
            {
                string sourceFilePath = fileResult.FullPath;
                var destinationPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, "title_thumbnail.jpg");
                var destinationThumbPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, "title_thumbnail.jpg");

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                if (File.Exists(destinationThumbPath))
                    File.Delete(destinationThumbPath);

                using (FileStream sourceStream = new(sourceFilePath, FileMode.OpenOrCreate))
                using (FileStream destinationStream = new(destinationPath, FileMode.Create))
                {
                    sourceStream.CopyTo(destinationStream);
                }

                Thumbnail.Generate(sourceFilePath, destinationThumbPath);

                Helper.HeaderUpdate();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Auswählen der Datei: {ex.Message}");
        }
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
}
