#nullable disable

using Mopups.Pages;
using Mopups.Services;
using bsm24.Services;

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

        darkModePicker.ItemsSource = SettingsService.Instance.DarkMode;
        colorThemePicker.ItemsSource = SettingsService.Instance.Themes;
        darkModePicker.SelectedItem = SettingsService.Instance.SelectedDarkMode;
        colorThemePicker.SelectedItem = SettingsService.Instance.SelectedTheme;

        _taskCompletionSource = new TaskCompletionSource<string>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.SaveSettings();
        await MopupService.Instance.PopAsync();
    }

    private void OnSelectedValueChanged(object sender, EventArgs e)
    {
        // Hole die AppShell
        var appShell = (AppShell)Application.Current.Windows[0].Page;

        foreach (var item in appShell.Items)
        {
            // Überprüfen und aktualisieren des `Icon`
            if (item is FlyoutItem flyoutItem && flyoutItem.Icon is FontImageSource fontIcon)
            {
                fontIcon.Color = (Color)(Application.Current.RequestedTheme == AppTheme.Dark
                                ? Application.Current.Resources["PrimaryDark"]
                                : Application.Current.Resources["Primary"]);
            }

            // Überprüfen und aktualisieren des `FlyoutIcon`
            if (item is BaseShellItem baseShellItem && baseShellItem.FlyoutIcon is FontImageSource flyoutFontIcon)
            {
                flyoutFontIcon.Color = (Color)(Application.Current.RequestedTheme == AppTheme.Dark
                                ? Application.Current.Resources["PrimaryDark"]
                                : Application.Current.Resources["Primary"]);
            }
        }
    }
}