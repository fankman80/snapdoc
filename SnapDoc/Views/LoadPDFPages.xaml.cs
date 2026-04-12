#nullable disable
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;
public partial class LoadPDFPages : ContentPage
{
    IEnumerable<FileResult> resultList;
    public int DynamicSpan { get; set; } = 0;

    public LoadPDFPages()
    {
        InitializeComponent();

        btnRows.Text = Settings.TableRowIcon;
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
        if (Directory.Exists(Settings.CacheDirectory))
        {
            foreach (var file in Directory.GetFiles(Settings.CacheDirectory))
            {
                try { File.Delete(file); } catch { }
            }
        }

        resultList = await PickPdfFileAsync();
        if (resultList == null || !resultList.Any())
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        string importId = DateTime.Now.ToString("yyyyMMddHHmmss");
        List<PdfItem> pdfImages = [];

        busyOverlay.BusyMessage = AppResources.lade_pdf_seiten;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.InputTransparent = false;

        try
        {
            int pdfIndex = 0;
            foreach (var file in resultList)
            {
                string localPdfPath = Path.Combine(Settings.CacheDirectory, $"input_{pdfIndex}.pdf");
                using (var sourceStream = await file.OpenReadAsync())
                {
                    await Task.Run(async () =>
                    {
                        if (!Directory.Exists(Settings.CacheDirectory))
                            Directory.CreateDirectory(Settings.CacheDirectory);

                        using (var destStream = File.Create(localPdfPath))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        byte[] pdfBytes = await File.ReadAllBytesAsync(localPdfPath);
                        using (var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes))
                        {
                            for (int i = 0; i < nativeDoc.PageCount; i++)
                            {
                                string imgBaseName = $"pdf_{importId}_{pdfIndex}_page_{i}";
                                string previewPath = Path.Combine(Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");
                                string imgPath = Path.Combine(Settings.CacheDirectory, imgBaseName + ".jpg");

                                await NativePdfRenderer.SavePageAsync(nativeDoc, previewPath, i, SettingsService.Instance.PdfThumbDpi);

                                int width = 0, height = 0;
                                using (var stream = File.OpenRead(previewPath))
                                using (var codec = SKCodec.Create(stream))
                                {
                                    if (codec != null) { width = codec.Info.Width; height = codec.Info.Height; }
                                }

                                int targetDpi = CalculateMaxDpiFromPixelLimit(width, height, SettingsService.Instance.MaxPdfPixelCount * 1000000);

                                pdfImages.Add(new PdfItem
                                {
                                    ImagePath = imgPath,
                                    PreviewPath = previewPath,
                                    PdfPath = localPdfPath,
                                    IsChecked = true,
                                    Dpi = targetDpi,
                                    DisplayName = $"Plan {pdfIndex + 1} – Seite {i + 1}",
                                    ImageName = imgBaseName,
                                    PdfPage = i,
                                });
                            }
                        }
                    });
                }
                pdfIndex++;
            }
            fileListView.ItemsSource = pdfImages;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fehler", ex.Message, "OK");
        }
        finally
        {
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
            busyOverlay.InputTransparent = true;
        }
    }

    private static int CalculateMaxDpiFromPixelLimit(int width72dpi, int height72dpi, int maxPixelCount)
    {
        if (width72dpi <= 0 || height72dpi <= 0)
            throw new ArgumentException("PDF-Seitenbreite und -höhe müssen > 0 sein.");

        double dpi = 72 * Math.Sqrt((double)maxPixelCount / (width72dpi * height72dpi));
        return Math.Max(10, (int)Math.Floor(dpi)); // Mindestwert 10 dpi zur Sicherheit    
    }

