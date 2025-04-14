#nullable disable

using bsm24.Models;
using CommunityToolkit.Maui.Core.Extensions;
using FFImageLoading.Maui;
using Mopups.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using UraniumUI.Material.Controls;
using UraniumUI.Pages;
using CheckBox = Microsoft.Maui.Controls.CheckBox;

namespace bsm24.Views;

public partial class SetPin : UraniumContentPage, IQueryAttributable
{
    public ObservableCollection<ImageItem> Images { get; set; }
    public int DynamicSpan { get; set; } = 3; // Standardwert
    public int DynamicSize;
    public string PlanId;
    public string PinId;
    public string PinIcon;

    public SetPin()
    {
        InitializeComponent();
        UpdateSpan();
        SizeChanged += OnSizeChanged;
        priorityPicker.PropertyChanged += OnSelectedItemChanged;
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;

        PinImage.Source = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon;

        MyView_Load();
        BindingContext = this;
    }

    private void MyView_Load()
    {
        Images = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos
            .Select(img => new ImageItem
            {
                ImagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, img.Value.File),
                IsChecked = img.Value.IsChecked,
                DateTime = img.Value.DateTime
            })
            .ToObservableCollection();

        priorityPicker.ItemsSource = Settings.PriorityItems.Select(item => item.Key).ToList();
        
        var file = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon;
        if (file.Contains("customicons", StringComparison.OrdinalIgnoreCase))
            file = Path.Combine(Settings.DataDirectory, file);

        this.Title = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName;
        PinDesc.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinDesc;
        PinLocation.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinLocation;
        PinImage.Source = file;
        LockSwitch.IsChecked = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked;
        AllowExport.IsChecked = GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport;
        priorityPicker.SelectedIndex = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority;
        PinAcc.Text = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null ?
                      GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.Accuracy.ToString() :
                      "N/A";

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
        if (MopupService.Instance.PopupStack.Any())
            return;

        var popup = new PopupDualResponse("Wollen Sie diesen Pin wirklich löschen?");
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
            await Shell.Current.GoToAsync($"//{PlanId}?pinDelete={PinId}");
    }

    private async void OnEditClick(object sender, EventArgs e)
    {
        if (MopupService.Instance.PopupStack.Any())
            return;

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
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = this.Title;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinDesc = PinDesc.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinLocation = PinLocation.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked = LockSwitch.IsChecked;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport = AllowExport.IsChecked;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority = priorityPicker.SelectedIndex;

        // save data to file
        GlobalJson.SaveToFile();

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].IsCustomPin)
            await Shell.Current.GoToAsync($"///{PlanId}");
        else
            await Shell.Current.GoToAsync($"///{PlanId}?pinUpdate={PinId}");
    }

    private async void ShowGeoLoc(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"mapview?planId={PlanId}&pinId={PinId}");
    }

    private async void TakePhoto(object sender, EventArgs e)
    {
        (FileResult path, Size imgSize) = await CapturePicture.Capture(Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath), Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath));

        if (path != null)
        {      
            Foto newImageData = new()
            {
                IsChecked = true,
                File = path.FileName,
                DateTime = DateTime.Now,
                ImageSize = imgSize
            };

            var pin = GlobalJson.Data.Plans[PlanId].Pins[PinId];
            pin.Fotos[path.FileName] = newImageData;

            Images.Add(new ImageItem
            {
                ImagePath = path.FullPath,
                IsChecked = true,
                DateTime = DateTime.Now
            });

            // save data to file
            GlobalJson.SaveToFile();
        }
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

    private void OnSelectedItemChanged(object sender, EventArgs e)
    {
        if (sender is PickerField picker)
        {
            var selectedIndex = picker.SelectedIndex;
            if (selectedIndex >= 0)
            {
                priorityPicker.BorderColor = Color.FromArgb(Settings.PriorityItems[selectedIndex].Color);
                priorityPicker.AccentColor = Color.FromArgb(Settings.PriorityItems[selectedIndex].Color);
                priorityLabel.TextColor = Color.FromArgb(Settings.PriorityItems[selectedIndex].Color);
            }
        }
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView).ItemsSource is ObservableCollection<ImageItem> reorderedItems)
        {
            var newFotosDict = reorderedItems
                .ToDictionary(img => Path.GetFileName(img.ImagePath), img => new Foto
                {
                    File = Path.GetFileName(Path.GetFileName(img.ImagePath)),
                    IsChecked = img.IsChecked,
                    DateTime = img.DateTime
                });

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos = newFotosDict;
            GlobalJson.SaveToFile();
        }
    }
}

public class ImageItem
{
    public string ImagePath { get; set; }
    public string PreviewPath { get; set; }
    public bool IsChecked { get; set; }
    public DateTime DateTime { get; set; }
    public int Dpi { get; set; }
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

