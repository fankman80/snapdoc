using UraniumUI;
using bsm24.Services;
using System.IO;
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
        SettingsService.Instance.LoadSettings();

        if (!Directory.Exists(Settings.TemplateDirectory))
        {
            Directory.CreateDirectory(Settings.TemplateDirectory);
            _ = Helper.CopyFileFromResourcesAsync("template_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_ebbe.docx"));
            _ = Helper.CopyFileFromResourcesAsync("template_location_ebbe.docx", Path.Combine(Settings.TemplateDirectory, "template_location_ebbe.docx"));
        }
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