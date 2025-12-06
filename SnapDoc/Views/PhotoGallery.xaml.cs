#nullable disable

using SnapDoc.Services;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class PhotoGalleryView : ContentPage
{
    public ObservableCollection<PhotoItem> Photos { get; set; }
    private List<PhotoItem> AllPhotos;

    private string OrderDirection = "asc";
    public int DynamicSpan { get; set; } = 3;
    public int DynamicSize;
    private bool _isActiveToggle;
    private PhotoItem _lastVisibleItem;
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

    public PhotoGalleryView()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SizeChanged += OnSizeChanged;

        UpdateSpan();
        PhotoLoader();
        ApplyFilterAndSorting();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SizeChanged -= OnSizeChanged;
    }

    private void PhotoLoader()
    {
        var list = new List<PhotoItem>();

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

                if (pin?.Photos == null)
                    continue;

                foreach (var photoEntry in pin.Photos)
                {
                    var photo = photoEntry.Value;

                    if (photo == null || string.IsNullOrWhiteSpace(photo.File))
                        continue;

                    list.Add(new PhotoItem
                    {
                        ImagePath = SafeCombine(
                            Settings.DataDirectory,
                            GlobalJson.Data.ProjectPath,
                            GlobalJson.Data.ThumbnailPath,
                            photo.File),
                        DateTime = photo.DateTime,
                        AllowExport = photo.AllowExport,
                        OnPlanId = planId,
                        OnPinId = pinId
                    });
                }
            }
        }

        AllPhotos = list;
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
        PhotoItem item = (PhotoItem)button.BindingContext;

        if (item != null)
        {
            item.AllowExport = !item.AllowExport;

            var fileName = Path.GetFileName(item.ImagePath);
            GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Photos[fileName].AllowExport = !GlobalJson.Data.Plans[item.OnPlanId].Pins[item.OnPinId].Photos[fileName].AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        var tappedButton = sender as Button;
        PhotoItem item = (PhotoItem)tappedButton.BindingContext;

        await Shell.Current.GoToAsync($"setpin?planId={item.OnPlanId}&pinId={item.OnPinId}&sender=pinList");
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        var tappedImage = sender as Image;
        var filePath = ((FileImageSource)tappedImage.Source).File;
        var fileName = new FileResult(filePath).FileName;
        PhotoItem item = (PhotoItem)tappedImage.BindingContext;

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={item.OnPlanId}&pinId={item.OnPinId}");
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        ApplyFilterAndSorting();
    }

    private void ApplyFilterAndSorting()
    {
        if (AllPhotos == null)
            return;

        var filtered = AllPhotos
            .Where(p => !IsActiveToggle || p.AllowExport);

        if (OrderDirection == "asc")
            filtered = filtered.OrderBy(p => p.DateTime);
        else
            filtered = filtered.OrderByDescending(p => p.DateTime);

        Photos = new ObservableCollection<PhotoItem>(filtered);
        PhotoGallery.ItemsSource = Photos;
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