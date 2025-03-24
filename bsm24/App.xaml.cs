using bsm24.Services;
using UraniumUI;

#if ANDROID
using Android.OS;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
#endif

#if WINDOWS
using Microsoft.UI.Windowing;
#endif

namespace bsm24;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override async void OnStart()
    {
        base.OnStart();

        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");

        // Warten, bis die Speicherberechtigung erteilt wurde
        await EnsureStoragePermissionAsync();

        // Asynchronen Initialisierungsprozess starten
        await InitializeAsync();
    }


    private async static Task EnsureStoragePermissionAsync()
    {
#if ANDROID
        // Auf Android 11+ muss geprüft werden, ob MANAGE_EXTERNAL_STORAGE erteilt wurde.
        while (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Android.OS.Environment.IsExternalStorageManager)
        {
            // Gib dem Benutzer Zeit, die Einstellung zu ändern (oder leite ihn in die Einstellungen)
            await Task.Delay(2000);
        }
#endif

        // Für andere Plattformen oder ältere Android-Versionen
        var status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        while (status != PermissionStatus.Granted)
        {
            await Task.Delay(2000);
            status = await Permissions.RequestAsync<Permissions.StorageWrite>();
        }
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

        // lade Einstellungen
        SettingsService.Instance.LoadSettings();

        // Icon-Daten einlesen
        var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
        SettingsService.Instance.IconCategories = iconCategories;
        Settings.PinData = iconItems;

        Helper.AddMenuItem("Projektliste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Open_in_new, "OnProjectOpenClicked");
    }


    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(UraniumServiceProvider.Current.GetRequiredService<AppShell>())
        {
            Title = "BSM 24 by EBBE"
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
