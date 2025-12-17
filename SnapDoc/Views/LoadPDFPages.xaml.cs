#nullable disable

using Microsoft.Maui.Controls;
using PDFtoImage;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Services;

namespace SnapDoc.Views;
public partial class LoadPDFPages : ContentPage
{
    IEnumerable<FileResult> resultList;
    public int DynamicSpan { get; set; } = 0; // Standardwert

    public LoadPDFPages()
    {
        InitializeComponent();

        btnRows.Text = Settings.TableGridIcon;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SizeChanged += OnSizeChanged;
        LoadPreviewPDFImages();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SizeChanged -= OnSizeChanged;
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    private async void LoadPreviewPDFImages()
    {
        resultList = await PickPdfFileAsync();
        if (resultList != null && resultList.Any())
        {
            List<PdfItem> pdfImages = [];
            busyOverlay.IsOverlayVisible = true;
            busyOverlay.IsActivityRunning = true;
            busyOverlay.BusyMessage = "Lade PDF Seiten...";

            await Task.Run(() =>
            {
                if (!Directory.Exists(Settings.CacheDirectory))
                    Directory.CreateDirectory(Settings.CacheDirectory);

                int pdfIndex = 0;

                foreach (var file in resultList)
                {
                    byte[] bytearray = File.ReadAllBytes(file.FullPath);
                    int pagecount = Conversion.GetPageCount(bytearray);

                    for (int i = 0; i < pagecount; i++)
                    {
                        string imgBaseName = $"pdf_{pdfIndex}_page_{i}";
                        string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, imgBaseName + ".jpg");
                        string previewPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");

                        // Schritt 1: Seite bei 72 DPI rendern, um Größe zu ermitteln
                        var probeRenderOptions = new RenderOptions
                        {
                            AntiAliasing = PdfAntiAliasing.None,
                            Dpi = SettingsService.Instance.PdfThumbDpi,
                            WithAnnotations = false,
                            WithFormFill = false,
                        };
                        Conversion.SaveJpeg(previewPath, bytearray, i, options: probeRenderOptions);

                        using var probeStream = File.OpenRead(previewPath);
                        using var probeBitmap = SKBitmap.Decode(probeStream);

                        int width72dpi = probeBitmap.Width;
                        int height72dpi = probeBitmap.Height;

                        // Schritt 2: DPI berechnen anhand MaxPixelCount
                        int targetDpi = CalculateMaxDpiFromPixelLimit(width72dpi, height72dpi, SettingsService.Instance.MaxPdfPixelCount * 1000000);

                        pdfImages.Add(new PdfItem
                        {
                            ImagePath = imgPath,
                            PreviewPath = previewPath,
                            PdfPath = file.FullPath,
                            IsChecked = true,
                            Dpi = targetDpi,
                            DisplayName = $"Plan {pdfIndex + 1} – Seite {i + 1}",
                            ImageName = imgBaseName,
                            PdfPage = i,
                        });
                    }
                    pdfIndex++;
                }
            });

            fileListView.ItemsSource = pdfImages;
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private static int CalculateMaxDpiFromPixelLimit(int width72dpi, int height72dpi, int maxPixelCount)
    {
        if (width72dpi <= 0 || height72dpi <= 0)
            throw new ArgumentException("PDF-Seitenbreite und -höhe müssen > 0 sein.");

        double dpi = 72 * Math.Sqrt((double)maxPixelCount / (width72dpi * height72dpi));
        return Math.Max(10, (int)Math.Floor(dpi)); // Mindestwert 10 dpi zur Sicherheit
    }

    private async Task LoadPDFImages()
    {
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.BusyMessage = "PDF wird konvertiert...";

        if (!Directory.Exists(Settings.CacheDirectory))
            Directory.CreateDirectory(Settings.CacheDirectory);

        await Task.Run(() =>
        {
            foreach (var item in fileListView.ItemsSource.Cast<PdfItem>())
            {
                byte[] bytearray = File.ReadAllBytes(item.PdfPath);
                string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, item.ImageName + ".jpg");

                var renderOptions = new RenderOptions()
                {
                    AntiAliasing = PdfAntiAliasing.All,
                    Dpi = item.Dpi,
                    WithAnnotations = true,
                    WithFormFill = true,
                    UseTiling = true,
                };
                Conversion.SaveJpeg(imgPath, bytearray, item.PdfPage, options: renderOptions);

                var stream = File.OpenRead(imgPath);
                var skBitmap = SKBitmap.Decode(stream);
                Size _imgSize = new(skBitmap.Width, skBitmap.Height);
            }
        });

        busyOverlay.IsActivityRunning = false;
        busyOverlay.IsOverlayVisible = false;
    }

    public static async Task<IEnumerable<FileResult>> PickPdfFileAsync()
    {
        try
        {
            var fileResult = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Eine oder mehrere PDF-Dateien auswählen",
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

    private async void AddPdfImages()
    {
        await LoadPDFImages(); //generiere High-Res Images

        string imageDirectory = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath);
        int i = 0;

        // Überprüfen, ob Plans null ist, und es gegebenenfalls initialisieren
        GlobalJson.Data.Plans ??= [];  // Initialisiere Plans, wenn es null ist

        foreach (var item in fileListView.ItemsSource.Cast<PdfItem>())
        {
            if (item.IsChecked)
            {
                string sourceFilePath = item.ImagePath;
                string fileName = "plan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + i + ".jpg";
                string planId = "plan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + i;
                string destinationFilePath = Path.Combine(imageDirectory, fileName);
                string destinationThumbPath = Path.Combine(imageDirectory, "thumbnails", fileName);

                if (!Path.Exists(Path.Combine(imageDirectory, "thumbnails")))
                    Directory.CreateDirectory(Path.Combine(imageDirectory, "thumbnails"));

                var stream = File.OpenRead(Path.Combine(Settings.CacheDirectory, item.ImagePath));
                var skBitmap = SKBitmap.Decode(stream);
                Size _imgSize = new(skBitmap.Width, skBitmap.Height);

                Plan plan = new()
                {
                    Name = item.DisplayName,
                    File = fileName,
                    ImageSize = _imgSize,
                    IsGrayscale = false,
                    Description = "",
                    AllowExport = true,
                    PlanColor = "#00FFFFFF"
                };

                // Überprüfen, ob die Plans-Struktur initialisiert ist
                GlobalJson.Data.Plans ??= [];
                GlobalJson.Data.Plans[Path.GetFileNameWithoutExtension(fileName)] = plan;

                // kopiere Plan_Image in das Projektverzeichnis
                File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

                // kopiere Plan-Thumbnail in das Projektverzeichnis
                File.Copy(item.PreviewPath, destinationThumbPath, overwrite: true);

                i += 1;

                // fügt neue Pläne hinzu
                var newPlan = new KeyValuePair<string, Models.Plan>(planId, plan);
                LoadDataToView.AddPlan(newPlan);
            }
            if (File.Exists(item.PreviewPath))
                File.Delete(item.PreviewPath);
        }

        var cacheFiles = Directory.GetFiles(Settings.CacheDirectory);
        foreach (var cacheFile in cacheFiles)
        {
            File.Delete(cacheFile);
        }

        // save data to file
        GlobalJson.SaveToFile();

        // Shell aktualisieren
        var shell = Application.Current.Windows[0].Page as AppShell;
        shell.ApplyFilterAndSorting();

        await Shell.Current.GoToAsync("project_details");
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        if (DynamicSpan == 1)
        {
            DynamicSpan = 0;
            btnRows.Text = Settings.TableGridIcon;
        }
        else
        {
            DynamicSpan = 1;
            btnRows.Text = Settings.TableRowIcon;
        }
        UpdateSpan();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        if (DynamicSpan != 1)
        {
            double screenWidth = this.Width;
            double imageWidth = SettingsService.Instance.PlanPreviewSize; // Mindestbreite in Pixeln
            DynamicSpan = Math.Max(2, (int)(screenWidth / imageWidth));
        }
        OnPropertyChanged(nameof(DynamicSpan));
    }
}
