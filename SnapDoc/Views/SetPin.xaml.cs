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
        // Laufende Ladevorgänge abbrechen
        _imageLoadingCts?.Cancel();
        _imageLoadingCts = new CancellationTokenSource();
        var token = _imageLoadingCts.Token;

        var fotoItems = SyncOps.LiveFotos(GlobalJson.Data.Plans[PlanId].Pins[PinId])
            .Select(kv => kv.Value)
            .Where(img => !string.IsNullOrWhiteSpace(img.File))
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
        await Shell.Current.GoToAsync($"///{PlanId}");
    }

    private async void OnMoveClick(object sender, EventArgs e)
    {
        var popup = new PopupPlanSelector(PlanId);
        var result = await this.ShowPopupAsync<PlanSelectorReturn>(popup, Settings.PopupOptions);
        if (result.Result == null) return;

        await MoveOrCopyPinAsync(PinId, PlanId, result.Result.PlanTarget, result.Result.IsPinCopy);
    }

    private static async Task MoveOrCopyPinAsync(string pinId, string fromPlanId, string toPlanId, bool isCopy)
    {
        if (!SyncOps.TryGetLivePlan(toPlanId, out var toPlan)) return;
        if (!SyncOps.TryGetLivePlan(fromPlanId, out var fromPlan)) return;
        if (!SyncOps.TryGetLivePin(fromPlan, pinId, out var originalPin)) return;

        Pin clonedPin = DeepClone(originalPin);

        // Kollisionsfreie ID – siehe Hinweis unten
        string newId = SyncClock.NewId();

        clonedPin.SelfId = newId;
        clonedPin.OnPlanId = toPlanId;
        clonedPin.DeletedAt = null;          // Klon ist immer lebendig
        clonedPin.Touch();

        if (fromPlanId == toPlanId)
            clonedPin.Pos = new Point(
                clonedPin.Pos.X + SettingsService.Instance.PinDuplicateOffset, clonedPin.Pos.Y);

        if (isCopy)
            clonedPin.Fotos = [];            // statt Clear() – Fotos kann null sein

        toPlan.Pins ??= [];
        toPlan.Pins[newId] = clonedPin;
        toPlan.PinCount = SyncOps.LivePinCount(toPlan);
        toPlan.Touch();

        WeakReferenceMessenger.Default.Send(new PinAddedMessage((toPlanId, newId)));

        if (!isCopy)
        {
            // Verschieben: Original als gelöscht markieren.
            // Fotos gehören jetzt dem Klon, also NICHT die Dateien löschen.
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
        if (Pin.IsCustomPin) return;

        // save data to file
        SaveManager.NotifyDataChanged();

        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}");
    }

    private async void OnOkayClick(object sender, EventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new PinPropertyChangedMessage(PinId, Pin.IsLockPosition));

        await Shell.Current.GoToAsync("..");
    }

    private void DeletePinData(string pinId)
    {
        if (!SyncOps.TryGetLivePlan(PlanId, out var plan)) return;
        if (!SyncOps.TryGetLivePin(plan, pinId, out var pinToDelete)) return;

        string projectDir = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath);

        // --- Fotos: lokal sofort weg, Cloud erst nach Tombstone-Upload ---
        foreach (var foto in pinToDelete.Fotos?.Values ?? Enumerable.Empty<Foto>())
        {
            string fileName = foto.File;
            if (string.IsNullOrEmpty(fileName)) continue;

            string imagePath = Path.Combine(projectDir, GlobalJson.Data.ImagePath, fileName);
            if (File.Exists(imagePath)) File.Delete(imagePath);

            string thumbPath = Path.Combine(projectDir, GlobalJson.Data.ThumbnailPath, fileName);
            if (File.Exists(thumbPath)) File.Delete(thumbPath);

            // NICHT mehr direkt löschen: solange die Tombstone nicht in der
            // Cloud liegt, kennt Gerät B den Pin als lebendig und würde
            // das Bild nachladen wollen – ins Leere.
            SaveManager.QueueCloudDelete($"{GlobalJson.Data.ImagePath}/{fileName}");
            SaveManager.QueueCloudDelete($"{GlobalJson.Data.ThumbnailPath}/{fileName}");
        }

        // --- CustomPin-Grafiken ---
        if (pinToDelete.IsCustomPin && !string.IsNullOrEmpty(pinToDelete.PinIcon))
        {
            string baseName = Path.GetFileNameWithoutExtension(pinToDelete.PinIcon);
            foreach (var ext in new[] { ".png", ".data" })
            {
                string file = baseName + ext;
                string path = Path.Combine(projectDir, GlobalJson.Data.CustomPinsPath, file);
                if (File.Exists(path)) File.Delete(path);
                SaveManager.QueueCloudDelete($"{GlobalJson.Data.CustomPinsPath}/{file}");
            }
        }

        // Tombstone setzen + PinCount + Save (sendet PinDeletedMessage)
        SyncOps.DeletePin(PlanId, pinId);
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

            newImageData.Touch();
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[path.FileName] = newImageData;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Touch();

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
        if (sender is not CollectionView { ItemsSource: ObservableCollection<FotoItem> reorderedItems })
            return;

        var pin = GlobalJson.Data.Plans[PlanId].Pins[PinId];
        var currentFotos = pin.Fotos ?? [];
        var newFotosDict = new Dictionary<string, Foto>();

        // 1. Neue Reihenfolge der sichtbaren Fotos
        foreach (var img in reorderedItems)
        {
            var fileName = Path.GetFileName(img.ImagePath);
            if (currentFotos.TryGetValue(fileName, out var existing))
            {
                if (existing.AllowExport != img.AllowExport)
                {
                    existing.AllowExport = img.AllowExport;
                    existing.Touch();
                }
                newFotosDict[fileName] = existing;
            }
            else
            {
                var created = new Foto
                {
                    File = fileName,
                    AllowExport = img.AllowExport,
                    DateTime = img.DateTime
                };
                created.Touch();
                newFotosDict[fileName] = created;
            }
        }

        // 2. Tombstones hinten anhängen – sonst gehen sie verloren und
        //    die gelöschten Fotos kehren beim nächsten Sync zurück.
        foreach (var kv in currentFotos)
            if (kv.Value.IsDeleted() && !newFotosDict.ContainsKey(kv.Key))
                newFotosDict[kv.Key] = kv.Value;

        pin.Fotos = newFotosDict;
        pin.Touch();
        SaveManager.NotifyDataChanged();
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        if (sender is Button { BindingContext: FotoItem item })
        {
            item.AllowExport = !item.AllowExport;

            var fileName = Path.GetFileName(item.ImagePath);
            if (GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos.TryGetValue(fileName, out var foto))
            {
                foto.AllowExport = item.AllowExport;
                foto.Touch();
            }

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
