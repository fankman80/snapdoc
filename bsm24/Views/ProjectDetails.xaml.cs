#nullable disable

using bsm24.Models;
using PDFtoImage;
using SkiaSharp;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class ProjectDetails : UraniumContentPage
{
    private Boolean isPdfExist = true;

    public ProjectDetails()
    {
        InitializeComponent();

    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (GlobalJson.Data.PlanPdf == null)
            isPdfExist = false;

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

        if (!isPdfExist)
        {
            LoadDataToView.LoadData(new FileResult(Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.JsonFile)));
            isPdfExist = true;
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

    public static async Task<FileResult> PickPdfFileAsync()
    {
        try
        {
            // Öffne den FilePicker nur für PDF-Dateien
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Bitte wähle eine PDF-Datei aus",
                FileTypes = FilePickerFileType.Pdf // Nur PDF-Dateien anzeigen
            });

            if (fileResult != null)
                return fileResult;
        }
        catch (Exception ex)
        {
            // Fehlerbehandlung (z.B. wenn der Benutzer den Picker abbricht)
            Console.WriteLine($"Fehler beim Auswählen der Datei: {ex.Message}");
        }
        return null; // Kein PDF ausgewählt
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
        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "PDF wird konvertiert...";

        await Task.Run(async () =>
        {
            var result = await PickPdfFileAsync();
            var root = GlobalJson.Data;
            byte[] bytearray = File.ReadAllBytes(result.FullPath);
            int pagecount = Conversion.GetPageCount(bytearray);

            for (int i = 0; i < pagecount; i++)
            {
                string imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, "plan_" + i + ".jpg");
                Conversion.SaveJpeg(imgPath, bytearray, i, options: new RenderOptions(Dpi: 300));

                // Bildgrösse auslesen
                var stream = File.OpenRead(imgPath);
                var skBitmap = SKBitmap.Decode(stream);
                Size _imgSize = new(skBitmap.Width, skBitmap.Height);

                Plan plan = new()
                {
                    Name = "Plan " + i,
                    File = "plan_" + i + ".jpg",
                    ImageSize = _imgSize
                };

                // Überprüfen, ob die Plans-Struktur initialisiert ist
                root.Plans ??= [];
                root.Plans["plan_" + i] = plan;
                GlobalJson.SaveToFile();
            }

            GlobalJson.Data.PlanPdf = new Pdf
            {
                File = result.FileName,
            };
        });

        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;
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
