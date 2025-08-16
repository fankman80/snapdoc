#nullable disable

using SnapDoc.Messages;
using SnapDoc.Models;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using System.Collections.ObjectModel;
using UraniumUI.Material.Controls;

namespace SnapDoc.Views;

public partial class SetPin : ContentPage, IQueryAttributable
{
    public ObservableCollection<FotoItem> Images { get; set; }
    public int DynamicSpan { get; set; } = 3; // Standardwert
    public int DynamicSize;
    private string PlanId;
    private string PinId;
    private string SenderView;

    private Color priorityColor;
    public Color PriorityColor
    {
        get => priorityColor;
        set
        {
            if (priorityColor != value)
            {
                priorityColor = value;
                OnPropertyChanged(nameof(PriorityColor));
            }
        }
    }

    public SetPin()
    {
        InitializeComponent();
        UpdateSpan();
        SizeChanged += OnSizeChanged;
        priorityPicker.PropertyChanged += OnSelectedItemChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        SizeChanged -= OnSizeChanged;
        priorityPicker.PropertyChanged -= OnSelectedItemChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
        if (query.TryGetValue("sender", out object value3))
            SenderView = value3 as string;

        PinImage.Source = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinIcon;

        MyView_Load();
        BindingContext = this;
    }

    private void MyView_Load()
    {
        Images = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos
            .Select(img => new FotoItem
            {
                ImagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, img.Value.File),
                AllowExport = img.Value.AllowExport,
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
        LockSwitch.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked;
        AllowExport.IsToggled = GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport;
        priorityPicker.SelectedIndex = GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority;

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
        {
            GeoLocButton.ImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Where_to_vote,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["PrimaryText"]
                        : (Color)Application.Current.Resources["PrimaryDarkText"]
            };
        }

        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].IsCustomPin)
        {
            PinImageContainer.IsVisible = false;
        }

        if (priorityPicker.SelectedIndex == 0)
        {
            PriorityColor = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["PrimaryDarkText"]
                        : (Color)Application.Current.Resources["PrimaryText"];
        }
    }

    private async void OnImageTapped(object sender, EventArgs e)
    {
        var tappedImage = sender as Image;
        var filePath = ((FileImageSource)tappedImage.Source).File;
        var fileName = new FileResult(filePath).FileName;

        await Shell.Current.GoToAsync($"imageview?imgSource={fileName}&planId={PlanId}&pinId={PinId}");
    }

    private async void OnDeleteClick(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Wollen Sie diesen Pin wirklich löschen?");
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result != null)
            await Shell.Current.GoToAsync($"///{PlanId}?pinDelete={PinId}");
    }

    private async void OnEditClick(object sender, EventArgs e)
    {
        var popup = new PopupEntry(title: "Pin umbenennen...", inputTxt: GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName);
        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = result.Result;
            this.Title = result.Result;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }

    private async void OnPinSelectClick(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}&sender=setpin");
    }

    private async void OnOkayClick(object sender, EventArgs e)
    {
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinName = this.Title;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinDesc = PinDesc.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinLocation = PinLocation.Text;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].IsLocked = LockSwitch.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].AllowExport = AllowExport.IsToggled;
        GlobalJson.Data.Plans[PlanId].Pins[PinId].PinPriority = priorityPicker.SelectedIndex;

        // save data to file
        GlobalJson.SaveToFile();

        SenderView ??= $"//{PlanId}";
        await Shell.Current.GoToAsync($"{SenderView}");
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
                AllowExport = true,
                File = path.FileName,
                DateTime = DateTime.Now,
                ImageSize = imgSize
            };

            var pin = GlobalJson.Data.Plans[PlanId].Pins[PinId];
            pin.Fotos[path.FileName] = newImageData;

            Images.Add(new FotoItem
            {
                ImagePath = path.FullPath,
                AllowExport = true,
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
                PriorityColor = Color.FromArgb(Settings.PriorityItems[selectedIndex].Color);
        }
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView).ItemsSource is ObservableCollection<FotoItem> reorderedItems)
        {
            var newFotosDict = reorderedItems
                .ToDictionary(img => Path.GetFileName(img.ImagePath), img => new Foto
                {
                    File = Path.GetFileName(Path.GetFileName(img.ImagePath)),
                    AllowExport = img.AllowExport,
                    DateTime = img.DateTime
                });

            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos = newFotosDict;
            GlobalJson.SaveToFile();
        }
    }

    private void OnAllowExportClicked(object sender, EventArgs e)
    {
        var button = sender as Button;

        FotoItem item = (FotoItem)button.BindingContext;

        if (item != null)
        {
            item.AllowExport = !item.AllowExport;

            var fileName = Path.GetFileName(item.ImagePath);
            GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[fileName].AllowExport = !GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos[fileName].AllowExport;

            // save data to file
            GlobalJson.SaveToFile();
        }
    }
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
