#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Globalization;

namespace SnapDoc.Views;

public partial class ProjectDetails : ContentPage
{
    // Wert in TitleImage, wenn kein eigenes Bild gesetzt ist (HasTitleImage = false).
    private const string DefaultTitleImage = "banner_thumbnail.png";

    public ProjectDetails()
    {
        InitializeComponent();
        BindingContext = ProjectItem.Current;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BindingContext = ProjectItem.Current;
        WeakReferenceMessenger.Default.Register<TitleCaptureRequestedMessage>(this, (r, m) =>
            MainThread.BeginInvokeOnMainThread(() => OnTitleCaptureClicked(null, null)));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<TitleCaptureRequestedMessage>(this);
    }

    private async void OnOkayClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//homescreen");
#if ANDROID || IOS
        Shell.Current.FlyoutIsPresented = true;
#endif
    }

    // ------------------------------------------------------------------
    // Titelbild
    // ------------------------------------------------------------------

    /// <summary>
    /// Tippen aufs Bild: eigenes Bild -> Vollansicht, Platzhalter -> Auswahl.
    /// </summary>
    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (!ProjectItem.Current.HasTitleImage)
        {
            OnChangeTitleImageClicked(sender, e);
            return;
        }

        await Shell.Current.GoToAsync($"imageview?imgSource=showTitle&gotoBtn=false");
    }

    /// <summary>
    /// Auswahl fuer den Bearbeiten-Button. "Entfernen" nur bei eigenem Bild.
    /// </summary>
    private async void OnChangeTitleImageClicked(object sender, EventArgs e)
    {
        string camera = AppResources.titelbild_mit_der_kamera_erstellen;
        string file = AppResources.titelbild_von_der_festplatte_hochladen;
        string remove = ProjectItem.Current.HasTitleImage ? AppResources.titelbild_entfernen : null;

        string choice = await DisplayActionSheetAsync(
            AppResources.titelbild, AppResources.abbrechen, remove, camera, file);

        if (choice == camera)
            await CaptureTitleImageAsync();
        else if (choice == file)
            await PickTitleImageAsync();
        else if (remove != null && choice == remove)
            RemoveTitleImage();
    }

    /// <summary>Wird auch per TitleCaptureRequestedMessage aufgerufen.</summary>
    public async void OnTitleCaptureClicked(object sender, EventArgs e)
        => await CaptureTitleImageAsync();

    private async Task CaptureTitleImageAsync()
    {
        try
        {
            // 1. Alten Dateinamen vor der Kameraaufnahme sichern
            string oldTitleImage = GlobalJson.Data.TitleImage;
            string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";

            (FileResult result, Size imgSize) = await CapturePicture.Capture(
                Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath),
                Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath),
                thumbFileName, true, true);

            if (result == null) return;

            // 2. Alte Dateien lokal UND aus der Cloud loeschen
            DeleteTitleImageFiles(oldTitleImage);

            // 3. JSON aktualisieren
            ProjectItem.Current.TitleImage = thumbFileName;
            GlobalJson.Data.TitleImageSize = imgSize;
            GlobalJson.SaveToFile();

            // 4. JSON-Aenderungen UND die 2 neuen Bilddateien an SaveManager uebergeben
            var (imagePath, thumbPath) = GetTitleImagePaths(thumbFileName);
            SaveManager.NotifyDataChanged([
                (imagePath, GlobalJson.Data.ImagePath),
                (thumbPath, GlobalJson.Data.ThumbnailPath)
            ]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler bei der Titelbild-Aufnahme: {ex.Message}");
        }
    }

    private async Task PickTitleImageAsync()
    {
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.bitte_waehle_bild,
                FileTypes = FilePickerFileType.Jpeg
            });

            if (fileResult == null) return;

            // 1. Alten Dateinamen vor dem Ueberschreiben merken
            string oldTitleImage = GlobalJson.Data.TitleImage;
            string thumbFileName = $"title_{DateTime.Now.Ticks}.jpg";
            string sourceFilePath = fileResult.FullPath;

            var (destinationPath, destinationThumbPath) = GetTitleImagePaths(thumbFileName);

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            if (File.Exists(destinationThumbPath)) File.Delete(destinationThumbPath);

            using (FileStream sourceStream = new(sourceFilePath, FileMode.Open, FileAccess.Read))
            using (FileStream destinationStream = new(destinationPath, FileMode.Create))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            await Thumbnail.Generate(sourceFilePath, destinationThumbPath);

            // 2. Alte Dateien lokal UND in der Cloud loeschen
            DeleteTitleImageFiles(oldTitleImage);

            // 3. JSON aktualisieren
            ProjectItem.Current.TitleImage = thumbFileName;

            // Codec in einem using-Block kapseln, um Memory Leaks zu verhindern
            using (var codec = SKCodec.Create(sourceFilePath))
            {
                GlobalJson.Data.TitleImageSize = codec != null
                    ? new Size(codec.Info.Size.Width, codec.Info.Size.Height)
                    : new Size(500, 500);
            }

            GlobalJson.SaveToFile();

            // 4. JSON UND die zwei neuen Bilddateien fuer den Upload registrieren
            SaveManager.NotifyDataChanged([
                (destinationPath, GlobalJson.Data.ImagePath),         // Originalbild im Images-Ordner
                (destinationThumbPath, GlobalJson.Data.ThumbnailPath) // Thumbnail im Thumbnails-Ordner
            ]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Auswaehlen der Datei: {ex.Message}");
        }
    }

    /// <summary>
    /// Loescht das eigene Titelbild und setzt auf den Standardwert zurueck
    /// (HasTitleImage = false -> Platzhalter banner.jpg wird angezeigt).
    /// </summary>
    private void RemoveTitleImage()
    {
        try
        {
            DeleteTitleImageFiles(GlobalJson.Data.TitleImage);

            ProjectItem.Current.TitleImage = DefaultTitleImage;
            GlobalJson.Data.TitleImageSize = new Size(0, 0);
            GlobalJson.SaveToFile();

            SaveManager.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Entfernen des Titelbilds: {ex.Message}");
        }
    }

    private static (string ImagePath, string ThumbPath) GetTitleImagePaths(string fileName)
    {
        string projectDir = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath);
        return (
            Path.Combine(projectDir, GlobalJson.Data.ImagePath, fileName),
            Path.Combine(projectDir, GlobalJson.Data.ThumbnailPath, fileName));
    }

    /// <summary>
    /// Loescht Original und Thumbnail lokal und in der Cloud.
    /// Der Standardwert wird nie geloescht.
    /// </summary>
    private static void DeleteTitleImageFiles(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName == DefaultTitleImage) return;

        var (imagePath, thumbPath) = GetTitleImagePaths(fileName);
        if (File.Exists(thumbPath)) File.Delete(thumbPath);
        if (File.Exists(imagePath)) File.Delete(imagePath);

        _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ThumbnailPath}/{fileName}");
        _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ImagePath}/{fileName}");
    }

    // ------------------------------------------------------------------
    // Weitere Aktionen
    // ------------------------------------------------------------------

    private async void OnAddPdfClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("loadPdfImages");
    }

    private async void OnAddWebMapClicked(object sender, EventArgs e)
    {
        var popup = new PopupEntry(header: AppResources.karte_aus_webmap,
                                   desc: AppResources.online_map_requirement_hint + ".",
                                   title: AppResources.plan_name,
                                   okText: AppResources.erstellen);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result?.Result == null) return;

        string planId = "webmap_" + SyncClock.NewId();
        Plan plan = new()
        {
            Name = result.Result == "" ? "Online Map" : result.Result,
            File = "",
            ImageSize = new Size(0, 0),
            IsGrayscale = false,
            Description = "",
            AllowExport = true,
            PlanColor = "#00FFFFFF"
        };
        plan.Touch();

        var newPlan = new KeyValuePair<string, Plan>(planId, plan);
        LoadDataToView.AddPlan(newPlan);

        // Ueberpruefen, ob die Plans-Struktur initialisiert ist
        GlobalJson.Data.Plans ??= [];
        GlobalJson.Data.Plans[planId] = plan;

        SaveManager.NotifyDataChanged();

        ProjectItem.Current.ApplyFilterAndSorting();
        await Shell.Current.GoToAsync($"//{planId}");
    }

    private async void CalendarClicked(object sender, EventArgs e)
    {
        var popup = new PopupCalendarView(ProjectItem.Current.CreationDate);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (string.IsNullOrEmpty(result?.Result)) return;

        if (DateTime.TryParseExact(result.Result, "dd.MM.yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var picked))
            ProjectItem.Current.CreationDate = picked;
    }
}
