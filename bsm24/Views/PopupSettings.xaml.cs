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
        colorThemePicker.SelectedValueChanged += OnThemeChanged;
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

    private void OnThemeChanged(object sender, object e)
    {
        Color newColor_root_menu = Application.Current.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["PrimaryDarkText"]
            : (Color)Application.Current.Resources["PrimaryText"];

        Color newColor_plan_menu = Application.Current.RequestedTheme == AppTheme.Dark
            ? (Color)Application.Current.Resources["PrimaryDark"]
            : (Color)Application.Current.Resources["Primary"];

        foreach (var item in (Application.Current.Windows[0].Page as AppShell).Items)
        {
            if (item is FlyoutItem flyoutItem && flyoutItem.Icon is FontImageSource fontIcon)
            {
                if (item.AutomationId == "root_menu")
                    fontIcon.Color = newColor_root_menu;
                else
                    fontIcon.Color = newColor_plan_menu;
            }
        }
    }
}
