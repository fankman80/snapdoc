#nullable disable

using bsm24.Models;
using FFImageLoading.Maui;
using Mopups.Services;
using System.Collections.ObjectModel;
using UraniumUI.Pages;

namespace bsm24.Views;

public partial class SetPin : UraniumContentPage, IQueryAttributable
{
    public ObservableCollection<ImageItem> Images { get; set; }
    public string PlanId { get; set; }
    public string PinId { get; set; }
    public string PinIcon { get; set; }
    public int DynamicSpan { get; set; } = 3; // Standardwert
    public int DynamicSize { get; set; }

    public SetPin()
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
        if (query.TryGetValue("pinIcon", out object value3))
        {
            PinIcon = value3 as string;
            PinImage.Source = PinIcon;
        }

        MyView_Load();
        BindingContext = this;
    }

    private void MyView_Load()
    {
        Images ??= []; // Initialisiere Images, falls es null ist
        Images.Clear(); // lösche Einträge in Images

        // lese neue Bilder ein
        foreach (var img in GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos)
        {
            string imgPath = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[img.Key].File;
            bool isChecked = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[img.Key].IsChecked;

            Images.Add(new ImageItem
            {
                ImagePath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ThumbnailPath, imgPath),
                IsChecked = isChecked,
                DateTime = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[img.Key].DateTime
            });
        }

        ImageGallery.ItemsSource = null; // Temporär die ItemsSource auf null setzen
        ImageGallery.ItemsSource = Images; // Dann wieder auf die Collection setzen

        priorityPicker.ItemsSource = Settings.PriorityItems.Select(item => item.Key).ToList();

        // read data
        this.Title = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName;
        PinDesc.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinDesc;
        PinLocation.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinLocation;
        PinImage.Source = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon;
        LockSwitch.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked;
        LockRotate.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate;
        AllowExport.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport;
        SizePercentText.Text = Math.Round(GlobalJson.Data.Plans[PlanId].Pins[PinId].PinScale * 100, 0).ToString() + "%";
        priorityPicker.SelectedIndex = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority;

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
        {
            GeoLocButton.ImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Where_to_vote,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["PrimaryDark"]
                        : (Color)Application.Current.Resources["Primary"]
            };
        }

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].IsCustomPin)
        {
            PinImageContainer.IsVisible = false;
            SizePercentButton.IsVisible = false;
            SizePercentText.IsVisible = false;
        }
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        var tappedImage = sender as CachedImage;
        var filePath = ((FileImageSource)tappedImage.Source).File;
        var fileName = new FileResult(filePath).FileName;

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={PlanId}&pinId={PinId}&pinIcon={PinIcon}");
    }

    private async void OnDeleteClick(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie diesen Pin wirklich löschen?");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
            await Shell.Current.GoToAsync($"..?pinDelete={PinId}");
    }

    private async void OnEditClick(object sender, EventArgs e)
    {
        var popup = new PopupEntry(title: "Pin umbenennen...", inputTxt: GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        if (result != null)
        {
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = result;
            this.Title = result;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnPinSelectClick(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}");

        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(PinIcon, StringComparison.OrdinalIgnoreCase));
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Anchor = iconItem.AnchorPoint;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Size = iconItem.IconSize;
    }

    private void OnCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        bool isChecked = e.Value;

        if (sender is CheckBox checkBox)
        {
            if (checkBox.BindingContext is ImageItem item)
            {
                var fileName = Path.GetFileName(item.ImagePath);
                GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[fileName].IsChecked = isChecked;

                // save data to file
                GlobalJson.SaveToFile();
            }
        }
    }

    private async void OnOkayClick(object sender, EventArgs e)
    {
        // write data
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = this.Title;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinDesc = PinDesc.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinLocation = PinLocation.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked = LockSwitch.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate = LockRotate.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport = AllowExport.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority = priorityPicker.SelectedIndex;

        // save data to file
        GlobalJson.SaveToFile();

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].IsCustomPin)
            await Shell.Current.GoToAsync($"..");
        else
            await Shell.Current.GoToAsync($"..?pinUpdate={PinId}");
    }

    private async void ShowGeoLoc(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("////map_view");
    }

    private async void TakePhoto(object sender, EventArgs e)
    {
        FileResult path = await CapturePicture.Capture(GlobalJson.Data.ImagePath, GlobalJson.Data.ThumbnailPath);

        if (path != null)
        {
            Foto newImageData = new()
            {
                IsChecked = true,
                File = path.FileName,
                DateTime = DateTime.Now
            };

            // Neues Image hinzufügen
            var pin = GlobalJson.Data.Plans[PlanId].Pins[PinId];
            pin.Fotos[path.FileName] = newImageData;

            // save data to file
            GlobalJson.SaveToFile();

            Images.Add(new ImageItem
            {
                ImagePath = path.FullPath,
                IsChecked = true,
                DateTime = DateTime.Now
            });
        }
    }

    private async void OnResizeClicked(object sender, EventArgs e)
    {
        var popup = new PopupSlider(GlobalJson.Data.Plans[PlanId].Pins[PinId].PinScale);
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinScale = result;
        SizePercentText.Text = Math.Round(result * 100, 0).ToString() + "%";

        // save data to file
        GlobalJson.SaveToFile();
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double imageWidth = Settings.PlanPreviewSize;
        DynamicSpan = Math.Max(3, (int)(screenWidth / imageWidth));
        DynamicSize = (int)(screenWidth / DynamicSpan);
        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(DynamicSize));
    }

    private void OnSelectedValueChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker)
        {
            var selectedIndex = picker.SelectedIndex;
            priorityPicker.TextColor = Color.FromArgb(Settings.PriorityItems[selectedIndex].Color);
        }
    }
}

public class ImageItem
{
    public string ImagePath { get; set; }
    public string PreviewPath { get; set; }
    public bool IsChecked { get; set; }
    public DateTime DateTime { get; set; }
}

public partial class SquareView : ContentView
{
    protected override async void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        await Task.Yield();
        HeightRequest = Width;
    }
}

