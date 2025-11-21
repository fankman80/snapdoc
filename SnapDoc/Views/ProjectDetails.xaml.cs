#nullable disable

using SkiaSharp;

namespace SnapDoc.Views;

public partial class ProjectDetails : ContentPage
{
    public ProjectDetails()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        client_name.Text = GlobalJson.Data.Client_name;
        object_address.Text = GlobalJson.Data.Object_address;
        working_title.Text = GlobalJson.Data.Working_title;
        project_nr.Text = GlobalJson.Data.Project_nr;
        object_name.Text = GlobalJson.Data.Object_name;
        project_manager.Text = GlobalJson.Data.Project_manager;
        creation_date.Date = GlobalJson.Data.Creation_date;

        Helper.HeaderUpdate();
    }

    private async void OnOkayClicked(object sender, EventArgs e)
    {
        UpdateProjectData();

        Helper.HeaderUpdate();

        await Shell.Current.GoToAsync("//homescreen");

#if ANDROID
        Shell.Current.FlyoutIsPresented = true;
#endif
    }

    public async void OnTitleCaptureClicked(object sender, EventArgs e)
    {
        string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";

        (FileResult result, Size imgSize) = await CapturePicture.Capture(Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath), GlobalJson.Data.ProjectPath, thumbFileName);
        if (result != null)
        {
            if (File.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage))) // delete old Thumbnail
                File.Delete(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage));
            if (File.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage))) // delete old Title Image
                File.Delete(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage));
            GlobalJson.Data.TitleImage = thumbFileName;
            GlobalJson.Data.TitleImageSize = imgSize;
            GlobalJson.SaveToFile();
            Helper.HeaderUpdate();
        }
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
                string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";
                string sourceFilePath = fileResult.FullPath;
                var codec = SKCodec.Create(fileResult.FullPath);
                var destinationPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, thumbFileName);
                var destinationThumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, thumbFileName);

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

                if (File.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage))) // delete old Thumbnail
                    File.Delete(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage));
                if (File.Exists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage))) // delete old Title Image
                    File.Delete(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage));
                GlobalJson.Data.TitleImage = thumbFileName;

                if (codec != null)
                    GlobalJson.Data.TitleImageSize = new Size(codec.Info.Size.Width, codec.Info.Size.Height);
                else
                    GlobalJson.Data.TitleImageSize = new Size(500, 500);

                GlobalJson.SaveToFile();
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
        UpdateProjectData();

        await Shell.Current.GoToAsync("loadPdfImages");
    }

    private void UpdateProjectData()
    {
        GlobalJson.Data.Client_name = client_name.Text;
        GlobalJson.Data.Object_address = object_address.Text;
        GlobalJson.Data.Working_title = working_title.Text;
        GlobalJson.Data.Project_nr = project_nr.Text;
        GlobalJson.Data.Object_name = object_name.Text;
        GlobalJson.Data.Project_manager = project_manager.Text;
        GlobalJson.Data.Creation_date = creation_date.Date ?? DateTime.Today;

        // save data to file
        GlobalJson.SaveToFile();
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"imageview?imgSource=showTitle");
    }
}
