#nullable disable

using SnapDoc.Messages;
using SnapDoc.Services;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class IconGallery : ContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    private string PlanId;
    private string PinId;
    private string SenderView;
    private bool isLongPressed = false;
    private string OrderDirection = "asc";
    public int DynamicSpan { get; set; } = 1; // Standardwert

    public IconGallery()
    {
        InitializeComponent(); 

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SizeChanged += OnSizeChanged;
        SortPicker.SelectedIndexChanged += OnSortPickerChanged;
        CategoryPicker.SelectedIndexChanged += OnCategoryPickerChanged;

        SetIconGridView();
        UpdateButton();
        UpdateSpan();
        IconSorting(OrderDirection);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SizeChanged -= OnSizeChanged;
        SortPicker.SelectedIndexChanged -= OnSortPickerChanged;
        CategoryPicker.SelectedIndexChanged -= OnCategoryPickerChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
        if (query.TryGetValue("sender", out object value3))
            SenderView = value3 as string;
    }

    private async void OnIconClicked(object sender, EventArgs e)
    {
        if (isLongPressed)
        {
            isLongPressed = false;
            return;
        }

        var button = sender as Button;
        var fileName = button.AutomationId;

        // Falls CustomIcon, dann wird Pfad relativ gesetzt
        int index = fileName.IndexOf("customicons", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
            fileName = fileName[index..];

        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon = fileName;

        // Suche Icon-Daten
        var iconItem = Settings.IconData.FirstOrDefault(item => item.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
        if (iconItem != null)
        {
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = iconItem.DisplayName;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Anchor = iconItem.AnchorPoint;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Size = iconItem.IconSize;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate = iconItem.IsRotationLocked;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinColor = iconItem.PinColor;
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinScale = iconItem.IconScale;
        }

        // save data to file
        GlobalJson.SaveToFile();

        WeakReferenceMessenger.Default.Send(new PinChangedMessage(PinId));

        await Shell.Current.GoToAsync($"{SenderView}?planId={PlanId}&pinId={PinId}");
    }

    private async void OnLongPressed(object sender, EventArgs e)
    {
        isLongPressed = true;
        var button = sender as Button;
        var fileName = button.AutomationId;

        // Suche Icon-Daten
        var iconItem = Settings.IconData.FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

        var popup = new PopupIconEdit(iconItem);
        var result1 = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result1.Result != null)
            IconSorting(OrderDirection);
    }

    private async void ImportIconClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Wähle ein Bild aus"
            });

            if (result == null)
                return;

            var origName = Path.Combine(Settings.DataDirectory, "customicons", result.FileName);
            var ext = Path.GetExtension(origName);
            string newName = origName;
            int i = 1;
            while (File.Exists(newName))
            {
                newName = Path.GetFileNameWithoutExtension(origName) + "_" + i.ToString() + ext;
                i++;
            }

            var fileName = newName;    
            using var stream = await result.OpenReadAsync();
            var localPath = Path.Combine(Settings.DataDirectory, "customicons", fileName);

            if (!Directory.Exists(Path.Combine(Settings.DataDirectory, "customicons")))
                Directory.CreateDirectory(Path.Combine(Settings.DataDirectory, "customicons"));

            using (var fileStream = File.Create(localPath))
            {
                await stream.CopyToAsync(fileStream);
            }

            var size = await Task.Run(() => GetImageSize(localPath));

            var updatedItem = new IconItem(
                Path.Combine("customicons", fileName),
                "Neues Icon",
                new Point(0.5, 0.5),
                size,
                false,
                new SKColor(255, 0, 0),
                1,
                "eigene Icons",
                false
            );

            var popup = new PopupIconEdit(updatedItem);
            var result2 = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

            if (result2.Result == null)
                File.Delete(localPath);  // Delete temporary Icon-File

            IconSorting(OrderDirection);
        }
        catch (Exception ex)
        {
            Toast.Make("Fehler beim Importieren des Bildes: " + ex.Message);
        }
    }

    public static Size GetImageSize(string localPath)
    {
        using var stream = File.OpenRead(localPath);
        using var bitmap = SKBitmap.Decode(stream);
        return new Size(bitmap.Width, bitmap.Height);
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        IconSorting(OrderDirection);
        SettingsService.Instance.SaveSettings();
    }

    private void OnCategoryPickerChanged(object sender, EventArgs e)
    {
        IconSorting(OrderDirection);
        SettingsService.Instance.SaveSettings();
    }

    private void OnSortDirectionClicked(object sender, EventArgs e)
    {

        if (OrderDirection == "asc")
        {
            SortDirection.ScaleY *= -1;
            OrderDirection = "desc";
            IconSorting("desc");
        }
        else
        {
            SortDirection.ScaleY *= -1;
            OrderDirection = "asc";
            IconSorting("asc");
        }
    }

    private void IconSorting(string order)
    {
        if (SortPicker.SelectedItem == null) return;

        var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories, CategoryPicker.SelectedItem.ToString());
        SettingsService.Instance.IconCategories = iconCategories;
        SettingsService.Instance.IconSortCrit = SortPicker.SelectedItem.ToString();
        SettingsService.Instance.IconCategory = CategoryPicker.SelectedItem.ToString();
        Settings.IconData = iconItems;

        CategoryPicker.ItemsSource = iconCategories;

        var selectedOption = SortPicker.SelectedItem.ToString();

        if (order == "asc") // Sortiere aufsteigend
        {
            switch (SettingsService.Instance.IconSortCrit)
            {
                case var crit when crit == SettingsService.Instance.IconSortCrits[0]:
                    Icons = [.. Settings.IconData.OrderBy(pin => pin.DisplayName).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.IconSortCrits[1]:
                    Icons = [.. Settings.IconData.OrderBy(pin => pin.PinColor.ToString()).ToList()];
                    break;
            }
        }
        else // Sortiere absteigend
        {
            switch (SettingsService.Instance.IconSortCrit)
            {
                case var crit when crit == SettingsService.Instance.IconSortCrits[0]:
                    Icons = [.. Settings.IconData.OrderByDescending(pin => pin.DisplayName).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.IconSortCrits[1]:
                    Icons = [.. Settings.IconData.OrderByDescending(pin => pin.PinColor.ToString()).ToList()];
                    break;
            }
        }

        IconCollectionView.ItemsSource = null;
        IconCollectionView.ItemsSource = Icons;
    }

    private void OnChangeRowsClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.IconGalleryGridView = !SettingsService.Instance.IconGalleryGridView;
        SettingsService.Instance.SaveSettings();
        SetIconGridView();
        UpdateButton();
        UpdateSpan();
    }

    private void SetIconGridView()
    {
        if (SettingsService.Instance.IconGalleryGridView)
            IconCollectionView.ItemTemplate = (DataTemplate)Resources["IconGridTemplate"];
        else
            IconCollectionView.ItemTemplate = (DataTemplate)Resources["IconListTemplate"];
    }

    private void UpdateButton()
    {
        if (SettingsService.Instance.IconGalleryGridView)
        {
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Grid_on,
                Color = (Color)Application.Current.Resources["PrimaryDark"]};
            btnRows.Text = "Kacheln";
        }
        else
        {
            btnRows.IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Table_rows,
                Color = (Color)Application.Current.Resources["PrimaryDark"]};
            btnRows.Text = "Liste";
        }
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        if (SettingsService.Instance.IconGalleryGridView)
        {
            double screenWidth = this.Width;
            double imageWidth = SettingsService.Instance.IconPreviewSize; // Mindestbreite in Pixeln
            DynamicSpan = Math.Max(2, (int)(screenWidth / imageWidth));
        }
        else
            DynamicSpan = 1;
        OnPropertyChanged(nameof(DynamicSpan));
    }
}