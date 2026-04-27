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
        Directory.CreateDirectory(Settings.TemplateDirectory);

        var copyTasks = new List<Task>();

        // Hilfsfunktion zum sicheren Kopieren
        static async Task SafeCopy(string fileName, string targetPath)
        {
            try
            {
                await Helper.CopyFileFromResourcesAsync(fileName, targetPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Kopieren von {fileName}: {ex.Message}");
            }
        }

        string ebbePath = Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx");
        if (!File.Exists(ebbePath))
            copyTasks.Add(SafeCopy("template_ebbe.docx", ebbePath));

        string iconPath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
        if (!File.Exists(iconPath))
            copyTasks.Add(SafeCopy("IconData.xml", iconPath));

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
        // Display-Density speichern
        Settings.DisplayDensity = DeviceDisplay.MainDisplayInfo.Density;

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
