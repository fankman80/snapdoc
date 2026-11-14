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
        if (MoreMenuOverlay.IsVisible)
        {
            _ = HideMoreMenuAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        FotoLoader();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = null;

        Pin?.Dispose();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;

        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        if (SyncOps.TryGetLivePlan(PlanId, out var plan) && SyncOps.TryGetLivePin(plan, PinId, out var pinModel))
            Pin = new PinItem(pinModel);
    }

    private void FotoLoader()
    {
        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = new CancellationTokenSource();
        var token = _imageLoadingCts.Token;

        if (!SyncOps.TryGetLivePlan(PlanId, out var plan) || !SyncOps.TryGetLivePin(plan, PinId, out var currentPin))
        {
            Fotos = [];
            return;
        }

        var fotoItems = SyncOps.LiveFotos(currentPin)
            .Select(kv => kv.Value)
            .Where(img => !string.IsNullOrWhiteSpace(img.File))
            .Select(img => new FotoItem
            {
                ImagePath = Path.Combine(
                    Settings.DataDirectory,
                    SettingsService.Instance.ProjectPath,
                    GlobalJson.Data.ThumbnailPath,
                    img.File),
                OnPlanId = PlanId,
                OnPinId = PinId,
                AllowExport = img.AllowExport,
                DateTime = img.DateTime
            }.Initialize())
            .ToList();

        Fotos = fotoItems.ToObservableCollection();

        _ = Task.Run(() => LoadImagesInBackgroundAsync(fotoItems, token), token);
    }

    private static async Task LoadImagesInBackgroundAsync(IEnumerable<FotoItem> itemsToLoad, CancellationToken token)
    {
        foreach (var item in itemsToLoad)
        {
            if (token.IsCancellationRequested)
                break;

            try
            {
                var fileName = Path.GetFileName(item.ImagePath);

                // Thumbnail für die schnelle Anzeige nachladen.
                if (!File.Exists(item.ImagePath))
                    await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: true);

                if (token.IsCancellationRequested)
                    break;

                if (File.Exists(item.ImagePath))
                {
                    var bytes = await File.ReadAllBytesAsync(item.ImagePath, token);

                    if (token.IsCancellationRequested)
                        break;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (!token.IsCancellationRequested)
                            item.DisplayImage = ImageSource.FromStream(() => new MemoryStream(bytes));
                    });
                }

                // Originalbild für die Offline-Verfügbarkeit nachladen.
                string originalImagePath = Path.Combine(
                    Settings.DataDirectory,
                    SettingsService.Instance.ProjectPath,
                    GlobalJson.Data.ImagePath,
                    fileName);

                if (!File.Exists(originalImagePath))
                {
                    await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LazyLoad Error: {ex.Message}");
            }

            try
            {
                await Task.Delay(10, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (sender is not Image tappedImage)
            return;

        if (tappedImage.BindingContext is not FotoItem fotoItem)
            return;

        var fileName = Path.GetFileName(fotoItem.ImagePath);
        string expectedFullPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ImagePath, fileName);

        if (!File.Exists(expectedFullPath))
        {
            string originalTitle = Title;
            Title = "Lade Foto herunter...";

            bool success;

            try
            {
                success = await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: false);
            }
            finally
            {
                Title = originalTitle;
            }

            if (!success)
            {
                await this.ShowPopupAsync(new PopupAlert("Das Originalbild konnte nicht heruntergeladen werden.", "Netzwerkfehler"), Settings.PopupOptions);
                return;
            }
        }

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={PlanId}&pinId={PinId}&gotoBtn=false");
    }

    private async void OnDeleteClick()
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diesen_pin_wirklich_loeschen);
        var result = await this.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);

        if (result?.Result is not DualPopupResult.Ok)
            return;

        DeletePinData(PinId);

        await Shell.Current.GoToAsync($"///{PlanId}");
    }

    private async void OnMoveClick()
    {
        var popup = new PopupPlanSelector(PlanId, PinId);
        var result = await this.ShowPopupAsync<PlanSelectorReturn>(popup, Settings.PopupOptions);

        if (result?.Result == null)
            return;

        await MoveOrCopyPinAsync(PinId, PlanId, result.Result.PlanTarget, result.Result.IsPinCopy);
    }

    private static async Task MoveOrCopyPinAsync(string pinId, string fromPlanId, string toPlanId, bool isCopy)
    {
        if (!SyncOps.TryGetLivePlan(toPlanId, out var toPlan))
            return;

        if (!SyncOps.TryGetLivePlan(fromPlanId, out var fromPlan))
            return;

        if (!SyncOps.TryGetLivePin(fromPlan, pinId, out var originalPin))
            return;

        Pin clonedPin = DeepClone(originalPin);

        if (clonedPin == null)
            return;

        string newId = SyncClock.NewId();

        clonedPin.SelfId = newId;
        clonedPin.OnPlanId = toPlanId;
        clonedPin.DeletedAt = null;
        clonedPin.Touch();

        if (fromPlanId == toPlanId)
            clonedPin.Pos = new Point(clonedPin.Pos.X + SettingsService.Instance.PinDuplicateOffset, clonedPin.Pos.Y);

        if (isCopy)
            clonedPin.Fotos = [];

        toPlan.Pins ??= [];
        toPlan.Pins[newId] = clonedPin;
        toPlan.PinCount = SyncOps.LivePinCount(toPlan);
        toPlan.Touch();

        WeakReferenceMessenger.Default.Send(new PinAddedMessage((toPlanId, newId)));

        if (!isCopy)
        {
            SyncOps.DeletePin(fromPlanId, pinId);
            fromPlan.PinCount = SyncOps.LivePinCount(fromPlan);
        }

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
        if (Pin?.IsCustomPin != false)
            return;

        SaveManager.NotifyDataChanged();

        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}");
    }

    private void OnExportSwitchToggled(object sender, ToggledEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new PinPropertyChangedMessage(PinId, e.Value));
    }

    private void DeletePinData(string pinId)
    {
        if (!SyncOps.TryGetLivePlan(PlanId, out var plan))
            return;

        if (!SyncOps.TryGetLivePin(plan, pinId, out var pinToDelete))
            return;

        string projectDir = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath);

        // Fotos lokal entfernen und für Cloud-Löschung vormerken.
        foreach (var foto in pinToDelete.Fotos?.Values ?? Enumerable.Empty<Foto>())
        {
            string fileName = foto.File;

            if (string.IsNullOrEmpty(fileName))
                continue;

            string imagePath = Path.Combine(projectDir, GlobalJson.Data.ImagePath, fileName);

            if (File.Exists(imagePath))
                File.Delete(imagePath);

            string thumbPath = Path.Combine(projectDir, GlobalJson.Data.ThumbnailPath, fileName);

            if (File.Exists(thumbPath))
                File.Delete(thumbPath);

            SaveManager.QueueCloudDelete($"{GlobalJson.Data.ImagePath}/{fileName}");
            SaveManager.QueueCloudDelete($"{GlobalJson.Data.ThumbnailPath}/{fileName}");
        }

        // Grafiken eines benutzerdefinierten Pins entfernen.
        if (pinToDelete.IsCustomPin && !string.IsNullOrEmpty(pinToDelete.PinIcon))
        {
            string baseName = Path.GetFileNameWithoutExtension(pinToDelete.PinIcon);

            foreach (var extension in new[] { ".png", ".data" })
            {
                string fileName = baseName + extension;
                string path = Path.Combine(projectDir, GlobalJson.Data.CustomPinsPath, fileName);

                if (File.Exists(path))
                    File.Delete(path);

                SaveManager.QueueCloudDelete($"{GlobalJson.Data.CustomPinsPath}/{fileName}");
            }
        }

        SyncOps.DeletePin(PlanId, pinId);
    }

    private async void ZoomToPinClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"///{PlanId}?pinZoom={PinId}");
    }

    private async void ZoomToWebPinClicked(object sender, EventArgs e)
    {
        if (Pin?.IsWebMapPin == true)
            await Shell.Current.GoToAsync($"///{PlanId}?pinZoom={PinId}");
        else
            await Shell.Current.GoToAsync($"generalmapview?planId={PlanId}&pinZoom={PinId}");
    }

    private async void TakeFoto(object sender, EventArgs e)
    {
        try
        {
            (FileResult path, Size imgSize) = await CapturePicture.Capture(
                    Path.Combine(
                        SettingsService.Instance.ProjectPath,
                        GlobalJson.Data.ImagePath),
                    Path.Combine(
                        SettingsService.Instance.ProjectPath,
                        GlobalJson.Data.ThumbnailPath));

            if (path == null)
                return;

            if (!SyncOps.TryGetLivePlan(PlanId, out var plan) || !SyncOps.TryGetLivePin(plan, PinId, out var currentPin))
                return;

            Foto newImageData = new()
            {
                AllowExport = true,
                File = path.FileName,
                DateTime = DateTime.Now,
                ImageSize = imgSize
            };

            newImageData.Touch();

            currentPin.Fotos ??= [];
            currentPin.Fotos[path.FileName] = newImageData;
            currentPin.Touch();

            string thumbPath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, GlobalJson.Data.ThumbnailPath, path.FileName);

            SaveManager.NotifyDataChanged();

            var newItem = new FotoItem
            {
                ImagePath = thumbPath,
                OnPlanId = PlanId,
                OnPinId = PinId,
                AllowExport = true,
                DateTime = newImageData.DateTime
            }.Initialize();

            Fotos.Add(newItem);

            ImageGallery.ScrollTo(newItem, position: ScrollToPosition.End, animate: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TakeFoto failed: {ex.Message}");
        }
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if (sender is not CollectionView { ItemsSource: ObservableCollection<FotoItem> reorderedItems })
            return;

        if (!SyncOps.TryGetLivePlan(PlanId, out var plan) || !SyncOps.TryGetLivePin(plan, PinId, out var currentPin))
            return;

        var currentFotos = currentPin.Fotos ?? [];
        var newFotos = new Dictionary<string, Foto>();

        // Sichtbare Fotos in der neuen Reihenfolge übernehmen.
        foreach (var image in reorderedItems)
        {
            var fileName = Path.GetFileName(image.ImagePath);

            if (currentFotos.TryGetValue(fileName, out var existing))
            {
                if (existing.AllowExport != image.AllowExport)
                {
                    existing.AllowExport = image.AllowExport;
                    existing.Touch();
                }

                newFotos[fileName] = existing;
            }
            else
            {
                var created = new Foto
                {
                    File = fileName,
                    AllowExport = image.AllowExport,
                    DateTime = image.DateTime
                };

                created.Touch();
                newFotos[fileName] = created;
            }
        }

        // Tombstones behalten, damit gelöschte Fotos nicht zurückkehren.
        foreach (var entry in currentFotos)
        {
            if (entry.Value.IsDeleted() && !newFotos.ContainsKey(entry.Key))
                newFotos[entry.Key] = entry.Value;
        }

        currentPin.Fotos = newFotos;
        currentPin.Touch();

        SaveManager.NotifyDataChanged();
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: FotoItem item })
            return;

        item.AllowExport = !item.AllowExport;

        if (!SyncOps.TryGetLivePlan(PlanId, out var plan) || !SyncOps.TryGetLivePin(plan, PinId, out var currentPin))
            return;

        var fileName = Path.GetFileName(item.ImagePath);

        if (currentPin.Fotos?.TryGetValue(fileName, out var foto) == true)
        {
            foto.AllowExport = item.AllowExport;
            foto.Touch();
            currentPin.Touch();
        }

        SaveManager.NotifyDataChanged();
    }

    // =================================================================
    // Prioritäten
    // =================================================================

    private void ApplyPriorityChange(string selectKey)
    {
        SettingsService.Instance.SaveSettings();   // lokale Liste
        Pin?.RefreshPriorities();

        if (selectKey != null)
            Pin?.SelectPriority(selectKey);

        SaveManager.NotifyDataChanged();           // Projektdatei + Cloud
    }

    private async Task<string> AskPriorityNameAsync(string current)
    {
        var popup = new PopupEntry(input: current ?? string.Empty, desc: AppResources.text_bearbeiten);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        return result?.Result?.Trim();             // null = abgebrochen
    }

    private async void OnPriorityEditClicked(object sender, EventArgs e)
    {
        if (Pin == null) return;

        string oldKey = Pin.PriorityKey;
        string newKey = await AskPriorityNameAsync(oldKey);

        if (newKey == null) return;

        // Keine Priorität gewählt -> neue erstellen
        if (string.IsNullOrEmpty(oldKey))
        {
            if (newKey.Length == 0) return;

            ApplyPriorityChange(PriorityService.AddOrGet(newKey).Key);
            return;
        }

        // Leere Eingabe -> löschen
        if (newKey.Length == 0)
        {
            PriorityService.Delete(oldKey);
            ApplyPriorityChange(string.Empty);
            return;
        }

        if (newKey == oldKey) return;

        ApplyPriorityChange(PriorityService.Rename(oldKey, newKey));
    }

    private async void OnPriorityColorClicked(object sender, EventArgs e)
    {
        if (Pin == null) return;

        string key = Pin.PriorityKey;

        // Keine Priorität gewählt -> zuerst neue erstellen
        if (string.IsNullOrEmpty(key))
        {
            string newKey = await AskPriorityNameAsync(null);
            if (string.IsNullOrEmpty(newKey)) return;

            key = PriorityService.AddOrGet(newKey).Key;
            ApplyPriorityChange(key);
        }

        var initial = Color.FromArgb(PriorityService.ColorOf(key) ?? PriorityService.DefaultColor);

        var result = await this.ShowPopupAsync<ColorPickerReturn>(
            new PopupColorPicker(initial),
            Settings.PopupOptions);

        if (result?.Result == null) return;

        PriorityService.SetColor(key, result.Result.ColorHex);

        SettingsService.Instance.SaveSettings();
        Pin.RefreshPriorityColor();
        SaveManager.NotifyDataChanged();
    }

    // =================================================================
    // Mehr-Menü
    // =================================================================

    private async void OnMoreClicked(object sender, EventArgs e)
    {
        if (MoreMenuOverlay.IsVisible)
        {
            await HideMoreMenuAsync();
            return;
        }

        MoreMenu.AnchorX = 1;
        MoreMenu.AnchorY = 0;
        MoreMenu.Opacity = 0;
        MoreMenu.Scale = 0.92;

        MoreMenuOverlay.IsVisible = true;

        await Task.WhenAll(MoreMenu.FadeToAsync(1, 120, Easing.CubicOut), MoreMenu.ScaleToAsync( 1, 120, Easing.CubicOut));
    }

    private async Task HideMoreMenuAsync()
    {
        if (!MoreMenuOverlay.IsVisible)
            return;

        await MoreMenu.FadeToAsync(0, 90, Easing.CubicIn);

        MoreMenuOverlay.IsVisible = false;
    }

    private async void OnMoreMenuDismiss(object sender, EventArgs e)
    {
        await HideMoreMenuAsync();
    }

    private async void OnMenuMoveTapped(object sender, EventArgs e)
    {
        await HideMoreMenuAsync();
        OnMoveClick();
    }

    private async void OnMenuDeleteTapped(object sender, EventArgs e)
    {
        await HideMoreMenuAsync();
        OnDeleteClick();
    }
}