#nullable disable
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using SnapDoc.Controls;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using static SnapDoc.Helper;

namespace SnapDoc.Views;

public partial class PopupSettings : Popup, IQueryAttributable
{
    public PopupSettings()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("fileType", out var value))
        {
            if (value as string == "Prg")
                SettingsService.Instance.LoadSettings();
            if (value as string == "Doc")
                GlobalJson.LoadFromFile(Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, SettingsService.DefaultJson));
            if (value as string == "Icon")
            {
                // Icon-Daten einlesen
                Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
                SettingsService.Instance.IconCategories = iconCategories;
                IconLookup.Initialize(Settings.IconData);
            }
        }
    }

    private void OnOkClicked(object sender, EventArgs e)
    {
        SettingsService.Instance.SaveSettings();

        try { CloseAsync(); }
        catch (InvalidOperationException) { }
    }

    private async void OpenPrgEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
        if (File.Exists(filePath))
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Prg");
    }

    private async void OpenDocEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.DataDirectory, SettingsService.Instance.ProjectPath, SettingsService.DefaultJson);
        if (File.Exists(filePath))
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Doc");
    }

    private async void OpenIconEditor(object sender, EventArgs e)
    {
        var filePath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
        if (File.Exists(filePath))
            await Shell.Current.GoToAsync($"xmleditor?file={filePath}&fileMode=W&fileType=Icon");
    }

    private async void ResetPrg(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.standardeinstellungen_laden);
        var result = await Shell.Current.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);

        if (result.Result is DualPopupResult.Ok)
        {
            var filePath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
            if (File.Exists(filePath))
                File.Delete(filePath);

            SettingsService.Instance.ResetSettingsToDefaults();
            SettingsService.Instance.SaveSettings();
        }
    }

    private async void LockSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupLockBadgeSettings();
        var result = await Shell.Current.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
        }
    }

    private async void GestureHelperSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupGestureHelperSettings();
        var result = await Shell.Current.ShowPopupAsync<string>(popup, Settings.PopupOptions);

        if (result.Result != null)
        {
        }
    }

    private async void ResetIcon(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.standardeinstellungen_laden);
        var result = await Shell.Current.ShowPopupAsync<DualPopupResult>(popup, Settings.PopupOptions);

        if (result.Result is DualPopupResult.Ok)
        {
            var filePath = Path.Combine(Settings.TemplateDirectory, "IconData.xml");
            if (File.Exists(filePath))
                File.Delete(filePath);

            await Helper.CopyFileFromResourcesAsync("IconData.xml", Path.Combine(Settings.TemplateDirectory, "IconData.xml"));

            // Icon-Daten einlesen
            Settings.IconData = Helper.LoadIconItems(Path.Combine(Settings.TemplateDirectory, "IconData.xml"), out List<string> iconCategories);
            SettingsService.Instance.IconCategories = iconCategories;
            IconLookup.Initialize(Settings.IconData);
        }
    }

    private async void OnDeleteTileCacheClicked(object sender, EventArgs e)
    {
        try
        {
            int failed = await Task.Run(TileCache.ClearAll);

            await SnackbarExtensions.ShowSafeAsync(AppResources.PlansCacheCleared, includeDelay: true);

            if (failed > 0)
                System.Diagnostics.Debug.WriteLine($"Tile-Cache: {failed} Ordner in Verwendung, übersprungen.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tile-Cache leeren fehlgeschlagen: {ex.Message}");
        }
    }
}
