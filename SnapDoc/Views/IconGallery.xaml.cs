#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using SkiaSharp;
using SnapDoc.Messages;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SnapDoc.Views;

public partial class IconGallery : ContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    public int DynamicSpan { get; set; } = 1;
    private string PlanId;
    private string PinId;
    private string OrderDirection = "asc";

    public ICommand TapCommand =>
        new Command<IconItem>(item => SelectIcon(item));

    public ICommand DoubleTapCommand =>
        new Command<IconItem>(async item => await ShowEditPopup(item));

    public IconGallery()
    {
        InitializeComponent(); 
        UpdateButton();

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Dispatcher.Dispatch(async () =>
        {
            SetIconGridView();

            await Task.Delay(100);
            UpdateSpan();

            IconSorting(OrderDirection);

            SizeChanged += OnSizeChanged;
            SortPicker.SelectedIndexChanged += OnSortPickerChanged;
            CategoryPicker.SelectedIndexChanged += OnCategoryPickerChanged;
        });
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
    }

    private async void SelectIcon(IconItem _pressedItem)
    {
        if (_pressedItem == null)
            return;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = _pressedItem.DisplayName;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon = _pressedItem.FileName;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Anchor = _pressedItem.AnchorPoint;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Size = _pressedItem.IconSize;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate = _pressedItem.IsRotationLocked;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockAutoScale = _pressedItem.IsAutoScaleLocked;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinColor = _pressedItem.PinColor;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinScale = _pressedItem.IconScale;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsCustomIcon = _pressedItem.IsCustomIcon;

        // save data to file
        GlobalJson.SaveToFile();

        WeakReferenceMessenger.Default.Send(new PinChangedMessage(PinId));

        await Shell.Current.GoToAsync($"..?planId={PlanId}&pinId={PinId}");
    }

    private async void ImportIconClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = AppResources.waehle_bild_aus
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
                fileName,
                AppResources.neues_icon,
                new Point(0.5, 0.5),
                size,
                false,
                false,
                true,
                new SKColor(255, 0, 0),
                1,
                AppResources.eigene_icons,
                false
            );

            var popup = new PopupIconEdit(updatedItem);
            var result2 = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

            if (result2.Result == null)
                File.Delete(localPath);

            IconSorting(OrderDirection);
        }
        catch (Exception ex)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.fehler_bild_import + ": " + ex.Message, AppResources.ok);
            else
                await Toast.Make(AppResources.fehler_bild_import + ": " + ex.Message).Show();
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

    private async void IconSorting(string order)
    {
        if (SortPicker.SelectedItem == null || CategoryPicker.SelectedItem == null)
            return;

        var selectedCategory = CategoryPicker.SelectedItem.ToString();
        var selectedCrit = SortPicker.SelectedItem.ToString();

        var (sortedIcons, categories) = await Task.Run(() =>
        {
            var data = Helper.LoadIconItems(
                Path.Combine(Settings.TemplateDirectory, "IconData.xml"),
                out List<string> iconCategories,
                selectedCategory);

            IEnumerable<IconItem> query = data;
            if (order == "asc")
            {
                query = selectedCrit == SettingsService.Instance.IconSortCrits[0]
                    ? query.OrderBy(pin => pin.DisplayName)
                    : query.OrderBy(pin => pin.PinColor.ToString());
            }
            else
            {
                query = selectedCrit == SettingsService.Instance.IconSortCrits[0]
                    ? query.OrderByDescending(pin => pin.DisplayName)
                    : query.OrderByDescending(pin => pin.PinColor.ToString());
            }

            return (SortedList: query.ToList(), Categories: iconCategories);
        });

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SettingsService.Instance.IconCategories = categories;
            SettingsService.Instance.IconSortCrit = selectedCrit;
            SettingsService.Instance.IconCategory = selectedCategory;

            if (Icons == null)
                Icons = new ObservableCollection<IconItem>(sortedIcons);
            else
            {
                Icons.Clear();
                foreach (var icon in sortedIcons)
                    Icons.Add(icon);
            }

            if (IconCollectionView.ItemsSource != Icons)
                IconCollectionView.ItemsSource = Icons;

            CategoryPicker.ItemsSource = categories;
        });
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
        if (SettingsService.Instance.IconGalleryGridView)
        {
            double screenWidth = this.Width;
            double imageWidth = SettingsService.Instance.IconPreviewSize; // Mindestbreite in Pixeln
            DynamicSpan = Math.Max(SettingsService.Instance.GridViewMinColumns, (int)(screenWidth / imageWidth));
        }
        else
            DynamicSpan = 1;

        OnPropertyChanged(nameof(DynamicSpan));
    }

    private async Task ShowEditPopup(IconItem item)
    {
        item.IsDefaultIcon = item.FileName == SettingsService.Instance.DefaultPinIcon;

        var popup = new PopupIconEdit(item);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
            IconSorting(OrderDirection);
    }
}
