#nullable disable
using SnapDoc.Services;
using UraniumUI;
using static SnapDoc.Helper;
using SnapDoc.ViewModels;
using System.Text.Json;


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
        // Preferences.Clear();
        // Führe eventuell anstehende Migrationen durch
        await ExecutePendingMigrations();

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

    // Statische Instanz der Optionen (Thread-safe und ressourcenschonend)
    private static readonly JsonSerializerOptions MigrationOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private async static Task ExecutePendingMigrations()
    {
        try
        {
            var assembly = typeof(App).Assembly;
            using var stream = assembly.GetManifestResourceStream("SnapDoc.Resources.Raw.migration_tasks.json")!;
            using var reader = new StreamReader(stream);
            string jsonContent = reader.ReadToEnd();

            var manifest = JsonSerializer.Deserialize<MigrationManifest>(jsonContent, MigrationOptions);

            if (manifest == null)
                return;

            int appliedId = Preferences.Get("AppliedMigrationId", 0);

            if (manifest.LastMigrationId != appliedId)
            {
                bool success = await PerformMigrationTask(manifest.TaskName, manifest.Parameters);
                if (success)
                    Preferences.Set("AppliedMigrationId", manifest.LastMigrationId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Migration Error: {ex.Message}");
        }
    }

    private async static Task<bool> PerformMigrationTask(string taskName, MigrationParameters parameters)
    {
        switch (taskName)
        {
            case "DeleteTemplates":
                if (parameters?.Files == null || parameters.Files.Count == 0)
                    return true;

                foreach (var fileName in parameters.Files)
                {
                    try
                    {
                        string filePath = Path.Combine(Settings.TemplateDirectory, fileName);

                        if (File.Exists(filePath))
                            File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                        // Fehler bei einzelner Datei ignorieren, damit andere gelöscht werden können
                    }
                }
                return true;

            // bei Bedarf weitere Cases hinzufügen...

            default:
                return true;
        }
    }
}

public class MigrationManifest
{
    public int LastMigrationId { get; set; }
    public string TaskName { get; set; }
    public MigrationParameters Parameters { get; set; }
}

public class MigrationParameters
{
    public List<string> Files { get; set; }
}
