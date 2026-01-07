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

namespace SnapDoc.Views;

public partial class IconGallery : ContentPage, IQueryAttributable
{
    public ObservableCollection<IconItem> Icons { get; set; }
    public int DynamicSpan { get; set; } = 1;
    private string PlanId;
    private string PinId;
    private string OrderDirection = "asc";
    private CancellationTokenSource _longPressCts;
    private IconItem _pressedItem;
    private const int LongPressMs = 750;
    private bool _longPressHandled;
    private bool _isPopupOpen;

    public IconGallery()
    {
        InitializeComponent(); 
        UpdateButton();

        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        SizeChanged += OnSizeChanged;
        SortPicker.SelectedIndexChanged += OnSortPickerChanged;
        CategoryPicker.SelectedIndexChanged += OnCategoryPickerChanged;

        SetIconGridView();

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
    }

    private async void OnIconClicked(object sender, TappedEventArgs e)
    {
        if (_longPressHandled)
        {
            _longPressHandled = false;
            return;
        }

        if (sender is not BindableObject view)
            return;

        _pressedItem = view.BindingContext as IconItem;
        if (_pressedItem == null)
            return;

        if (e.Buttons == ButtonsMask.Secondary)
            await ShowEditPopup(_pressedItem);

        if (e.Buttons == ButtonsMask.Primary)
        {
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

    private void IconSorting(string order)
    {
        if (SortPicker.SelectedItem == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories, CategoryPicker.SelectedItem.ToString());
            SettingsService.Instance.IconCategories = iconCategories;
            SettingsService.Instance.IconSortCrit = SortPicker.SelectedItem.ToString();
            SettingsService.Instance.IconCategory = CategoryPicker.SelectedItem.ToString();

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

    private void OnPointerPressed(object sender, PointerEventArgs e)
    {
        if (_isPopupOpen || sender is not BindableObject view)
            return;

        _pressedItem = view.BindingContext as IconItem;
        if (_pressedItem == null)
            return;

        _longPressCts?.Cancel();
        _longPressCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LongPressMs, _longPressCts.Token);

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (_isPopupOpen || _pressedItem == null)
                        return;

                    _longPressHandled = true;
                    _isPopupOpen = true;

                    try
                    {
                        await ShowEditPopup(_pressedItem);
                    }
                    finally
                    {
                        _isPopupOpen = false;
                    }
                });
            }
            catch (TaskCanceledException)
            {
                // normal
            }
        });
    }

    private void OnPointerReleased(object sender, PointerEventArgs e)
    {
        _longPressCts?.Cancel();
        _longPressCts = null;

        if (_isPopupOpen) return;

        _pressedItem = null;
        _longPressHandled = false;
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
