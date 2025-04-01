#nullable disable

using bsm24.Services;
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
        colorThemePicker.PropertyChanged += MapLayerPicker_PropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        darkModePicker.ItemsSource = SettingsService.Instance.AppThemes;
        colorThemePicker.ItemsSource = SettingsService.Instance.ColorThemes;
        darkModePicker.SelectedItem = SettingsService.Instance.SelectedAppTheme;
        colorThemePicker.SelectedItem = SettingsService.Instance.SelectedColorTheme;

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

    private void MapLayerPicker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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