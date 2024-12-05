using bsm24.Models;
using FFImageLoading.Maui;
using Mopups.Services;
using System.Collections.ObjectModel;
using UraniumUI.Pages;

#nullable disable

namespace bsm24.Views;

[QueryProperty(nameof(PlanId), "planId")]
[QueryProperty(nameof(PinId), "pinId")]
[QueryProperty(nameof(PinIcon), "pinIcon")]

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

        // read data
        PinTxt.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinTxt;
        PinInfo.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].InfoTxt;
        PinImage.Source = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon;
        LockSwitch.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked;
        LockRotate.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate;
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

    private async void OnPinSelectClick(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}");
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
        var iconItem = Settings.PinData.FirstOrDefault(item => item.FileName.Equals(PinIcon, StringComparison.OrdinalIgnoreCase));
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Anchor = iconItem.AnchorPoint;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].Size = iconItem.IconSize;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinTxt = PinTxt.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].InfoTxt = PinInfo.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked = LockSwitch.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLockRotate = LockRotate.IsToggled;

        // save data to file
        GlobalJson.SaveToFile();

        await Shell.Current.GoToAsync($"..?pinUpdate={PinId}");
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

    private void OnSizeChanged(object sender, EventArgs e)
    {
        UpdateSpan();
    }

    private void UpdateSpan()
    {
        double screenWidth = this.Width;
        double imageWidth = Settings.PlanPreviewSize; // Mindestbreite in Pixeln
        DynamicSpan = Math.Max(3, (int)(screenWidth / imageWidth));
        DynamicSize = (int)(screenWidth / DynamicSpan);
        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(DynamicSize));
    }
}

public class ImageItem
{
    public string ImagePath { get; set; }
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

