#nullable disable
using SnapDoc.Services;
using UraniumUI;
using static SnapDoc.Helper;
using SnapDoc.ViewModels;

#if ANDROID
using Android.Hardware.Display;
using Android.Content;
#endif

#if WINDOWS
using Microsoft.UI.Windowing;
#endif

#if IOS
using UIKit;
#endif

namespace SnapDoc;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        await InitializeAsync();
    }

    private async static Task InitializeAsync()
    {
        // Template-Dateien und Konfigurationsdatei kopieren
        if (!Directory.Exists(Settings.TemplateDirectory))
            Directory.CreateDirectory(Settings.TemplateDirectory);

        var copyTasks = new List<Task>();

        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx")))
            copyTasks.Add(Helper.CopyFileFromResourcesAsync("template_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx")));

        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "template_location_ebbe.docx")))
            copyTasks.Add(Helper.CopyFileFromResourcesAsync("template_location_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_location_ebbe.docx")));

        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "IconData.xml")))
            copyTasks.Add(Helper.CopyFileFromResourcesAsync("IconData.xml", Path.Combine(Settings.TemplateDirectory, "IconData.xml")));

        if (!File.Exists(Path.Combine(Settings.DataDirectory, "appsettings.ini")))
            SettingsService.Instance.SaveSettings();

        // Warte, bis alle Kopiervorgänge abgeschlossen sind
        await Task.WhenAll(copyTasks);

        // Icon-Daten einlesen
        Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
        SettingsService.Instance.IconCategories = iconCategories;
        IconLookup.Initialize(Settings.IconData);

        // lade Einstellungen
        SettingsService.Instance.LoadSettings();

        // prüfe GPS-Verfügbarkeit
        var location = await GeolocationViewModel.Instance.TryGetLocationAsync();
        if (location == null)
            SettingsService.Instance.IsGpsActive = false;
        else
            SettingsService.Instance.IsGpsActive = true;

        // ermittle die Höhe der Navigationsleiste (für CustomPinOffset)
        double bottomInset = 0;
#if ANDROID
        var displayManager = Android.App.Application.Context.GetSystemService(Context.DisplayService) as DisplayManager;
        var defaultdisplay = displayManager.GetDisplay(Android.Views.Display.DefaultDisplay);
        var gotheight = Android.App.Application.Context.CreateDisplayContext(defaultdisplay).Resources.DisplayMetrics.HeightPixels;

        // this is the status bar height
        var statusid = Platform.CurrentActivity.ApplicationContext.Resources.GetIdentifier("status_bar_height", "dimen", "android");
        var statusbarheight = Platform.CurrentActivity.ApplicationContext.Resources.GetDimensionPixelSize(statusid);

        // this is the navigation bar height
        var navid = Platform.CurrentActivity.ApplicationContext.Resources.GetIdentifier("navigation_bar_height", "dimen", "android");
        var navbarheight = Platform.CurrentActivity.ApplicationContext.Resources.GetDimensionPixelSize(navid);

        bottomInset = navbarheight / DeviceDisplay.MainDisplayInfo.Density;
#endif

#if IOS
        var window = UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault()?
            .Windows
            .FirstOrDefault(w => w.IsKeyWindow);

        var safeAreaBottom = window?.SafeAreaInsets.Bottom ?? 0;

        bottomInset = safeAreaBottom;
#endif

        SettingsService.Instance.CustomPinOffset = new Point(0, -bottomInset / 2);

        // Einstellungen speichern
        SettingsService.Instance.SaveSettings();
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = new Window(UraniumServiceProvider.Current.GetRequiredService<AppShell>())
        {
            Title = "SnapDoc"
        };

#if WINDOWS
        window.HandlerChanged += (sender, args) =>
        {
            if (window.Handler?.PlatformView is MauiWinUIWindow w)
            {
                var presenter = (w.AppWindow.Presenter as OverlappedPresenter);
           }
        };
#endif
        return window;
    }
}
