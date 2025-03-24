#nullable disable

using bsm24.Services;
using CommunityToolkit.Maui.Alerts;
using Mopups.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class IconGallery : UraniumContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    private Command<IconItem> IconTappedCommand { get; }
    private string PlanId;
    private string PinId;
    private bool isLongPressed = false;
    private object previousSelectedSortItem;
    private object previousSelectedCategoryItem;
    private string OrderDirection = "asc";

    public IconGallery()
    {
        InitializeComponent();
        BindingContext = this;
        IconSorting(OrderDirection);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SortPicker.PropertyChanged += OnSortPickerChanged;
        CategoryPicker.PropertyChanged += OnCategoryPickerChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SortPicker.PropertyChanged -= OnSortPickerChanged;
        CategoryPicker.PropertyChanged -= OnCategoryPickerChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
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
        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
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

        await Shell.Current.GoToAsync($"setpin?planId={PlanId}&pinId={PinId}&pinIcon={fileName}");
    }

    private async void OnLongPressed(object sender, EventArgs e)
    {
        isLongPressed = true;
        var button = sender as Button;
        var fileName = button.AutomationId;

        // Suche Icon-Daten
        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

        var popup = new PopupIconEdit(iconItem);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        if (result != null)
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
                Path.Combine(Settings.DataDirectory, "customicons", fileName),
                "Neues Icon",
                new Point(0.5, 0.5),
                size,
                false,
                new SKColor(255, 0, 0),
                1,
                ""
            );

            var popup = new PopupIconEdit(updatedItem);
            await MopupService.Instance.PushAsync(popup);
            var popup_result = await popup.PopupDismissedTask;

            if (popup_result == null)
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
        var currentSelectedItem = SortPicker.SelectedItem;
        if (previousSelectedSortItem != currentSelectedItem)
        {
            previousSelectedSortItem = currentSelectedItem;
            IconSorting(OrderDirection);
            SettingsService.Instance.SaveSettings();
        }
    }

    private void OnCategoryPickerChanged(object sender, EventArgs e)
    {
        var currentSelectedItem = CategoryPicker.SelectedItem;
        if (previousSelectedCategoryItem != currentSelectedItem)
        {
            previousSelectedCategoryItem = currentSelectedItem;

            // Icon-Daten einlesen
            var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories, currentSelectedItem.ToString());
            SettingsService.Instance.IconCategories = iconCategories;
            Settings.PinData = iconItems;

            Settings.PinData = iconItems;
            IconSorting(OrderDirection);
            SettingsService.Instance.SaveSettings();
        }
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

        SettingsService.Instance.IconSortCrit = SortPicker.SelectedItem.ToString();

        var selectedOption = SortPicker.SelectedItem.ToString();

        if (order == "asc") // Sortiere aufsteigend
        {
            switch (SettingsService.Instance.IconSortCrit)
            {
                case var crit when crit == SettingsService.Instance.IconSortCrits[0]:
                    Icons = [.. Settings.PinData.OrderBy(pin => pin.DisplayName).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.IconSortCrits[1]:
                    Icons = [.. Settings.PinData.OrderBy(pin => pin.PinColor.ToString()).ToList()];
                    break;
            }
        }
        else // Sortiere absteigend
        {
            switch (SettingsService.Instance.IconSortCrit)
            {
                case var crit when crit == SettingsService.Instance.IconSortCrits[0]:
                    Icons = [.. Settings.PinData.OrderByDescending(pin => pin.DisplayName).ToList()];
                    break;
                case var crit when crit == SettingsService.Instance.IconSortCrits[1]:
                    Icons = [.. Settings.PinData.OrderByDescending(pin => pin.PinColor.ToString()).ToList()];
                    break;
            }
        }

        IconCollectionView.ItemsSource = null;
        IconCollectionView.ItemsSource = Icons;
    }
}
