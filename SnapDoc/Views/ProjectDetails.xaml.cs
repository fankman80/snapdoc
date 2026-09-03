#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Messages;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

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

        LoadDataToUI();

        WeakReferenceMessenger.Default.Register<RemoteDataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == RemoteChangeType.ProjectDetailsUpdated)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadDataToUI();
                });
            }
        });

        WeakReferenceMessenger.Default.Register<TitleImageChangedMessage>(this, (r, m) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadDataToUI();
                Helper.HeaderUpdate();
            });
        });

        Helper.HeaderUpdate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        WeakReferenceMessenger.Default.Unregister<RemoteDataChangedMessage>(this);

        WeakReferenceMessenger.Default.Unregister<TitleImageChangedMessage>(this);
    }

    private async void OnOkayClicked(object sender, EventArgs e)
    {
        UpdateProjectData();

        Helper.HeaderUpdate();

        await Shell.Current.GoToAsync("//homescreen");

#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = true;
#endif
    }

    public async void OnTitleCaptureClicked(object sender, EventArgs e)
    {
        // 1. Alten Dateinamen vor der Kameraaufnahme sichern
        string oldTitleImage = GlobalJson.Data.TitleImage;
        string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";

        (FileResult result, Size imgSize) = await CapturePicture.Capture(
            Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath),
            Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath),
            thumbFileName, true, true);

        if (result != null)
        {
            // 2. Alte Dateien lokal UND aus der Cloud loeschen
            if (!string.IsNullOrEmpty(oldTitleImage) && oldTitleImage != "banner_thumbnail.png")
            {
                var oldThumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, oldTitleImage);
                var oldImagePath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, oldTitleImage);

                if (File.Exists(oldThumbPath)) File.Delete(oldThumbPath);
                if (File.Exists(oldImagePath)) File.Delete(oldImagePath);

                _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ThumbnailPath}/{oldTitleImage}");
                _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ImagePath}/{oldTitleImage}");
            }

            // 3. JSON aktualisieren
            GlobalJson.Data.TitleImage = thumbFileName;
            GlobalJson.Data.TitleImageSize = imgSize;
            GlobalJson.SaveToFile();

            // Absolute Pfade der neuen Dateien ermitteln
            var destinationPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, thumbFileName);
            var destinationThumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, thumbFileName);

            // 4. JSON-Aenderungen UND die 2 neuen Bilddateien an SaveManager uebergeben
            SaveManager.NotifyDataChanged([(destinationPath, GlobalJson.Data.ImagePath), (destinationThumbPath, GlobalJson.Data.ThumbnailPath)]);

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
                // 1. Alten Dateinamen vor dem Ueberschreiben merken
                string oldTitleImage = GlobalJson.Data.TitleImage;

                string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";
                string sourceFilePath = fileResult.FullPath;

                var destinationPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, thumbFileName);
                var destinationThumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, thumbFileName);

                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
                if (File.Exists(destinationThumbPath))
                    File.Delete(destinationThumbPath);

                using (FileStream sourceStream = new(sourceFilePath, FileMode.OpenOrCreate))
                using (FileStream destinationStream = new(destinationPath, FileMode.Create))
                {
                    sourceStream.CopyTo(destinationStream);
                }
                await Thumbnail.Generate(sourceFilePath, destinationThumbPath);

                // 2. Alte Dateien lokal UND in der Cloud loeschen
                if (!string.IsNullOrEmpty(oldTitleImage) && oldTitleImage != "banner_thumbnail.png")
                {
                    var oldThumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, oldTitleImage);
                    var oldImagePath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, oldTitleImage);

                    if (File.Exists(oldThumbPath)) File.Delete(oldThumbPath);
                    if (File.Exists(oldImagePath)) File.Delete(oldImagePath);

                    // Cloud-Loeschung anstossen
                    _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ThumbnailPath}/{oldTitleImage}");
                    _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ImagePath}/{oldTitleImage}");
                }

                // 3. JSON aktualisieren
                GlobalJson.Data.TitleImage = thumbFileName;

                // Codec in einem using-Block kapseln, um Memory Leaks zu verhindern
                using (var codec = SKCodec.Create(sourceFilePath))
                {
                    if (codec != null)
                        GlobalJson.Data.TitleImageSize = new Size(codec.Info.Size.Width, codec.Info.Size.Height);
                    else
                        GlobalJson.Data.TitleImageSize = new Size(500, 500);
                }
                
                GlobalJson.SaveToFile();
                
                // 4. JSON UND die zwei neuen Bilddateien fuer den Upload registrieren
                SaveManager.NotifyDataChanged([
                    (destinationPath, GlobalJson.Data.ImagePath), // Originalbild im Images-Ordner
                    (destinationThumbPath, GlobalJson.Data.ThumbnailPath) // Thumbnailbild im Thumbnails-Ordner
                ]);

                Helper.HeaderUpdate();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Auswaehlen der Datei: {ex.Message}");
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

        var popup = new PopupEntry(header: AppResources.karte_aus_webmap,
                                   desc: AppResources.online_map_requirement_hint + ".",
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

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
        SaveManager.NotifyDataChanged();

        // Shell aktualisieren
        var shell = Shell.Current as AppShell;
        shell.ApplyFilterAndSorting();

        await Shell.Current.GoToAsync($"//{planId}");
    }

    private async void CalendarClicked(object sender, EventArgs e)
    {
        UpdateProjectData();

        var popup = new PopupCalendarView(DateTime.TryParse(creation_date.Text, out DateTime parsedDate) ? parsedDate : DateTime.Today);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (!string.IsNullOrEmpty(result.Result))
            creation_date.Text = result.Result;
    }

    private void UpdateProjectData()
    {
        GlobalJson.Data.Client_name = client_name.Text;
        GlobalJson.Data.Object_address = object_address.Text;
        GlobalJson.Data.Working_title = working_title.Text;
        GlobalJson.Data.Project_nr = project_nr.Text;
        GlobalJson.Data.Object_name = object_name.Text;
        GlobalJson.Data.Project_manager = project_manager.Text;
        GlobalJson.Data.Creation_date = DateTime.TryParse(creation_date.Text, out DateTime parsedDate) ? parsedDate : DateTime.Today;

        // save data to file
        SaveManager.NotifyDataChanged();
    }

    private void LoadDataToUI()
    {
        if (!client_name.IsFocused) client_name.Text = GlobalJson.Data.Client_name;
        if (!object_address.IsFocused) object_address.Text = GlobalJson.Data.Object_address;
        if (!working_title.IsFocused) working_title.Text = GlobalJson.Data.Working_title;
        if (!project_nr.IsFocused) project_nr.Text = GlobalJson.Data.Project_nr;
        if (!object_name.IsFocused) object_name.Text = GlobalJson.Data.Object_name;
        if (!project_manager.IsFocused) project_manager.Text = GlobalJson.Data.Project_manager;

        creation_date.Text = GlobalJson.Data.Creation_date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"imageview?imgSource=showTitle&gotoBtn=false");
    }
}
