#nullable disable

using CommunityToolkit.Maui.Alerts;
using DocumentFormat.OpenXml.Spreadsheet;
using Mopups.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using UraniumUI.Pages;
using bsm24.Services;

namespace bsm24.Views;

public partial class IconGallery : UraniumContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    public Command<IconItem> IconTappedCommand { get; }
    public string PlanId;
    public string PinId;
    public int DynamicSpan;
    public int MinSize;
    private bool isLongPressed = false;

    public IconGallery()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        IconSorting();
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

        IconSorting();
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

            var origName = Path.Combine(FileSystem.AppDataDirectory, "customicons", result.FileName);
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
            var localPath = Path.Combine(FileSystem.AppDataDirectory, "customicons", fileName);

            if (!Directory.Exists(Path.Combine(FileSystem.AppDataDirectory, "customicons")))
                Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "customicons"));

            using (var fileStream = File.Create(localPath))
            {
                await stream.CopyToAsync(fileStream);
            }

            var size = await Task.Run(() => GetImageSize(localPath));

            var updatedItem = new IconItem(
                Path.Combine(FileSystem.AppDataDirectory, "customicons", fileName),
                "Neues Icon",
                new Point(0.5, 0.5),
                size,
                false,
                new SKColor(255, 0, 0),
                1
            );

            var popup = new PopupIconEdit(updatedItem);
            await MopupService.Instance.PushAsync(popup);
            var popup_result = await popup.PopupDismissedTask;

            if (popup_result == null)
                File.Delete(localPath);  // Delete temporary Icon-File

            IconSorting();
        }
        catch (Exception ex)
        {
            Toast.Make("Fehler beim Importieren des Bildes: " + ex.Message);
        }
    }

    private void UpdateSpan()
    {
        OnPropertyChanged(nameof(DynamicSpan));
    }

    public static Size GetImageSize(string localPath)
    {
        using var stream = File.OpenRead(localPath);
        using var bitmap = SKBitmap.Decode(stream);
        return new Size(bitmap.Width, bitmap.Height);
    }

    private void OnSortPickerChanged(object sender, EventArgs e)
    {
        IconSorting();
        SettingsService.Instance.SaveSettings();
    }

    private void IconSorting()
    {
        if (SortPicker.SelectedIndex == -1) return;

        SettingsService.Instance.IconSortCrit = SortPicker.SelectedItem.ToString();

        var selectedOption = SortPicker.SelectedItem.ToString();

        switch (SettingsService.Instance.IconSortCrit)
        {
            case var crit when crit == SettingsService.Instance.IconSortCrits[0]:
                Icons = [.. Settings.PinData.OrderBy(pin => pin.DisplayName).ToList()];
                break;
            case var crit when crit == SettingsService.Instance.IconSortCrits[1]:
                Icons = [.. Settings.PinData.OrderBy(pin => pin.PinColor.ToString()).ToList()];
                break;
        }

        IconCollectionView.ItemsSource = null;
        IconCollectionView.ItemsSource = Icons;
    }
}
