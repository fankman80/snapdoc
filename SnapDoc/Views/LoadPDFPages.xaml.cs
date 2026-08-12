#nullable disable
using SkiaSharp;
using SnapDoc.Controls;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;

namespace SnapDoc.Views;

public partial class LoadPDFPages : ContentPage
{
    IEnumerable<FileResult> resultList;
    private bool _isProcessing = false;
    public int DynamicSpan { get; set; } = 0;

    public LoadPDFPages()
    {
        InitializeComponent();
        BindingContext = new BaseViewModel();
        btnRows.Text = Settings.TableRowIcon;
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
        if (_isProcessing) return;
        _isProcessing = true;

        var viewModel = BindingContext as BaseViewModel;

        try
        {
            // Cache leeren
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

            // Ladeanzeige aktivieren
            if (viewModel != null)
            {
                viewModel.BusyText = AppResources.lade_pdf_seiten;
                viewModel.IsBusy = true;
                await Task.Delay(100); // Gibt dem UI-Thread Zeit, das Overlay anzuzeigen
            }

            string importId = DateTime.Now.ToString("yyyyMMddHHmmss");
            List<PdfItem> pdfImages = [];

            // Die zeitintensive PDF-Verarbeitung komplett im Hintergrund ausfuehren
            await Task.Run(async () =>
            {
                Directory.CreateDirectory(Settings.CacheDirectory);

                int pdfIndex = 0;
                foreach (var file in resultList)
                {
                    string localPdfPath = Path.Combine(Settings.CacheDirectory, $"input_{pdfIndex}.pdf");

                    using (var sourceStream = await file.OpenReadAsync())
                    using (var destStream = File.Create(localPdfPath))
                    {
                        await sourceStream.CopyToAsync(destStream);
                    }

                    byte[] pdfBytes = await File.ReadAllBytesAsync(localPdfPath);
                    using var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes);

                    for (int i = 0; i < nativeDoc.PageCount; i++)
                    {
                        string imgBaseName = $"pdf_{importId}_{pdfIndex}_page_{i}";
                        string previewPath = Path.Combine(Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");
                        string imgPath = Path.Combine(Settings.CacheDirectory, imgBaseName + ".jpg");
                        var (width, height) = await NativePdfRenderer.SavePageAsync(nativeDoc, previewPath, i, SettingsService.Instance.PdfThumbDpi);
                        int targetDpi = SettingsService.Instance.PdfFullViewDpi;

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
                    pdfIndex++;
                }
            });

            // UI-Aktualisierung nach Abschluss des Hintergrund-Tasks
            fileListView.ItemsSource = pdfImages;
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"{AppResources.fehler}: {ex.Message}", includeDelay: true);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            viewModel?.IsBusy = false;

            _isProcessing = false;
        }
    }

