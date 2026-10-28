#nullable disable
using SkiaSharp;
using SnapDoc.Controls;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class LoadPDFPages : ContentPage
{
    IEnumerable<FileResult> resultList;
    private bool _isProcessing = false;
    private bool _isListView;

    private static readonly FilePickerFileType CustomMediaFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "com.adobe.pdf", "public.jpeg", "public.png" } },
            { DevicePlatform.Android, new[] { "application/pdf", "image/jpeg", "image/png" } },
            { DevicePlatform.WinUI, new[] { ".pdf", ".jpg", ".jpeg", ".png" } },
            { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf", "public.jpeg", "public.png" } }
        });

    public int DynamicSpan { get; set; } = 0;
    public double TileHeight { get; set; } = 200;

    public LoadPDFPages()
    {
        InitializeComponent();
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

    protected override bool OnBackButtonPressed() => true;

    private async void LoadPreviewPDFImages()
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            ClearCache();

            resultList = await PickMediaFileAsync();
            if (resultList == null || !resultList.Any())
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            await BusyService.ShowAsync(AppResources.lade_pdf_seiten);

            string importId = DateTime.Now.ToString("yyyyMMddHHmmss");
            int thumbSize = SettingsService.Instance.PlanThumbSize;
            int fullDpi = SettingsService.Instance.PdfFullViewDpi;
            List<PdfItem> pdfImages = [];

            await Task.Run(async () =>
            {
                Directory.CreateDirectory(Settings.CacheDirectory);

                int fileIndex = 0;
                foreach (var file in resultList)
                {
                    string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                    if (ext == ".pdf")
                    {
                        string localPdfPath = Path.Combine(Settings.CacheDirectory, $"input_{fileIndex}.pdf");
                        using (var sourceStream = await file.OpenReadAsync())
                        using (var destStream = File.Create(localPdfPath))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }

                        byte[] pdfBytes = await File.ReadAllBytesAsync(localPdfPath);
                        using var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes);

                        for (int i = 0; i < nativeDoc.PageCount; i++)
                        {
                            string imgBaseName = $"pdf_{importId}_{fileIndex}_page_{i}";
                            string previewPath = Path.Combine(Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");
                            string imgPath = Path.Combine(Settings.CacheDirectory, imgBaseName + ".jpg");

                            // Vorschau direkt klein rendern (laengste Kante = PlanThumbSize)
                            await NativePdfRenderer.SaveThumbnailAsync(nativeDoc, previewPath, i, thumbSize);

                            pdfImages.Add(new PdfItem
                            {
                                ImagePath = imgPath,
                                PreviewPath = previewPath,
                                PdfPath = localPdfPath,
                                IsChecked = true,
                                Dpi = fullDpi,
                                DisplayName = $"Plan {fileIndex + 1} – Seite {i + 1}",
                                ImageName = imgBaseName,
                                PdfPage = i,
                                // Wird in LoadPDFImages() beim Vollrendern gesetzt
                                FinalWidth = 0,
                                FinalHeight = 0
                            });
                        }
                    }
                    else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        string imgBaseName = $"img_{importId}_{fileIndex}";
                        string previewPath = Path.Combine(Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");
                        string imgPath = Path.Combine(Settings.CacheDirectory, imgBaseName + ".jpg");

                        int imageWidth, imageHeight;

                        if (ext == ".png")
                        {
                            using var sourceStream = await file.OpenReadAsync();
                            using var bitmap = SKBitmap.Decode(sourceStream);
                            imageWidth = bitmap.Width;
                            imageHeight = bitmap.Height;

                            using var image = SKImage.FromBitmap(bitmap);
                            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                            using var destStream = File.Create(imgPath);
                            data.SaveTo(destStream);
                        }
                        else
                        {
                            using (var sourceStream = await file.OpenReadAsync())
                            using (var destStream = File.Create(imgPath))
                            {
                                await sourceStream.CopyToAsync(destStream);
                            }

                            // Nur Header lesen, nicht das ganze Bild dekodieren
                            using var codec = SKCodec.Create(imgPath);
                            imageWidth = codec.Info.Width;
                            imageHeight = codec.Info.Height;
                        }

                        // Vorschau verkleinert erzeugen statt 1:1 kopieren
                        CreateDownscaledPreview(imgPath, previewPath, thumbSize);

                        pdfImages.Add(new PdfItem
                        {
                            ImagePath = imgPath,
                            PreviewPath = previewPath,
                            PdfPath = null,
                            IsChecked = true,
                            Dpi = 0,
                            DisplayName = Path.GetFileNameWithoutExtension(file.FileName),
                            ImageName = imgBaseName,
                            PdfPage = -1,
                            FinalWidth = imageWidth,
                            FinalHeight = imageHeight
                        });
                    }

                    fileIndex++;
                }
            });

            UpdateSpan();
            fileListView.ItemsSource = pdfImages;
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"{AppResources.fehler}: {ex.Message}", includeDelay: true);
        }
        finally
        {
            await BusyService.HideAsync();
            _isProcessing = false;
        }
    }

    private static void CreateDownscaledPreview(string sourcePath, string previewPath, int maxSize)
    {
        using var src = SKBitmap.Decode(sourcePath);
        if (src == null)
        {
            File.Copy(sourcePath, previewPath, overwrite: true);
            return;
        }

        double scale = Math.Min(1.0, (double)maxSize / Math.Max(src.Width, src.Height));
        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));

        using var resized = scale < 1.0
            ? src.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            : src.Copy();

        using var img = SKImage.FromBitmap(resized);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 80);
        using var fs = File.Create(previewPath);
        data.SaveTo(fs);
    }

    public static async Task<IEnumerable<FileResult>> PickMediaFileAsync()
    {
        try
        {
            var fileResult = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = AppResources.pdf_dateien_auswaehlen,
                FileTypes = CustomMediaFileType
            });
            if (fileResult != null)
                return fileResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fehler beim Auswaehlen der Datei: {ex.Message}");
        }
        return null;
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        ClearCache();
        Shell.Current.GoToAsync("..");
    }

    private void OnPagesAddClicked(object sender, EventArgs e) => AddPdfImages();

    private async void AddPdfImages()
    {
        if (_isProcessing) return;
        _isProcessing = true;

        try
        {
            await BusyService.ShowAsync(AppResources.pdf_wird_konvertiert);

            await LoadPDFImages();
            var uploads = await ProcessFileOrganizationLogic();
            SaveManager.NotifyDataChanged(uploads);

            if (Shell.Current is AppShell)
                ProjectItem.Current.ApplyFilterAndSorting();

            await BusyService.HideAsync();
            await Shell.Current.GoToAsync("project_details");
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"PDF-Error: {ex.Message}", includeDelay: true);
        }
        finally
        {
            await BusyService.HideAsync();
            _isProcessing = false;
        }
    }

    /// <summary>
    /// Rendert nur die angehakten PDF-Seiten in voller Aufloesung (PdfFullViewDpi).
    /// </summary>
    private async Task LoadPDFImages()
    {
        var itemsToProcess = fileListView.ItemsSource.Cast<PdfItem>()
            .Where(x => x.IsChecked && !string.IsNullOrEmpty(x.PdfPath))
            .ToList();

        int totalPages = itemsToProcess.Count;
        int processedPages = 0;

        var groups = itemsToProcess.GroupBy(x => x.PdfPath).ToList();

        await Parallel.ForEachAsync(groups,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (group, cancellationToken) =>
            {
                byte[] pdfBytes = await File.ReadAllBytesAsync(group.Key, cancellationToken);
                using var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes);

                foreach (var item in group)
                {
                    int current = Interlocked.Increment(ref processedPages);
                    await BusyService.SetMessageAsync(string.Format(AppResources.pdf_seite_wird_generiert, current, totalPages));

                    var (width, height) = await NativePdfRenderer.SavePageAsync(nativeDoc, item.ImagePath, item.PdfPage, item.Dpi);
                    item.FinalWidth = width;
                    item.FinalHeight = height;
                }
            });
    }

    private async Task<List<(string LocalFilePath, string SubFolder)>> ProcessFileOrganizationLogic()
    {
        var items = fileListView.ItemsSource?.Cast<PdfItem>()
            .Where(x => x.IsChecked)
            .ToList() ?? [];

        if (items.Count == 0)
            return [];

        var uploads = new System.Collections.Concurrent.ConcurrentBag<(string, string)>();
        string planFolder = GlobalJson.Data.PlanPath;
        string thumbFolder = $"{planFolder}/thumbnails";

        await Task.Run(() =>
        {
            _ = SyncClock.DeviceId;

            string imageDirectory = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, planFolder);
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(Path.Combine(imageDirectory, "thumbnails"));

            var processedPlans = new KeyValuePair<string, Plan>[items.Count];

            Parallel.For(0, items.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var item = items[i];

                if (!File.Exists(item.ImagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Planbild fehlt, Eintrag wird uebersprungen: {item.ImagePath}");
                    return;
                }

                string baseName = $"plan_{SyncClock.NewId()}_{i}";
                string fileName = baseName + ".jpg";
                string destinationFilePath = Path.Combine(imageDirectory, fileName);
                string destinationThumbPath = Path.Combine(imageDirectory, "thumbnails", fileName);

                Plan plan = new()
                {
                    Name = item.DisplayName,
                    File = fileName,
                    ImageSize = new Size(item.FinalWidth, item.FinalHeight),
                    IsGrayscale = false,
                    AllowExport = true,
                    PlanColor = "#00FFFFFF",
                    MeasureFactor = 20
                };
                plan.Touch();

                // Vorschau hat bereits PlanThumbSize -> direkt als Thumbnail uebernehmen
                try { File.Copy(item.PreviewPath, destinationThumbPath, overwrite: true); } catch { }

                File.Copy(item.ImagePath, destinationFilePath, overwrite: true);

                uploads.Add((destinationFilePath, planFolder));
                if (File.Exists(destinationThumbPath))
                    uploads.Add((destinationThumbPath, thumbFolder));

                processedPlans[i] = new KeyValuePair<string, Plan>(baseName, plan);
            });

            lock (GlobalJson.Data)
            {
                GlobalJson.Data.Plans ??= [];
                foreach (var planKvp in processedPlans)
                {
                    if (planKvp.Value != null)
                        GlobalJson.Data.Plans[planKvp.Key] = planKvp.Value;
                }
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var planKvp in processedPlans)
                {
                    if (planKvp.Value != null)
                        LoadDataToView.AddPlan(planKvp);
                }
            });

            ClearCache();
        });

        return [.. uploads];
    }

    private static void ClearCache()
    {
        if (!Directory.Exists(Settings.CacheDirectory)) return;
        foreach (var file in Directory.GetFiles(Settings.CacheDirectory))
        {
            try { File.Delete(file); } catch { }
        }
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        _isListView = !_isListView;
        btnRows.Text = _isListView ? Settings.TableGridIcon : Settings.TableRowIcon;
        UpdateSpan();
    }

    private void OnSizeChanged(object sender, EventArgs e) => UpdateSpan();

    private void UpdateSpan()
    {
        if (Width <= 0) return;

        DynamicSpan = _isListView
            ? 1
            : Math.Max(SettingsService.Instance.GridViewMinColumns,
                       (int)(Width / SettingsService.Instance.PlanPreviewSize));

        const double spacing = 4;
        double tileWidth = (Width - spacing * (DynamicSpan - 1)) / DynamicSpan;
        TileHeight = Math.Floor(tileWidth / Math.Sqrt(2));

        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(TileHeight));
    }
}