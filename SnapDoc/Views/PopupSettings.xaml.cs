#nullable disable

using CommunityToolkit.Maui.Views;
using FFImageLoading.Maui;
using SnapDoc.Services;
using CommunityToolkit.Maui.Extensions;

namespace SnapDoc.Views;

public partial class PopupSettings : Popup
{
    public SvgCachedImage PinSvgImage;

    private readonly HashSet<Picker> _initializedPickers = [];

    public PopupSettings()
    {
        InitializeComponent();

        darkModePicker.ItemsSource = SettingsService.Instance.AppThemes;
        colorThemePicker.ItemsSource = SettingsService.Instance.ColorThemes;
        darkModePicker.SelectedItem = SettingsService.Instance.SelectedAppTheme;
        colorThemePicker.SelectedItem = SettingsService.Instance.SelectedColorTheme;
        
        string hexColor = ((Color)Application.Current.Resources["Primary"]).ToRgbaHex();
        svgIcon.Source = LoadSvgWithColor("customcolor.svg", hexColor);
    }

    private void OnOkClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.SaveSettings();
        CloseAsync();
    }

    private void OnThemeChanged(object sender, EventArgs e)
    {
        if (sender is not Picker picker)
            return;

        if (_initializedPickers.Add(picker) == false)
        {
            if (Application.Current?.Windows.Count > 0 &&
                Application.Current.Windows[0].Page is AppShell shell)
            {
                shell.RebuildFlyout();
            }
        }
    }

    private async void OpenPrgEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W");
        }
    }

    private async void OpenDocEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.JsonFile);
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W");
        }
    }

    private async void OpenIconEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
        if (File.Exists(filePath))
        {
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W");
        }
    }

    private async void ResetValues(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse("Standardeinstellungen laden?");
        var result = await Application.Current.Windows[0].Page.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
            var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
            if (File.Exists(filePath))
                File.Delete(filePath);

            SettingsService.Instance.ResetSettingsToDefaults();
            SettingsService.Instance.SaveSettings();
        }
    }

    private static string LoadSvgWithColor(string rawFileName, string newColor)
    {
        // Temporären Pfad erzeugen
        string tempPath = Path.Combine(FileSystem.CacheDirectory, $"temp_{newColor.TrimStart('#')}.svg");

        // Wenn bereits vorhanden → direkt zurückgeben
        if (File.Exists(tempPath))
            return tempPath;

        // SVG aus dem Paket laden
        using var stream = FileSystem.OpenAppPackageFileAsync(rawFileName).Result;
        using var reader = new StreamReader(stream);
        string svgText = reader.ReadToEnd();

        // Farbe ersetzen
        svgText = svgText.Replace("#999999", newColor, StringComparison.OrdinalIgnoreCase);

        // Geänderten SVG-Text in Datei schreiben
        File.WriteAllText(tempPath, svgText);

        // FileStream öffnen und zurückgeben
        return tempPath;
    }
}
