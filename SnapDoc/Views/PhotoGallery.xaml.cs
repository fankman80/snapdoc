#nullable disable
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class FotoGalleryView : ContentPage
{
    public ObservableCollection<FotoItem> Fotos { get; set; } = [];
    public int DynamicSpan { get; set; } = SettingsService.Instance.GridViewMinColumns;
    private string OrderDirection = "asc";
    private bool _isActiveToggle;
    private CancellationTokenSource _imageLoadingCts;
    private List<FotoItem> AllFotos;

    public bool IsActiveToggle
    {
        get => _isActiveToggle;
        set
        {
            if (_isActiveToggle == value) return;

            _isActiveToggle = value;
            OnPropertyChanged();
            ApplyFilterAndSorting();
        }
    }

    public FotoGalleryView()
    {
        InitializeComponent();
        UpdateButton();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Remote-Änderungen (Löschen/Hinzufügen/Overlay vom anderen Gerät) live übernehmen
        WeakReferenceMessenger.Default.Register<RemoteDataChangedMessage>(this, (r, m) =>
        {
            if (m.Value == RemoteChangeType.FotosUpdated)
                MainThread.BeginInvokeOnMainThread(FotoLoader);
        });

        Dispatcher.Dispatch(async () =>
        {
            SetIconGridView();
            await Task.Delay(100);
            UpdateSpan();
            FotoLoader();
            SizeChanged += OnSizeChanged;
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<RemoteDataChangedMessage>(this);

        _imageLoadingCts?.Cancel();
        _imageLoadingCts?.Dispose();
        _imageLoadingCts = null;
        SizeChanged -= OnSizeChanged;
    }

    private void FotoLoader()
    {
        _imageLoadingCts?.Cancel();
        _imageLoadingCts = new CancellationTokenSource();
        var token = _imageLoadingCts.Token;

        var list = new List<FotoItem>();
        if (GlobalJson.Data?.Plans == null) return;

        // Nur lebende Pläne, Pins und Fotos – gelöschte erzeugen sonst leere Items
        foreach (var (planId, plan) in SyncOps.LivePlans(GlobalJson.Data).ToList())
        {
            foreach (var (pinId, pin) in SyncOps.LivePins(plan).ToList())
            {
                foreach (var (fotoKey, foto) in SyncOps.LiveFotos(pin).ToList())
                {
                    string file = string.IsNullOrWhiteSpace(foto.File) ? fotoKey : foto.File;
                    if (string.IsNullOrWhiteSpace(file)) continue;

                    list.Add(new FotoItem
                    {
                        ImagePath = SafeCombine(Settings.DataDirectory, SettingsService.Instance.ProjectPath,
                                                GlobalJson.Data.ThumbnailPath, file),
                        DateTime = foto.DateTime,
                        AllowExport = foto.AllowExport,
                        OnPlanId = planId,
                        OnPinId = pinId,
                        FotoKey = fotoKey
                    });
                }
            }
        }

        AllFotos = list;
        ApplyFilterAndSorting();
        Task.Run(() => LoadImagesInBackgroundAsync(token), token);
    }

    private async Task LoadImagesInBackgroundAsync(CancellationToken token)
    {
        var itemsToLoad = AllFotos.ToList();

        foreach (var item in itemsToLoad)
        {
            if (token.IsCancellationRequested) break;

            try
            {
                var fileName = Path.GetFileName(item.ImagePath);

                // Fehlende Dateien vom Server nachladen
                if (!File.Exists(item.ImagePath))
                    await SaveManager.DownloadMediaOnDemandAsync(fileName, isThumbnail: true);

                // Pruefen, ob waehrend des Downloads die Galerie verlassen wurde
                if (token.IsCancellationRequested) break;

                if (File.Exists(item.ImagePath))
                {
                    var bytes = File.ReadAllBytes(item.ImagePath);

                    if (token.IsCancellationRequested) break;

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Nur aktualisieren, wenn der Vorgang nicht abgebrochen wurde
                        if (!token.IsCancellationRequested)
                            item.DisplayImage = ImageSource.FromStream(() => new MemoryStream(bytes));
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LazyLoad Error: {ex.Message}");
            }

            await Task.Delay(10, token);
        }
    }

    private static string SafeCombine(params string[] parts)
    {
        var valid = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

        if (valid.Length == 0)
            return string.Empty;

        return Path.Combine(valid);
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.BindingContext is not FotoItem item) return;

        if (!SyncOps.TryGetLivePlan(item.OnPlanId, out var plan) ||
            !SyncOps.TryGetLivePin(plan, item.OnPinId, out var pin) ||
            pin.Fotos == null || !pin.Fotos.TryGetValue(item.FotoKey, out var foto))
            return;

        item.AllowExport = !item.AllowExport;
        foto.AllowExport = item.AllowExport;   // gleichsetzen statt getrennt toggeln
        foto.Touch();

        SaveManager.NotifyDataChanged();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var tappedButton = sender as Button;
        FotoItem item = (FotoItem)tappedButton.BindingContext;

        if (item == null) return;

        await Shell.Current.GoToAsync($"setpin?planId={item.OnPlanId}&pinId={item.OnPinId}");
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        if ((sender as VisualElement)?.BindingContext is not FotoItem item) return;

        await Shell.Current.GoToAsync(
            $"imageview?imgSource={item.FotoKey}&planId={item.OnPlanId}&pinId={item.OnPinId}&gotoBtn=true");
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void ApplyFilterAndSorting()
    {
        if (AllFotos == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var filtered = AllFotos
                .Where(p => !IsActiveToggle || p.AllowExport);

            filtered = OrderDirection == "asc"
                ? filtered.OrderBy(p => p.DateTime)
                : filtered.OrderByDescending(p => p.DateTime);

            Fotos.Clear();
            foreach (var item in filtered)
                Fotos.Add(item);

            FotoCounterLabel.Text = $"{AppResources.fotos}: {Fotos.Count}";
        });
    }

    private void OnFilterToggleChanged(object sender, ToggledEventArgs e)
    {
        IsActiveToggle = e.Value;

        ApplyFilterAndSorting();
    }

    private void OnSortDirectionClicked(object sender, EventArgs e)
    {
        SortDirection.ScaleY *= -1;
        OrderDirection = (OrderDirection == "asc") ? "desc" : "asc";
        ApplyFilterAndSorting();
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.PhotoGalleryGridView = !SettingsService.Instance.PhotoGalleryGridView;
        SettingsService.Instance.SaveSettings();
        SetIconGridView();
        UpdateButton();
        UpdateSpan();
    }

    private void SetIconGridView()
    {
        if (SettingsService.Instance.PhotoGalleryGridView)
            FotoGallery.ItemTemplate = (DataTemplate)Resources["GalleryGridTemplate"];
        else
            FotoGallery.ItemTemplate = (DataTemplate)Resources["GalleryListTemplate"];
    }

    private void UpdateButton()
    {
        if (SettingsService.Instance.PhotoGalleryGridView)
            btnRows.Text = Settings.TableRowIcon;
        else
            btnRows.Text = Settings.TableGridIcon;
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double imageWidth = SettingsService.Instance.FotoPreviewSize;

        if (SettingsService.Instance.PhotoGalleryGridView)
            DynamicSpan = Math.Max(SettingsService.Instance.GridViewMinColumns, (int)(screenWidth / imageWidth));
        else
            DynamicSpan = 1;

        OnPropertyChanged(nameof(DynamicSpan));
    }
}
