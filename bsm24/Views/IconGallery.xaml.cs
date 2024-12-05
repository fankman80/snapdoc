using System.Collections.ObjectModel;
using UraniumUI.Pages;
using FFImageLoading.Maui;

#nullable disable

namespace bsm24.Views;

[QueryProperty(nameof(PlanId), "planId")]
[QueryProperty(nameof(PinId), "pinId")]

public partial class IconGallery : UraniumContentPage, IQueryAttributable
{
    public string PlanId { get; set; }
    public string PinId { get; set; }
    public ObservableCollection<IconItem> Icons { get; set; }
    public int DynamicSpan { get; set; } = 5; // Standardwert
    public int DynamicSize { get; set; }
    public int MinSize = 2;
    public bool IsListMode { get; set; }

    public IconGallery()
    {
        InitializeComponent();
        UpdateSpan();
        SizeChanged += OnSizeChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        IsListMode = true;  // Standardmäßig Rasteransicht
        Icons = new ObservableCollection<IconItem>(Settings.PinData);
        BindingContext = this;
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        var tappedImage = sender as CachedImage;
        var fileName = ((FileImageSource)tappedImage.Source).File;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon = fileName;

        // Suche Icon-Daten
        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (iconItem != null)
        {
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinTxt = iconItem.DisplayName;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Anchor = iconItem.AnchorPoint;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Size = iconItem.IconSize;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate = iconItem.IsRotationLocked;
        }

        // save data to file
        GlobalJson.SaveToFile();

        await Shell.Current.GoToAsync($"..?planId={PlanId}&pinId={PinId}&pinIcon={fileName}");
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        if (btnRows.Text == "Liste")
        {
            MinSize = 5;
            btnRows.Text = "Raster";
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Splitscreen_landscape,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]   
            };
            //IconCollectionView.ItemsLayout = new GridItemsLayout(1, ItemsLayoutOrientation.Vertical);
            IsListMode = true;
        }
        else
        {
            MinSize = 1;
            btnRows.Text = "Liste";
            DynamicSpan = 1;
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Splitscreen_portrait,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]
            };
            //IconCollectionView.ItemsLayout = new GridItemsLayout(3, ItemsLayoutOrientation.Vertical);
            IsListMode = false;
        }
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double iconWidth = 64; // Mindestbreite der Icons in Pixeln
        DynamicSpan = Math.Max(5, (int)(screenWidth / iconWidth));
        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(IsListMode));
    }
}
