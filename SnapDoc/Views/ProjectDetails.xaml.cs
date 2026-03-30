#nullable disable

using CommunityToolkit.Maui.Extensions;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;

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

        (FileResult result, System.Drawing.Size imgSize) = await CapturePicture.Capture(Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath), GlobalJson.Data.ProjectPath, thumbFileName);
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
                PickerTitle = AppResources.bitte_waehle_bild,
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
                    GlobalJson.Data.TitleImageSize = new System.Drawing.Size(codec.Info.Size.Width, codec.Info.Size.Height);
                else
                    GlobalJson.Data.TitleImageSize = new System.Drawing.Size(500, 500);

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

    private async void OnAddWebMapClicked(object sender, EventArgs e)
    {
        UpdateProjectData();

        var popup = new PopupEntry(title: AppResources.karte_aus_webmap + "." + Environment.NewLine + AppResources.online_map_requirement_hint, okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
        {
            string planId = "webmap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Plan plan = new()
            {
                Name = result.Result == "" ? "Online Map" : result.Result,
                File = "",
                ImageSize = new Size(0,0),
                IsGrayscale = false,
                Description = "",
                AllowExport = true,
                PlanColor = "#00FFFFFF"
            };

            var newPlan = new KeyValuePair<string, Plan>(planId, plan);
            LoadDataToView.AddPlan(newPlan);

            // Überprüfen, ob die Plans-Struktur initialisiert ist
            GlobalJson.Data.Plans ??= [];
            GlobalJson.Data.Plans[planId] = plan;

            // save data to file
            GlobalJson.SaveToFile();

            // Shell aktualisieren
            var shell = Application.Current.Windows[0].Page as AppShell;
            shell.ApplyFilterAndSorting();

            await Shell.Current.GoToAsync($"//{planId}");
        }
    }

    private void UpdateProjectData()
    {
        GlobalJson.Data.Client_name = client_name.Text;
        GlobalJson.Data.Object_address = object_address.Text;
        GlobalJson.Data.Working_title = working_title.Text;
        GlobalJson.Data.Project_nr = project_nr.Text;
        GlobalJson.Data.Object_name = object_name.Text;
        GlobalJson.Data.Project_manager = project_manager.Text;
        GlobalJson.Data.Creation_date = (creation_date.Date == DateTime.MinValue)
                                        ? DateTime.Today
                                        : creation_date.Date;

        // save data to file
        GlobalJson.SaveToFile();
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"imageview?imgSource=showTitle&gotoBtn=false");
    }
}
