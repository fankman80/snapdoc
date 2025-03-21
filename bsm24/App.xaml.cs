using bsm24.Services;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using UraniumUI;
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

    protected override void OnStart()
    {
        base.OnStart();

        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");

        // Template-Dateien und Konfigurationsdatei kopieren
        if (!Directory.Exists(Settings.TemplateDirectory))
            Directory.CreateDirectory(Settings.TemplateDirectory);
        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx")))
            _ = Helper.CopyFileFromResourcesAsync("template_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx"));
        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "template_location_ebbe.docx")))
            _ = Helper.CopyFileFromResourcesAsync("template_location_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_location_ebbe.docx"));
        if (!File.Exists(Path.Combine(Settings.TemplateDirectory, "IconData.xml")))
            _ = Helper.CopyFileFromResourcesAsync("IconData.xml", Path.Combine(Settings.TemplateDirectory, "IconData.xml"));
        if (!File.Exists(Path.Combine(FileSystem.AppDataDirectory, "appsettings.ini")))
            SettingsService.Instance.SaveSettings();

        // lade Einstellungen
        SettingsService.Instance.LoadSettings();

        // Icon-Daten einlesen
        var iconItems = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"));
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
