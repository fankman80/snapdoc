using SnapDoc.Services;
using UraniumUI;
using static SnapDoc.Helper;
using SnapDoc.ViewModels;

#if ANDROID
using Android.OS;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
#endif

#if WINDOWS
using Microsoft.UI.Windowing;
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

        // Einstellungen speichern
        SettingsService.Instance.SaveSettings();
    }

    protected override Window CreateWindow(IActivationState? activationState)
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
