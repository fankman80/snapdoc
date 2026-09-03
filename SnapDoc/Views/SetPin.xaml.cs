#nullable disable
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace SnapDoc.Views;

public partial class SetPin : ContentPage, IQueryAttributable
{
    public int DynamicSpan { get; set; } = SettingsService.Instance.GridViewMinColumns;
    private string PlanId;
    private string PinId;
    private CancellationTokenSource _imageLoadingCts;

    private ObservableCollection<FotoItem> fotos = [];
    public ObservableCollection<FotoItem> Fotos
    {
        get => fotos;
        set
        {
            if (fotos != value)
            {
                fotos = value;
                OnPropertyChanged(nameof(Fotos));
            }
        }
    }

    private PinItem pin;
    public PinItem Pin
    {
        get => pin;
        set
        {
            if (pin != value)
            {
                pin = value;
                OnPropertyChanged(nameof(Pin));
            }
        }
    }

    public SetPin()
    {
        InitializeComponent();

        BindingContext = this;
    }

    protected override bool OnBackButtonPressed()
    {
        return true; // Back blockiert
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SizeChanged += OnSizeChanged;

        UpdateSpan();
        FotoLoader();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        SizeChanged -= OnSizeChanged;

        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = null;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        Pin = new PinItem(GlobalJson.Data.Plans[PlanId].Pins[PinId]);
    }

    private void FotoLoader()
    {
        // Laufende Ladevorgänge abbrechen
        _imageLoadingCts?.Cancel();
        _imageLoadingCts = new CancellationTokenSource();
        var token = _imageLoadingCts.Token;

        var fotoItems = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.Values
            .Where(img => img != null && !string.IsNullOrWhiteSpace(img.File))
            .Select(img => new FotoItem
            {
                ImagePath = Path.Combine(
                    Settings.DataDirectory,
                    SettingsService.Instance.ProjectPath,
                    GlobalJson.Data.ThumbnailPath,
                    img.File),
                OnPlanId = this.PlanId,
                OnPinId = this.PinId,
                AllowExport = img.AllowExport,
                DateTime = img.DateTime
            }.Initialize())
            .ToList();

        Fotos = fotoItems.ToObservableCollection();

        // Startet das Nachladen und Generieren der Bild-Streams im Hintergrund
        Task.Run(() => LoadImagesInBackgroundAsync(fotoItems, token), token);
    }

