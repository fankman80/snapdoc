#nullable disable

using System.Collections.ObjectModel;
using UraniumUI.Pages;

namespace bsm24.Views;

[QueryProperty(nameof(PlanId), "planId")]
[QueryProperty(nameof(PinId), "pinId")]

public partial class IconGallery : UraniumContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    public Command<IconItem> IconTappedCommand { get; }
    public string PlanId { get; set; }
    public string PinId { get; set; }
    public int DynamicSpan { get; set; } = 1;
    public int MinSize { get; set; } = 1;
    public bool IsListMode { get; set; } = true;

    public IconGallery()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        Icons = new ObservableCollection<IconItem>(Settings.PinData);
        BindingContext = this;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
    }


    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private async void OnIconClicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var fileName = button.AutomationId;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon = fileName;

        // Suche Icon-Daten
        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (iconItem != null)
        {
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = iconItem.DisplayName;
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
            btnRows.Text = "Raster";
            MinSize = 3;
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Grid_on,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]
            };
            IsListMode = false;
        }
        else
        {
            btnRows.Text = "Liste";
            MinSize = 1;
            DynamicSpan = 1;
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Table_rows,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["Primary"]
                        : (Color)Application.Current.Resources["PrimaryDark"]
            };
            IsListMode = true;
        }
        UpdateSpan();
    }

    private async void UpdateSpan()
    {
        busyOverlay.IsVisible = true;
        activityIndicator.IsRunning = true;
        busyText.Text = "Icons werden geladen...";

        await Task.Run(() =>
        {
            if (btnRows.Text == "Raster")
            {
                double screenWidth = this.Width;
                double imageWidth = 76; // Mindestbreite in Pixeln
                DynamicSpan = Math.Max(MinSize, (int)(screenWidth / imageWidth));
            }
            OnPropertyChanged(nameof(DynamicSpan));
            OnPropertyChanged(nameof(IsListMode));
        });

        activityIndicator.IsRunning = false;
        busyOverlay.IsVisible = false;
    }
}
