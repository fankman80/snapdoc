#nullable disable

using SnapDoc.Services;
using System.Collections.ObjectModel;
using SnapDoc.Resources.Languages;

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
            if (_isActiveToggle == value)
                return;

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

        SizeChanged += OnSizeChanged;

        SetIconGridView();

        UpdateSpan();
        FotoLoader();
        ApplyFilterAndSorting();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _imageLoadingCts?.Cancel(); // Ladevorgang stoppen
        SizeChanged -= OnSizeChanged;
    }

    private void FotoLoader()
    {
        _imageLoadingCts?.Cancel();
        _imageLoadingCts = new CancellationTokenSource();
        var token = _imageLoadingCts.Token;

        var list = new List<FotoItem>();

        foreach (var planEntry in GlobalJson.Data.Plans)
        {
            var planId = planEntry.Key;
            var plan = planEntry.Value;
            if (plan?.Pins == null) continue;

            foreach (var pinEntry in plan.Pins)
            {
                var pinId = pinEntry.Key;
                var pin = pinEntry.Value;
                if (pin?.Fotos == null) continue;

                foreach (var fotoEntry in pin.Fotos)
                {
                    var foto = fotoEntry.Value;
                    if (foto == null || string.IsNullOrWhiteSpace(foto.File)) continue;

                    list.Add(new FotoItem
                    {
                        ImagePath = SafeCombine(
                            Settings.DataDirectory,
                            GlobalJson.Data.ProjectPath,
                            GlobalJson.Data.ThumbnailPath,
                            foto.File),
                        DateTime = foto.DateTime,
                        AllowExport = foto.AllowExport,
                        OnPlanId = planId,
                        OnPinId = pinId
                    });
                }
            }
        }

        AllFotos = list;
        ApplyFilterAndSorting();

        // Startet das Laden der Bilder im Hintergrund
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
                if (File.Exists(item.ImagePath))
                {
                    // 1. Bytes einmalig im Hintergrund lesen
                    var bytes = File.ReadAllBytes(item.ImagePath);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // 2. WICHTIG: Die Factory muss einen NEUEN MemoryStream 
                        // aus dem Byte-Array liefern, wenn sie aufgerufen wird.
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
        var tappedButton = sender as Button;
        FotoItem item = (FotoItem)tappedButton.BindingContext;

        if (item == null)
            return;

        item.AllowExport = !item.AllowExport;

        var fileName = Path.GetFileName(item.ImagePath);
        GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Fotos[fileName].AllowExport = !GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Fotos[fileName].AllowExport;

        // save data to file
        GlobalJson.SaveToFile();
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var tappedButton = sender as Button;
        FotoItem item = (FotoItem)tappedButton.BindingContext;

        if (item == null)
            return;

        await Shell.Current.GoToAsync($"setpin?planId={item.OnPlanId}&pinId={item.OnPinId}");
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        if (sender is not VisualElement element)
            return;

        if (element.BindingContext is not FotoItem item)
            return;

        var fileName = Path.GetFileName(item.ImagePath);

        await Shell.Current.GoToAsync(
            $"imageview?imgSource={fileName}&planId={item.OnPlanId}&pinId={item.OnPinId}&gotoBtn=true");
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void ApplyFilterAndSorting()
    {
        if (AllFotos == null)
            return;

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
