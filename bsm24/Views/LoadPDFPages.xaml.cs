#nullable disable

using bsm24.Models;
using bsm24.Services;
using PDFtoImage;
using SkiaSharp;
using UraniumUI.Pages;

namespace bsm24.Views;
public partial class LoadPDFPages : UraniumContentPage
{
    FileResult result;
    public int DynamicSpan { get; set; } = 2; // Standardwert
    public int MinSize = 2;

    public LoadPDFPages()
    {
        InitializeComponent();
        UpdateSpan();
        SizeChanged += OnSizeChanged;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadPreviewPDFImages();
    }
    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    private async void LoadPreviewPDFImages()
    {
        result = await PickPdfFileAsync();
        if (result != null)
        {
            List<ImageItem> pdfImages = [];
            busyOverlay.IsOverlayVisible = true;
            busyOverlay.IsActivityRunning = true;
            busyOverlay.BusyMessage = "Lade PDF Seiten...";

            await Task.Run(() =>
            {
                byte[] bytearray = File.ReadAllBytes(result.FullPath);
                int pagecount = Conversion.GetPageCount(bytearray);

                if (!Directory.Exists(Settings.CacheDirectory))
                    Directory.CreateDirectory(Settings.CacheDirectory);

                for (int i = 0; i < pagecount; i++)
                {
                    string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, "plan_" + i + ".jpg");
                    string previewPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, "preview_" + i + ".jpg");
                    var renderOptions = new RenderOptions()
                    {
                        AntiAliasing = PdfAntiAliasing.None,
                        Dpi = 72,
                        WithAnnotations = false,
                        WithFormFill = false,
                    };
                    Conversion.SaveJpeg(previewPath, bytearray, i, options: renderOptions);

                    var stream = File.OpenRead(previewPath);
                    var skBitmap = SKBitmap.Decode(stream);
                    Size _imgSize = new(skBitmap.Width, skBitmap.Height);

                    pdfImages.Add(new ImageItem
                    {
                        ImagePath = imgPath,
                        PreviewPath = previewPath,
                        IsChecked = true,
                    });
                }
            });

            fileListView.ItemsSource = pdfImages;
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
        }
        else
            await Shell.Current.GoToAsync("..");
    }

    private async Task LoadPDFImages()
    {
        List<ImageItem> pdfImages = [];
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "PDF wird konvertiert...";

        await Task.Run(() =>
        {
            byte[] bytearray = File.ReadAllBytes(result.FullPath);
            int pagecount = Conversion.GetPageCount(bytearray);

            if (!Directory.Exists(Settings.CacheDirectory))
                Directory.CreateDirectory(Settings.CacheDirectory);

            for (int i = 0; i < pagecount; i++)
            {
                string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, "plan_" + i + ".jpg");
                string previewImgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, "preview_" + i + ".jpg");
                var renderOptions = new RenderOptions()
                {
                    AntiAliasing = PdfAntiAliasing.All,
                    Dpi = SettingsService.Instance.PdfQuality,
                    WithAnnotations = true,
                    WithFormFill = true,
                    UseTiling = true,
                };
                Conversion.SaveJpeg(imgPath, bytearray, i, options: renderOptions);

                var stream = File.OpenRead(imgPath);
                var skBitmap = SKBitmap.Decode(stream);
                Size _imgSize = new(skBitmap.Width, skBitmap.Height);
                if (File.Exists(previewImgPath))
                    File.Delete(previewImgPath);
            }
        });

        busyOverlay.IsActivityRunning = false;
        busyOverlay.IsOverlayVisible = false;
    }

    public static async Task<FileResult> PickPdfFileAsync()
    {
        try
        {
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
            Console.WriteLine($"Fehler beim Auswählen der Datei: {ex.Message}");
        }
        return null; // Kein PDF ausgewählt
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        var cacheFiles = Directory.GetFiles(Settings.CacheDirectory);
        foreach (var cacheFile in cacheFiles)
        {
            File.Delete(cacheFile);
        }
        Shell.Current.GoToAsync("..");
    }

    private void OnPagesAddClicked(object sender, EventArgs e)
    {
        AddPdfImages();
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        if (MinSize == 1)
        {
            MinSize = 2;
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Grid_on,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]
            };
        }
        else
        {
            MinSize = 1;
            DynamicSpan = 1;
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Table_rows,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]
            };
        }
        UpdateSpan();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private async void AddPdfImages()
    {
        await LoadPDFImages(); //generiere High-Res Images
        
        string imageDirectory = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath);
        int i = 0;

        // Überprüfen, ob Plans null ist, und es gegebenenfalls initialisieren
        GlobalJson.Data.Plans ??= [];  // Initialisiere Plans, wenn es null ist

        foreach (var item in fileListView.ItemsSource.Cast<ImageItem>())
        {
            if (item.IsChecked)
            {
                string sourceFilePath = item.ImagePath;
                string fileName = "plan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + i + ".jpg";
                string destinationFilePath = Path.Combine(imageDirectory, fileName);
                string planSourceName = "plan_" + i + ".jpg";

                var stream = File.OpenRead(Path.Combine(Settings.CacheDirectory, planSourceName));
                var skBitmap = SKBitmap.Decode(stream);
                Size _imgSize = new(skBitmap.Width, skBitmap.Height);

                // Schleife, bis ein einzigartiger Name gefunden wird
                string planName;
                int j = 0;
                do
                {
                    planName = "Plan " + j;
                    j++;
                }
                while (GlobalJson.Data.Plans.Values.Any(p => p.Name == planName));

                Plan plan = new()
                {
                    Name = planName,
                    File = fileName,
                    ImageSize = _imgSize,
                    IsGrayscale = false,
                    Description = "",
                };

                // Überprüfen, ob die Plans-Struktur initialisiert ist
                GlobalJson.Data.Plans ??= [];
                GlobalJson.Data.Plans[Path.GetFileNameWithoutExtension(fileName)] = plan;

                File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
                i += 1;
            }
        }

        GlobalJson.Data.PlanPdf = new Pdf
        {
            File = result.FileName,
        };

        GlobalJson.SaveToFile();

        var cacheFiles = Directory.GetFiles(Settings.CacheDirectory);
        foreach (var cacheFile in cacheFiles)
        {
            File.Delete(cacheFile);
        }
        await Shell.Current.GoToAsync("..");
    }

    private void UpdateSpan()
    {
        if (MinSize != 1)
        {
            double screenWidth = this.Width;
            double imageWidth = Settings.PlanPreviewSize; // Mindestbreite in Pixeln
            DynamicSpan = Math.Max(MinSize, (int)(screenWidth / imageWidth));
        }
        OnPropertyChanged(nameof(DynamicSpan));
    }
}
