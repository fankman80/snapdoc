using bsm24.Services;
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

        // Setze die Kultur der Anwendung auf Deutsch (Deutschland)
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");

        //MainPage = UraniumServiceProvider.Current.GetRequiredService<AppShell>();
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
