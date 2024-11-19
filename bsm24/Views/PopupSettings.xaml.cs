#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupSettings : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
    public string ReturnValue { get; set; }

    public PopupSettings()
	{
		InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<string>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }
    private async void OnExportSettingsClicked(object sender, EventArgs e)
    {
        // Show Settings Page
        var popup = new PopupExportSettings();
        await MopupService.Instance.PushAsync(popup);
        var result = await popup.PopupDismissedTask;
        if (result != null)
        {

        }
    }
    private void OnThemePickerChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        string selectedTheme = picker.SelectedItem.ToString();

        switch (selectedTheme)
        {
            case "Light":
                App.Current.UserAppTheme = AppTheme.Light; // Setze auf helles Theme
                break;
            case "Dark":
                App.Current.UserAppTheme = AppTheme.Dark; // Setze auf dunkles Theme
                break;
            case "System Default":
                App.Current.UserAppTheme = AppTheme.Unspecified; // Verwende das systemweite Theme
                break;
        }
    }

    private void OnColorSchemeChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;
        string selectedScheme = picker.SelectedItem.ToString();

        // Dynamische Farbwerte basierend auf der Auswahl des Benutzers ‰ndern
        switch (selectedScheme)
        {
            case "Blue-Orange":
                Application.Current.Resources["PrimaryColor"] = Color.FromArgb("#2196F3"); // Blau
                Application.Current.Resources["SecondaryColor"] = Color.FromArgb("#FF5722"); // Orange
                Application.Current.Resources["BackgroundColor"] = Color.FromArgb("#FFFFFF"); // Weiﬂ
                break;

            case "Green-Purple":
                Application.Current.Resources["PrimaryColor"] = Color.FromArgb("#4CAF50"); // Gr¸n
                Application.Current.Resources["SecondaryColor"] = Color.FromArgb("#9C27B0"); // Lila
                Application.Current.Resources["BackgroundColor"] = Color.FromArgb("#FFFFFF"); // Weiﬂ
                break;

            case "Red-Yellow":
                Application.Current.Resources["PrimaryColor"] = Color.FromArgb("#F44336"); // Rot
                Application.Current.Resources["SecondaryColor"] = Color.FromArgb("#FFEB3B"); // Gelb
                Application.Current.Resources["BackgroundColor"] = Color.FromArgb("#FFFFFF"); // Weiﬂ
                break;
        }
    }
}