    public static async Task<IEnumerable<FileResult>> PickPdfFileAsync()
    {
        try
        {
            var fileResult = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = AppResources.pdf_dateien_auswaehlen,
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
        busyOverlay.BusyMessage = AppResources.pdf_wird_konvertiert;
        busyOverlay.IsActivityRunning = true;
        busyOverlay.IsOverlayVisible = true;
        busyOverlay.InputTransparent = false;

        try
        {
            await LoadPDFImages();

            MainThread.BeginInvokeOnMainThread(() =>
                busyOverlay.BusyMessage = AppResources.pdf_wird_konvertiert);

            await ProcessFileOrganizationLogic();

            GlobalJson.SaveToFile();

            if (Application.Current.Windows[0].Page is AppShell shell)
                shell.ApplyFilterAndSorting();

            await Shell.Current.GoToAsync("project_details");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("PDF-Error", ex.Message, "OK");
        }
        finally
        {
            busyOverlay.IsActivityRunning = false;
            busyOverlay.IsOverlayVisible = false;
            busyOverlay.InputTransparent = true;
        }
    }

    private async Task LoadPDFImages()
    {
        await Task.Run(async () =>
        {
            var groups = fileListView.ItemsSource.Cast<PdfItem>()
                            .Where(x => x.IsChecked)
                            .GroupBy(x => x.PdfPath);

            foreach (var group in groups)
            {
                // group.Key ist jetzt unser sicherer lokaler Pfad im Cache
                byte[] pdfBytes = await File.ReadAllBytesAsync(group.Key);
                using (var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes))
                {
                    foreach (var item in group)
                    {
                        string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, item.ImageName + ".jpg");
                        await NativePdfRenderer.SavePageAsync(nativeDoc, imgPath, item.PdfPage, item.Dpi);
                    }
                }
                pdfBytes = null;
            }
        });
    }

    private async Task ProcessFileOrganizationLogic()
    {
        await Task.Run(() =>
        {
            string imageDirectory = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath);
            if (!Directory.Exists(Path.Combine(imageDirectory, "thumbnails")))
                Directory.CreateDirectory(Path.Combine(imageDirectory, "thumbnails"));

            int i = 0;
            var items = fileListView.ItemsSource.Cast<PdfItem>().Where(x => x.IsChecked).ToList();

            foreach (var item in items)
            {
                string fileName = $"plan_{DateTime.Now:yyyyMMdd_HHmmss}_{i}.jpg";
                string planId = $"plan_{DateTime.Now:yyyyMMdd_HHmmss}_{i}";
                string destinationFilePath = Path.Combine(imageDirectory, fileName);
                string destinationThumbPath = Path.Combine(imageDirectory, "thumbnails", fileName);

                // Bildgröße ermitteln (Schonend!)
                Size _imgSize;
                using (var stream = File.OpenRead(item.ImagePath))
                using (var codec = SkiaSharp.SKCodec.Create(stream))
                {
                    _imgSize = new Size(codec.Info.Width, codec.Info.Height);
                }

                Plan plan = new()
                {
                    Name = item.DisplayName,
                    File = fileName,
                    ImageSize = _imgSize,
                    IsGrayscale = false,
                    AllowExport = true,
                    PlanColor = "#00FFFFFF"
                };

                lock (GlobalJson.Data)
                {
                    GlobalJson.Data.Plans ??= [];
                    GlobalJson.Data.Plans[Path.GetFileNameWithoutExtension(fileName)] = plan;
                }

                // Kopieren der generierten Dateien vom Cache zum Projektordner
                File.Copy(item.ImagePath, destinationFilePath, overwrite: true);
                File.Copy(item.PreviewPath, destinationThumbPath, overwrite: true);

                i++;

                MainThread.BeginInvokeOnMainThread(() => {
                    LoadDataToView.AddPlan(new KeyValuePair<string, Plan>(planId, plan));
                });
            }

            // Am Ende den gesamten Cache leeren
            if (Directory.Exists(Settings.CacheDirectory))
            {
                var cacheFiles = Directory.GetFiles(Settings.CacheDirectory);
                foreach (var cacheFile in cacheFiles)
                {
                    try { File.Delete(cacheFile); } catch { }
                }
            }
        });
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        if (DynamicSpan == 1)
        {
            DynamicSpan = 0;
            btnRows.Text = Settings.TableRowIcon;
        }
        else
        {
            DynamicSpan = 1;
            btnRows.Text = Settings.TableGridIcon;
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
            DynamicSpan = Math.Max(SettingsService.Instance.GridViewMinColumns, (int)(screenWidth / imageWidth));
        }

        OnPropertyChanged(nameof(DynamicSpan));
    }
}
