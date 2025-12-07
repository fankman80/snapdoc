#nullable disable

using SnapDoc.Services;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class FotoGalleryView : ContentPage
{
    public ObservableCollection<FotoItem> Fotos { get; set; }
    private List<FotoItem> AllFotos;

    private string OrderDirection = "asc";
    public int DynamicSpan { get; set; } = 3;
    public int DynamicSize;
    private bool _isActiveToggle;
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
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Helper.RemoveAllDuplicatePages();

        SizeChanged += OnSizeChanged;

        UpdateSpan();
        FotoLoader();
        ApplyFilterAndSorting();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SizeChanged -= OnSizeChanged;
    }

    private void FotoLoader()
    {
        var list = new List<FotoItem>();

        foreach (var planEntry in GlobalJson.Data.Plans)
        {
            var planId = planEntry.Key;
            var plan = planEntry.Value;

            if (plan?.Pins == null)
                continue;

            foreach (var pinEntry in plan.Pins)
            {
                var pinId = pinEntry.Key;
                var pin = pinEntry.Value;

                if (pin?.Fotos == null)
                    continue;

                foreach (var fotoEntry in pin.Fotos)
                {
                    var foto = fotoEntry.Value;

                    if (foto == null || string.IsNullOrWhiteSpace(foto.File))
                        continue;

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
        var button = sender as Button;
        FotoItem item = (FotoItem)button.BindingContext;

        if (item != null)
        {
            item.AllowExport = !item.AllowExport;

            var fileName = Path.GetFileName(item.ImagePath);
            GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Fotos[fileName].AllowExport = !GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Fotos[fileName].AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var tappedButton = sender as Button;
        FotoItem item = (FotoItem)tappedButton.BindingContext;

        await Shell.Current.GoToAsync($"setpin?planId={item.OnPlanId}&pinId={item.OnPinId}&sender=fotogallery");
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        var tappedImage = sender as Image;
        var filePath = ((FileImageSource)tappedImage.Source).File;
        var fileName = new FileResult(filePath).FileName;
        FotoItem item = (FotoItem)tappedImage.BindingContext;

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={item.OnPlanId}&pinId={item.OnPinId}&gotoBtn=true");
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void ApplyFilterAndSorting()
    {
        if (AllFotos == null)
            return;

        var filtered = AllFotos
            .Where(p => !IsActiveToggle || p.AllowExport);

        if (OrderDirection == "asc")
            filtered = filtered.OrderBy(p => p.DateTime);
        else
            filtered = filtered.OrderByDescending(p => p.DateTime);

        Fotos = new ObservableCollection<FotoItem>(filtered);
        FotoGallery.ItemsSource = Fotos;
        FotoCounterLabel.Text = $"Fotos: {Fotos.Count}";
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

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double imageWidth = SettingsService.Instance.PlanPreviewSize;
        DynamicSpan = Math.Max(3, (int)(screenWidth / imageWidth));
        DynamicSize = (int)(screenWidth / DynamicSpan);
        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(DynamicSize));
    }
}
