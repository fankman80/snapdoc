namespace bsm24;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Setze die Kultur der Anwendung auf Deutsch (Deutschland)
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo("de-DE");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
