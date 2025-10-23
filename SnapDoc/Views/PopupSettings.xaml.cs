#nullable disable

using CommunityToolkit.Maui.Views;
using FFImageLoading.Maui;
using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class PopupSettings : Popup
{
    public SvgCachedImage PinSvgImage;

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