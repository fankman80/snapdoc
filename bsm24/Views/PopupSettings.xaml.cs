#nullable disable

using bsm24.Services;
using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupSettings : Popup
{
    public string ReturnValue { get; set; }

    public PopupSettings()
    {
        InitializeComponent();

        darkModePicker.ItemsSource = SettingsService.Instance.AppThemes;
        colorThemePicker.ItemsSource = SettingsService.Instance.ColorThemes;
        darkModePicker.SelectedItem = SettingsService.Instance.SelectedAppTheme;
        colorThemePicker.SelectedItem = SettingsService.Instance.SelectedColorTheme;
    }

    private void OnOkClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.SaveSettings();
        Close();
    }
}
