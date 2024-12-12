
#nullable disable

using System.Reflection;
using bsm24.Services;

namespace bsm24;

public class Helper
{
    public static void FlyoutItemState(string itemRoute, bool isVisible)
    {
        if ((Application.Current.Windows[0].Page as AppShell).Items
        .SelectMany(item => item.Items) // Alle FlyoutItem/ShellSections durchsuchen
        .SelectMany(section => section.Items) // Alle ShellContent-Items durchsuchen
        .FirstOrDefault(content => content.Route == itemRoute) is ShellContent shellContent)
            shellContent.FlyoutItemIsVisible = isVisible;
    }

    public static void AddMenuItem(string title, string glyph, string methodName)
    {
        var newMenuItem = new MenuItem
        {
            Text = title,
            AutomationId = "990",
            IconImageSource = new FontImageSource
            {
                FontFamily = "MaterialOutlined",
                Glyph = glyph,
                Color = Application.Current.RequestedTheme == AppTheme.Dark
                        ? (Color)Application.Current.Resources["PrimaryDark"]
                        : (Color)Application.Current.Resources["Primary"]
            }
        };

        if (Application.Current.Windows[0].Page is AppShell appShell)
        {
            var methodInfo = appShell.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo != null)
                newMenuItem.Clicked += (s, e) => methodInfo.Invoke(appShell, [s, e]);
            else
                Console.WriteLine($"Methode '{methodName}' wurde nicht gefunden.");
        }

        if (Shell.Current.Items is IList<ShellItem> shellItems)
            shellItems.Add(newMenuItem);
    }

    public static void AddDivider()
    {
        var menuItem = new MenuItem
        {
            Text = "----------- Pläne -----------",
            IsEnabled = false,
            AutomationId = "990",
        };

        if (Shell.Current.Items is IList<ShellItem> shellItems)
            shellItems.Add(menuItem);
    }

    public static void HeaderUpdate()
    {
        // aktualisiere den Header Text
        SettingsService.Instance.FlyoutHeaderTitle = GlobalJson.Data.Object_name;
        SettingsService.Instance.FlyoutHeaderDesc = GlobalJson.Data.Client_name;

        // aktualisiere das Thumbnail Bild
        if (GlobalJson.Data.TitleImage == "banner_thumbnail.png")
            SettingsService.Instance.FlyoutHeaderImage = "banner_thumbnail.png";
        else
            SettingsService.Instance.FlyoutHeaderImage = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.TitleImage);
    }
}
