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

    private static readonly FilePickerFileType CustomMediaFileType = new(
    new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.iOS, new[] { "com.adobe.pdf", "public.jpeg", "public.png" } },
        { DevicePlatform.Android, new[] { "application/pdf", "image/jpeg", "image/png" } },
        { DevicePlatform.WinUI, new[] { ".pdf", ".jpg", ".jpeg", ".png" } },
        { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf", "public.jpeg", "public.png" } }
    });

    public int DynamicSpan { get; set; } = 0;

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

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    private async void LoadPreviewPDFImages()
    {
        if (_isProcessing) return;
        _isProcessing = true;

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

            resultList = await PickMediaFileAsync();

            if (resultList == null || !resultList.Any())
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Ladeanzeige aktivieren
            await BusyService.ShowAsync(AppResources.lade_pdf_seiten);

            string importId = DateTime.Now.ToString("yyyyMMddHHmmss");
            List<PdfItem> pdfImages = [];

            // Verarbeitung im Hintergrund ausfuehren
            await Task.Run(async () =>
            {
                Directory.CreateDirectory(Settings.CacheDirectory);

                int fileIndex = 0;
                foreach (var file in resultList)
                {
                    string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

                    if (ext == ".pdf")
                    {
                        // --- A) PDF-VERARBEITUNG ---
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
                            var (width, height) = await NativePdfRenderer.SavePageAsync(nativeDoc, previewPath, i, SettingsService.Instance.PdfThumbDpi);
                            int targetDpi = SettingsService.Instance.PdfFullViewDpi;

                            pdfImages.Add(new PdfItem
                            {
                                ImagePath = imgPath,
                                PreviewPath = previewPath,
                                PdfPath = localPdfPath,
                                IsChecked = true,
                                Dpi = targetDpi,
                                DisplayName = $"Plan {fileIndex + 1} – Seite {i + 1}",
                                ImageName = imgBaseName,
                                PdfPage = i,
                                FinalWidth = width,
                                FinalHeight = height
                            });
                        }
                    }
                    else if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        // --- B) BILD-VERARBEITUNG (JPG / PNG) ---
                        string imgBaseName = $"img_{importId}_{fileIndex}";
                        string previewPath = Path.Combine(Settings.CacheDirectory, "preview_" + imgBaseName + ".jpg");
                        string imgPath = Path.Combine(Settings.CacheDirectory, imgBaseName + ".jpg");

                        int imageWidth = 0;
                        int imageHeight = 0;

                        if (ext == ".png")
                        {
                            // PNG zu JPG konvertieren
                            using var sourceStream = await file.OpenReadAsync();
                            using var bitmap = SKBitmap.Decode(sourceStream);
                            imageWidth = bitmap.Width;
                            imageHeight = bitmap.Height;

                            using var image = SKImage.FromBitmap(bitmap);
                            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

                            using (var destStream = File.Create(imgPath))
                                data.SaveTo(destStream);

                            File.Copy(imgPath, previewPath, overwrite: true);
                        }
                        else
                        {
                            // JPG/JPEG direkt in Cache kopieren
                            using (var sourceStream = await file.OpenReadAsync())
                            using (var destStream = File.Create(imgPath))
                            {
                                await sourceStream.CopyToAsync(destStream);
                            }

                            // Bildgroesse mittels SkiaSharp ermitteln
                            using (var bitmap = SKBitmap.Decode(imgPath))
                            {
                                imageWidth = bitmap.Width;
                                imageHeight = bitmap.Height;
                            }

                            File.Copy(imgPath, previewPath, overwrite: true);
                        }

                        pdfImages.Add(new PdfItem
                        {
                            ImagePath = imgPath,
                            PreviewPath = previewPath,
                            PdfPath = null, // Ist kein PDF
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
        if (_isProcessing)
            return;

        _isProcessing = true;

        try
        {
            // BusyOverlay anzeigen
            await BusyService.ShowAsync(
                AppResources.pdf_wird_konvertiert);

            await LoadPDFImages();

            await ProcessFileOrganizationLogic();

            SaveManager.NotifyDataChanged();

            if (Shell.Current is AppShell shell)
                shell.ApplyFilterAndSorting();

            // BusyOverlay schließen
            await BusyService.HideAsync();

            await Shell.Current.GoToAsync("project_details");
        }
        catch (Exception ex)
        {
            await SnackbarExtensions.ShowSafeAsync($"PDF-Error: {ex.Message}", includeDelay: true);
        }
        finally
        {
            // BusyOverlay schließen
            await BusyService.HideAsync();

            _isProcessing = false;
        }
    }

    private async Task LoadPDFImages()
    {
        var itemsToProcess = fileListView.ItemsSource.Cast<PdfItem>()
                        .Where(x => x.IsChecked && !string.IsNullOrEmpty(x.PdfPath))
                        .ToList();

        int totalPages = itemsToProcess.Count;
        int processedPages = 0;

        // Nur die Elemente filtern, die wirklich aus einem PDF gerendert werden muessen (PdfPath != null)
        var groups = itemsToProcess.GroupBy(x => x.PdfPath).ToList();

        await Parallel.ForEachAsync(groups, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (group, cancellationToken) =>
        {
            byte[] pdfBytes = await File.ReadAllBytesAsync(group.Key, cancellationToken);
            using var nativeDoc = await NativePdfRenderer.OpenDocumentAsync(pdfBytes);

            foreach (var item in group)
            {
                int current = Interlocked.Increment(ref processedPages);
                await BusyService.SetMessageAsync(string.Format(AppResources.pdf_seite_wird_generiert, current, totalPages));

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

                File.Copy(item.ImagePath, destinationFilePath, overwrite: true);

                processedPlans[i] = new KeyValuePair<string, Plan>(planId, plan);
            });

            lock (GlobalJson.Data)
            {
                GlobalJson.Data.Plans ??= [];
                foreach (var planKvp in processedPlans)
                {
                    if (planKvp.Value != null)
                        GlobalJson.Data.Plans[Path.GetFileNameWithoutExtension(planKvp.Value.File)] = planKvp.Value;
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
            double imageWidth = SettingsService.Instance.PlanPreviewSize;
            DynamicSpan = Math.Max(SettingsService.Instance.GridViewMinColumns, (int)(screenWidth / imageWidth));
        }

        OnPropertyChanged(nameof(DynamicSpan));
    }
}