    private static int CalculateMaxDpiFromMaxDimension(int currentWidth, int currentHeight, int maxDimension)
    {
        int maxEdge = Math.Max(currentWidth, currentHeight);

        if (maxEdge <= 0)
            return SettingsService.Instance.PdfThumbDpi;

        double scaleFactor = (double)maxDimension / maxEdge;
        int currentDpi = SettingsService.Instance.PdfThumbDpi;
        int targetDpi = (int)Math.Floor(currentDpi * scaleFactor);

        return Math.Max(1, targetDpi);
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
        if (_isProcessing) return;
        _isProcessing = true;

        var viewModel = BindingContext as BaseViewModel;

        try
        {
            // Ladeanzeige aktivieren
            if (viewModel != null)
            {
                viewModel.BusyText = AppResources.pdf_wird_konvertiert;
                viewModel.IsBusy = true;
                await Task.Delay(100); // Gibt dem UI-Thread Zeit, das Overlay anzuzeigen
            }

            await LoadPDFImages();
            await ProcessFileOrganizationLogic();

            // Daten speichern
            GlobalJson.SaveToFile();

            if (Shell.Current is AppShell shell)
                shell.ApplyFilterAndSorting();

            await Shell.Current.GoToAsync("project_details");
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"PDF-Error: {ex.Message}", includeDelay: true);
        }
        finally
        {
            // Ladeanzeige deaktivieren
            viewModel?.IsBusy = false;

            _isProcessing = false;
        }
    }

    private async Task LoadPDFImages()
    {
        var groups = fileListView.ItemsSource.Cast<PdfItem>()
                        .Where(x => x.IsChecked)
                        .GroupBy(x => x.PdfPath)
                        .ToList();

        await Parallel.ForEachAsync(groups, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (group, cancellationToken) =>
        {
            byte[] pdfBytes = await File.ReadAllBytesAsync(group.Key, cancellationToken);
            using var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes);

            foreach (var item in group)
            {
                string imgPath = Path.Combine(Settings.DataDirectory, Settings.CacheDirectory, item.ImageName + ".jpg");
                var (width, height) = await NativePdfRenderer.SavePageAsync(nativeDoc, imgPath, item.PdfPage, item.Dpi);
                item.FinalWidth = width;
                item.FinalHeight = height;
            }
        });
    }

    private async Task ProcessFileOrganizationLogic()
    {
        await Task.Run(() =>
        {
            string imageDirectory = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath);
            Directory.CreateDirectory(Path.Combine(imageDirectory, "thumbnails"));

            var items = fileListView.ItemsSource.Cast<PdfItem>().Where(x => x.IsChecked).ToList();
            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Array vorab in der exakten Grosse erstellen, um die Reihenfolge zu garantieren
            var processedPlans = new KeyValuePair<string, Plan>[items.Count];

            Parallel.For(0, items.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var item = items[i];
                string fileName = $"plan_{timeStamp}_{i}.jpg";
                string planId = $"plan_{timeStamp}_{i}";
                string destinationFilePath = Path.Combine(imageDirectory, fileName);
                string destinationThumbPath = Path.Combine(imageDirectory, "thumbnails", fileName);

                Size _imgSize = new(item.FinalWidth, item.FinalHeight);

                Plan plan = new()
                {
                    Name = item.DisplayName,
                    File = fileName,
                    ImageSize = _imgSize,
                    IsGrayscale = false,
                    AllowExport = true,
                    PlanColor = "#00FFFFFF"
                };

                try
                {
                    using var inputStream = File.OpenRead(item.PreviewPath);
                    using var originalBitmap = SKBitmap.Decode(inputStream);
                    int maxThumbSize = SettingsService.Instance.PlanThumbSize;
                    int targetWidth = originalBitmap.Width;
                    int targetHeight = originalBitmap.Height;

                    if (targetWidth > maxThumbSize || targetHeight > maxThumbSize)
                    {
                        if (targetWidth > targetHeight)
                        {
                            targetHeight = (int)(targetHeight * ((double)maxThumbSize / targetWidth));
                            targetWidth = maxThumbSize;
                        }
                        else
                        {
                            targetWidth = (int)(targetWidth * ((double)maxThumbSize / targetHeight));
                            targetHeight = maxThumbSize;
                        }
                    }

                    // SKSamplingOptions statt SKFilterQuality verwenden
                    using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(targetWidth, targetHeight), SKSamplingOptions.Default);
                    if (resizedBitmap != null)
                    {
                        using var image = SKImage.FromBitmap(resizedBitmap);
                        using var thumbData = image.Encode(SKEncodedImageFormat.Jpeg, 75);
                        using var thumbStream = File.OpenWrite(destinationThumbPath);
                        thumbData.SaveTo(thumbStream);
                    }
                    else
                    {
                        File.Copy(item.PreviewPath, destinationThumbPath, overwrite: true);
                    }
                }
                catch
                {
                    File.Copy(item.PreviewPath, destinationThumbPath, overwrite: true);
                }

                // Hauptbild in voller Auflösung kopieren
                File.Copy(item.ImagePath, destinationFilePath, overwrite: true);

                // Ergebnis exakt an der vorgegebenen Position im Array ablegen
                processedPlans[i] = new KeyValuePair<string, Plan>(planId, plan);
            });

            // JSON sequentiell befuellen
            lock (GlobalJson.Data)
            {
                GlobalJson.Data.Plans ??= [];
                foreach (var planKvp in processedPlans)
                {
                    if (planKvp.Value != null)
                        GlobalJson.Data.Plans[Path.GetFileNameWithoutExtension(planKvp.Value.File)] = planKvp.Value;
                }
            }

            // UI sequentiell updaten
            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var planKvp in processedPlans)
                {
                    if (planKvp.Value != null)
                        LoadDataToView.AddPlan(planKvp);
                }
            });

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