    private static async Task LoadImagesInBackgroundAsync(IEnumerable<FotoItem> itemsToLoad, CancellationToken token)
    {
        foreach (var item in itemsToLoad)
        {
            if (token.IsCancellationRequested) break;

            try
            {
                var fileName = Path.GetFileName(item.ImagePath);

                // 1. Thumbnail nachladen (fuer schnelle UI-Anzeige)
                if (!File.Exists(item.ImagePath))
                    await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: true);

                if (token.IsCancellationRequested) break;

                if (File.Exists(item.ImagePath))
                {
                    var bytes = File.ReadAllBytes(item.ImagePath);

                    if (token.IsCancellationRequested) break;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested)
                            item.DisplayImage = ImageSource.FromStream(() => new MemoryStream(bytes));
                    });
                }

                // 2. Originalbild im Hintergrund nachladen (fuer Offline-Verfuegbarkeit)
                string originalImagePath = Path.Combine(
                    Settings.DataDirectory,
                    SettingsService.Instance.ProjectPath,
                    GlobalJson.Data.ImagePath,
                    fileName);

                if (!File.Exists(originalImagePath))
                    await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LazyLoad Error: {ex.Message}");
            }

            await Task.Delay(10, token);
        }
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (sender is not Image tappedImage) return;
        if (tappedImage.BindingContext is not FotoItem fotoItem) return;

        var fileName = Path.GetFileName(fotoItem.ImagePath);
        string expectedFullPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, fileName);

        // Pruefen, ob das Originalbild bereits lokal existiert
        if (!File.Exists(expectedFullPath))
        {
            // Blockierende Ladeanzeige (einfacher Fallback über den Page-Title, falls du keinen Loading-Popup hast)
            string originalTitle = this.Title;
            this.Title = "Lade Foto herunter...";

            bool success = await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: false);

            this.Title = originalTitle;

            if (!success)
            {
                await this.ShowPopupAsync(new PopupAlert("Das Originalbild konnte nicht heruntergeladen werden.", "Netzwerkfehler"), Settings.PopupOptions);
                return;
            }
        }

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={PlanId}&pinId={PinId}&gotoBtn=false");
    }

    private async void OnDeleteClick(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diesen_pin_wirklich_loeschen);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);
        if (result?.Result is not DualPopupResult.Ok) return;

        DeletePinData(PinId);
        WeakReferenceMessenger.Default.Send(new PinDeletedMessage(PinId));
        await Shell.Current.GoToAsync($"///{PlanId}");
    }

    private async void OnMoveClick(object sender, EventArgs e)
    {
        var popup = new PopupPlanSelector(PlanId);
        var result = await this.ShowPopupAsync<PlanSelectorReturn>(popup, Settings.PopupOptions);
        if (result.Result == null) return;

        await MoveOrCopyPinAsync(PinId, PlanId, result.Result.PlanTarget, result.Result.IsPinCopy);
    }

    private static async Task MoveOrCopyPinAsync(
    string pinId,
    string fromPlanId,
    string toPlanId,
    bool isCopy)
    {
        if (!GlobalJson.Data.Plans.TryGetValue(toPlanId, out Plan toPlan)) return;
        if (!GlobalJson.Data.Plans.TryGetValue(fromPlanId, out Plan fromPlan)) return;
        if (!fromPlan.Pins.TryGetValue(pinId, out Pin originalPin)) return;

        Pin clonedPin = DeepClone(originalPin);

        string newId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        clonedPin.SelfId = newId;
        clonedPin.OnPlanId = toPlanId;

        if (fromPlanId == toPlanId)
            clonedPin.Pos = new Point(clonedPin.Pos.X + SettingsService.Instance.PinDuplicateOffset, clonedPin.Pos.Y);

        toPlan.Pins ??= [];
        toPlan.Pins[newId] = clonedPin;
        toPlan.PinCount++;

        if (isCopy)
        {
            clonedPin.Fotos?.Clear();
        }
        else
        {
            fromPlan.Pins.Remove(pinId);
            fromPlan.PinCount--;
            WeakReferenceMessenger.Default.Send(new PinDeletedMessage(pinId));
        }

        // save data to file
        SaveManager.NotifyDataChanged();

        await Shell.Current.GoToAsync($"///{toPlanId}?pinMove={newId}");
    }

    private static T DeepClone<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<T>(json);
    }

    private async void OnPinSelectClick(object sender, EventArgs e)
    {
        if (Pin.IsCustomPin) return;

        // save data to file
        SaveManager.NotifyDataChanged();

        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}");
    }

    private async void OnOkayClick(object sender, EventArgs e)
    {
        // save data to file
        SaveManager.NotifyDataChanged();

        WeakReferenceMessenger.Default.Send(new PinPropertyChangedMessage(PinId, Pin.IsLockPosition));

        await Shell.Current.GoToAsync("..");
    }

    private void DeletePinData(string pinId)
    {
        if (!GlobalJson.Data.Plans.TryGetValue(PlanId, out var plan) ||
            !plan.Pins.TryGetValue(pinId, out var pinToDelete))
            return;

        // Foto-Dateien loeschen (lokal + Cloud)
        foreach (var foto in pinToDelete.Fotos.Values)
        {
            string fileName = foto.File;
            if (string.IsNullOrEmpty(fileName)) continue;

            string imagePath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, fileName);
            if (File.Exists(imagePath)) File.Delete(imagePath);

            string thumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, fileName);
            if (File.Exists(thumbPath)) File.Delete(thumbPath);

            _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ImagePath}/{fileName}");
            _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.ThumbnailPath}/{fileName}");
        }

        // CustomPin-Grafiken loeschen (lokal + Cloud)
        if (pinToDelete.IsCustomPin && !string.IsNullOrEmpty(pinToDelete.PinIcon))
        {
            string baseName = Path.GetFileNameWithoutExtension(pinToDelete.PinIcon);
            string filenamePng = baseName + ".png";
            string filenameData = baseName + ".data";

            string pathPng = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.CustomPinsPath, filenamePng);
            if (File.Exists(pathPng)) File.Delete(pathPng);

            string pathData = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.CustomPinsPath, filenameData);
            if (File.Exists(pathData)) File.Delete(pathData);

            _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.CustomPinsPath}/{filenamePng}");
            _ = SaveManager.DeleteCloudFileAsync($"{GlobalJson.Data.CustomPinsPath}/{filenameData}");
        }

        // Pin aus Datenmodell entfernen
        plan.Pins.Remove(pinId);

        // Speicher-Event ausloesen
        SaveManager.NotifyDataChanged();
    }

    private async void ZoomToPinClicked(object sender, EventArgs e)
    {
            await Shell.Current.GoToAsync($"///{PlanId}?pinZoom={PinId}");
    }

    private async void ZoomToWebPinClicked(object sender, EventArgs e)
    {
        if (Pin.IsWebMapPin)
            await Shell.Current.GoToAsync($"///{PlanId}?pinZoom={PinId}");
        else
            await Shell.Current.GoToAsync($"generalmapview?planId={PlanId}&pinZoom={PinId}");
    }

    private async void TakeFoto(object sender, EventArgs e)
    {
        try
        {
            (FileResult path, Size imgSize) = await CapturePicture.Capture(
                Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath),
                Path.Combine(SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath));

            if (path == null) return;

            Foto newImageData = new()
            {
                AllowExport = true,
                File = path.FileName,
                DateTime = DateTime.Now,
                ImageSize = imgSize
            };

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[path.FileName] = newImageData;

            string originalPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, path.FileName);
            string thumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, path.FileName);

            SaveManager.NotifyDataChanged();

            var newItem = new FotoItem
            {
                ImagePath = thumbPath, // Verwende direkt thumbPath
                OnPlanId = this.PlanId,
                OnPinId = this.PinId,
                AllowExport = true,
                DateTime = DateTime.Now
            }.Initialize();

            Fotos.Add(newItem);
            this.ForceLayout();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TakeFoto failed: {ex.Message}");
        }
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if (sender is CollectionView { ItemsSource: ObservableCollection<FotoItem> reorderedItems })
        {
            var currentFotos = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos;

            var newFotosDict = reorderedItems.ToDictionary(
                img => Path.GetFileName(img.ImagePath),
                img =>
                {
                    var fileName = Path.GetFileName(img.ImagePath);
                    if (currentFotos.TryGetValue(fileName, out var existingFoto))
                    {
                        existingFoto.AllowExport = img.AllowExport;
                        return existingFoto;
                    }

                    return new Foto
                    {
                        File = fileName,
                        AllowExport = img.AllowExport,
                        DateTime = img.DateTime
                    };
                });

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos = newFotosDict;

            // save data to file
            SaveManager.NotifyDataChanged();
        }
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        if (sender is Button { BindingContext: FotoItem item })
        {
            item.AllowExport = !item.AllowExport;

            var fileName = Path.GetFileName(item.ImagePath);
            if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.TryGetValue(fileName, out var foto))
                foto.AllowExport = item.AllowExport;

            // save data to file
            SaveManager.NotifyDataChanged();
        }
    }

    private async void OnPriorityEditClicked(object sender, EventArgs e)
    {
        if (PriorityPicker.SelectedItem is not string oldKey)
            return;

        bool isCreatingNew = string.IsNullOrWhiteSpace(oldKey);
        var popup = new PopupEntry(input: oldKey, desc: AppResources.text_bearbeiten);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        var items = SettingsService.Instance.PriorityItems;

        if (isCreatingNew)
        {
            if (!string.IsNullOrWhiteSpace(result.Result))
            {
                var newItem = new PriorityItem { Key = result.Result, Color = null };
                items.Add(newItem);
                PriorityPicker.ItemsSource = null;
                PriorityPicker.ItemsSource = items.Select(x => x.Key).ToList();
                PriorityPicker.SelectedItem = result.Result;
            }
        }
        else
        {
            var priorityItem = items.FirstOrDefault(x => x.Key == oldKey);

            if (priorityItem != null)
            {
                if (string.IsNullOrWhiteSpace(result.Result))
                {
                    items.Remove(priorityItem);
                    PriorityPicker.ItemsSource = null;
                    PriorityPicker.ItemsSource = items.Select(x => x.Key).ToList();
                    PriorityPicker.SelectedItem = null;
                }
                else
                {
                    priorityItem.Key = result.Result;
                    PriorityPicker.ItemsSource = null;
                    PriorityPicker.ItemsSource = items.Select(x => x.Key).ToList();
                    PriorityPicker.SelectedItem = result.Result;
                }
            }
        }

        SettingsService.Instance.SaveSettings();
    }

    private async void OnPriorityColorClicked(object sender, EventArgs e)
    {
        if (PriorityPicker.SelectedItem is not string selectedKey)
            return;

        if (string.IsNullOrWhiteSpace(selectedKey))
            return;

        var items = SettingsService.Instance.PriorityItems;
        var priorityItem = items.FirstOrDefault(x => x.Key == selectedKey);

        if (priorityItem == null)
            return;

        var currentHexColor = priorityItem.Color ?? "#FFFFFF";
        var initialColor = Color.FromArgb(currentHexColor);

        var popup = new PopupColorPicker(initialColor);
        var result = await this.ShowPopupAsync<ColorPickerReturn>(popup, Settings.PopupOptions);
        if (result?.Result == null) return;

        priorityItem.Color = result.Result.ColorHex;
        PriorityPicker.ItemsSource = null;
        PriorityPicker.ItemsSource = items.Select(x => x.Key).ToList();
        PriorityPicker.SelectedItem = selectedKey;

        SettingsService.Instance.SaveSettings();
    }

    private void OnTitleChanged(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Entry entry) return;

        // Fokus entfernen
        entry.Unfocus();

#if ANDROID
        try
        {
            if (entry.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var inputMethodManager = nativeView.Context?.GetSystemService(
                    Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;

                // Tastatur schließen
                inputMethodManager?.HideSoftInputFromWindow(nativeView.WindowToken, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Android keyboard hide failed: {ex.Message}");
        }
#endif

#if IOS
        try
        {
            UIKit.UIApplication.SharedApplication.SendAction(
                new ObjCRuntime.Selector("resignFirstResponder"), null, null, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iOS keyboard hide failed: {ex.Message}");
        }
#endif
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double imageWidth = SettingsService.Instance.FotoPreviewSize;
        DynamicSpan = Math.Max(3, (int)(screenWidth / imageWidth));

        OnPropertyChanged(nameof(DynamicSpan));
    }
}
