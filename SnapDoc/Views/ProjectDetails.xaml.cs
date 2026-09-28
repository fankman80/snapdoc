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

    private const uint MenuAnimationMs = 150;
    private const double MenuSlideOffset = 24;

    private bool _isTitleMenuOpen;

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
        CloseTitleMenuImmediately();
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
    // Titelbild-Menue
    // ------------------------------------------------------------------

    private async void OnTitleEditClicked(object sender, EventArgs e)
        => await SetTitleMenuAsync(!_isTitleMenuOpen);

    private async void OnTitleMenuScrimTapped(object sender, TappedEventArgs e)
        => await SetTitleMenuAsync(false);

    private async void OnTitleCameraTapped(object sender, TappedEventArgs e)
    {
        await SetTitleMenuAsync(false);
        await CaptureTitleImageAsync();
    }

    private async void OnTitleFileTapped(object sender, TappedEventArgs e)
    {
        await SetTitleMenuAsync(false);
        await PickTitleImageAsync();
    }

    private async void OnTitleRemoveTapped(object sender, TappedEventArgs e)
    {
        await SetTitleMenuAsync(false);

        bool confirmed = await DisplayAlertAsync(
            AppResources.titelbild_entfernen + "?", string.Empty,
            AppResources.ok, AppResources.abbrechen);

        if (confirmed)
            RemoveTitleImage();
    }

    /// <summary>
    /// Klappt die Aktionen animiert auf/zu. Der Stift wird dabei zum X.
    /// Der Papierkorb (oben links) erscheint nur bei eigenem Bild.
    /// </summary>
    private async Task SetTitleMenuAsync(bool open)
    {
        if (_isTitleMenuOpen == open) return;
        _isTitleMenuOpen = open;

        titleEditButton.Text = open ? MaterialIcons.Close : MaterialIcons.Edit;

        titleMenu.CancelAnimations();
        titleMenuScrim.CancelAnimations();
        titleRemoveChip.CancelAnimations();

        bool showRemove = ProjectItem.Current.HasTitleImage;

        if (open)
        {
            titleMenu.TranslationX = MenuSlideOffset;
            titleMenu.Opacity = 0;
            titleMenu.IsVisible = true;
            titleMenuScrim.Opacity = 0;
            titleMenuScrim.IsVisible = true;
            titleRemoveChip.Opacity = 0;
            titleRemoveChip.IsVisible = showRemove;

            await Task.WhenAll(
                titleMenu.FadeToAsync(1, MenuAnimationMs),
                titleMenu.TranslateToAsync(0, 0, MenuAnimationMs, Easing.CubicOut),
                titleMenuScrim.FadeToAsync(1, MenuAnimationMs),
                showRemove ? titleRemoveChip.FadeToAsync(1, MenuAnimationMs) : Task.CompletedTask);
        }
        else
        {
            await Task.WhenAll(
                titleMenu.FadeToAsync(0, MenuAnimationMs),
                titleMenu.TranslateToAsync(MenuSlideOffset, 0, MenuAnimationMs, Easing.CubicIn),
                titleMenuScrim.FadeToAsync(0, MenuAnimationMs),
                titleRemoveChip.FadeToAsync(0, MenuAnimationMs));

            // Wurde waehrend der Animation wieder geoeffnet, nicht ausblenden.
            if (!_isTitleMenuOpen)
            {
                titleMenu.IsVisible = false;
                titleMenuScrim.IsVisible = false;
                titleRemoveChip.IsVisible = false;
            }
        }
    }

    private void CloseTitleMenuImmediately()
    {
        _isTitleMenuOpen = false;
        titleMenu.CancelAnimations();
        titleMenuScrim.CancelAnimations();
        titleRemoveChip.CancelAnimations();
        titleMenu.IsVisible = false;
        titleMenu.Opacity = 0;
        titleMenuScrim.IsVisible = false;
        titleMenuScrim.Opacity = 0;
        titleRemoveChip.IsVisible = false;
        titleRemoveChip.Opacity = 0;
        titleEditButton.Text = MaterialIcons.Edit;
    }

    // ------------------------------------------------------------------
    // Titelbild
    // ------------------------------------------------------------------

    /// <summary>
    /// Offenes Menue -> schliessen. Eigenes Bild -> Vollansicht. Platzhalter -> Menue oeffnen.
    /// </summary>
    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (_isTitleMenuOpen)
        {
            await SetTitleMenuAsync(false);
            return;
        }

        if (!ProjectItem.Current.HasTitleImage)
        {
            await SetTitleMenuAsync(true);
            return;
        }

        await Shell.Current.GoToAsync($"imageview?imgSource=showTitle&gotoBtn=false");
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
