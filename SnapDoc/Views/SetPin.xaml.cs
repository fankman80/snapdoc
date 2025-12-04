#nullable disable

using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace SnapDoc.Views;

public partial class SetPin : ContentPage, IQueryAttributable
{
    private readonly HashSet<Picker> _initializedPickers = [];
    
    public List<string> PinPriorites { get; set; } = [.. SettingsService.Instance.PriorityItems.Select(item => item.Key)];
    public int DynamicSpan { get; set; } = 3;
    public int DynamicSize;

    private string PlanId;
    private string PinId;
    private string SenderView;

    private ObservableCollection<FotoItem> images;
    public ObservableCollection<FotoItem> Images
    {
        get => images;
        set
        {
            if (images != value)
            {
                images = value;
                OnPropertyChanged(nameof(Images));
            }
        }
    }

    private PinItem pin;
    public PinItem Pin
    {
        get => pin;
        set
        {
            if (pin != value)
            {
                pin = value;
                OnPropertyChanged(nameof(Pin));
            }
        }
    }

    public SetPin()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateSpan();
        SizeChanged += OnSizeChanged;;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        SizeChanged -= OnSizeChanged;;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
        if (query.TryGetValue("sender", out object value3))
            SenderView = value3 as string;

        MyView_Load();
    }

    private void MyView_Load()
    {
        Images = GlobalJson.Data.Plans[PlanId].Pins[PinId].Fotos
            .Select(img => new FotoItem
            {
                ImagePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, img.Value.File),
                AllowExport = img.Value.AllowExport,
                DateTime = img.Value.DateTime
            }).ToObservableCollection();
            
        if (GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
            GeoLocButton.Text = Settings.GPSButtonOnIcon;
        else
            GeoLocButton.Text = Settings.GPSButtonUnknownIcon;

        Pin = new PinItem(GlobalJson.Data.Plans[PlanId].Pins[PinId]);
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
        {
            DeletePinData(PinId);
            WeakReferenceMessenger.Default.Send(new PinDeletedMessage(PinId));
            await Shell.Current.GoToAsync($"///{PlanId}");
        }
    }

    private async void OnMoveClick(object sender, EventArgs e)
    {
        var popup = new PopupPlanSelector(PlanId);
        var result = await this.ShowPopupAsync<PlanSelectorReturn>(popup, Settings.PopupOptions);

        if (result.Result == null)
            return;

        await MoveOrCopyPinAsync(PinId, PlanId, result.Result.PlanTarget, result.Result.IsPinCopy);
    }

    private static async Task MoveOrCopyPinAsync(
    string pinId,
    string fromPlanId,
    string toPlanId,
    bool isCopy)
    {
        if (!GlobalJson.Data.Plans.TryGetValue(toPlanId, out Plan toPlan))
            return;

        if (!GlobalJson.Data.Plans.TryGetValue(fromPlanId, out Plan fromPlan))
            return;

        if (!fromPlan.Pins.TryGetValue(pinId, out Pin originalPin))
            return;

        Pin clonedPin = DeepClone(originalPin);

        string newId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        clonedPin.SelfId = newId;
        clonedPin.OnPlanId = toPlanId;

        toPlan.Pins ??= [];
        toPlan.Pins[newId] = clonedPin;
        toPlan.PinCount++;

        if (isCopy)
        {
            clonedPin.Fotos?.Clear();
        }
        else
        {
            fromPlan.Pins.Remove(pinId);
            fromPlan.PinCount--;
            WeakReferenceMessenger.Default.Send(new PinDeletedMessage(pinId));
        }

        GlobalJson.SaveToFile();

        await Shell.Current.GoToAsync($"///{toPlanId}?pinMove={newId}");
    }

    private static T DeepClone<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonSerializer.Deserialize<T>(json);
    }

    private async void OnPinSelectClick(object sender, EventArgs e)
    {
        // save data to file
        GlobalJson.SaveToFile();

        await Shell.Current.GoToAsync($"icongallery?planId={PlanId}&pinId={PinId}&sender=setpin");
    }

    private async void OnOkayClick(object sender, EventArgs e)
    {
        // save data to file
        GlobalJson.SaveToFile();

        SenderView ??= $"//{PlanId}";
        await Shell.Current.GoToAsync($"{SenderView}");
    }

    private void DeletePinData(string pinId)
    {
        // delete all images
        foreach (var del_image in GlobalJson.Data.Plans[PlanId].Pins[pinId].Fotos)
        {
            string file;
            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.Plans[PlanId].Pins[pinId].Fotos[del_image.Key].File);
            if (File.Exists(file))
                File.Delete(file);

            file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, GlobalJson.Data.Plans[PlanId].Pins[pinId].Fotos[del_image.Key].File);
            if (File.Exists(file))
                File.Delete(file);
        }

        // remove custom pin image
        if (GlobalJson.Data.Plans[PlanId].Pins[pinId].IsCustomPin)
        {
            string file = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath, GlobalJson.Data.Plans[PlanId].Pins[pinId].PinIcon);
            if (File.Exists(file))
                File.Delete(file);
        }

        // remove pin from database
        var plan = GlobalJson.Data.Plans[PlanId];
        plan.Pins.Remove(pinId);

        GlobalJson.Data.Plans[PlanId].PinCount -= 1;

        // save data to file
        GlobalJson.SaveToFile();
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
        double imageWidth = SettingsService.Instance.PlanPreviewSize;
        DynamicSpan = Math.Max(3, (int)(screenWidth / imageWidth));
        DynamicSize = (int)(screenWidth / DynamicSpan);
        OnPropertyChanged(nameof(DynamicSpan));
        OnPropertyChanged(nameof(DynamicSize));
    }

    private void OnSelectedItemChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker)
            return;

        if (_initializedPickers.Add(picker) == false)
            Pin.PinPriority = picker.SelectedIndex;
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

    private void OnTitleChanged(object sender, EventArgs e)
    {
        if (sender is not Microsoft.Maui.Controls.Entry entry)
            return;

        // Fokus entfernen
        entry.Unfocus();

#if ANDROID
        try
        {
            if (entry.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var inputMethodManager = nativeView.Context?.GetSystemService(
                    Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;

                // Tastatur schließen
                inputMethodManager?.HideSoftInputFromWindow(nativeView.WindowToken, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Android keyboard hide failed: {ex.Message}");
        }
#endif

#if IOS
        try
        {
            UIKit.UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                if (entry.Handler?.PlatformView is UIKit.UITextField textField)
                {
                    textField.ResignFirstResponder(); // Tastatur schließen
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iOS keyboard hide failed: {ex.Message}");
        }
#endif
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
