#nullable disable

using SnapDoc.Services;
using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupSettings : Popup
{
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
        CloseAsync();
    }
}