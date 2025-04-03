#nullable disable
using bsm24.Views;

namespace bsm24;

public partial class LoadDataToView
{
    public static void LoadData(FileResult path)
    {
        if (path != null && !string.IsNullOrEmpty(path.FullPath))
        {
            if (GlobalJson.Data.Plans != null)
            {
                foreach (var plan in GlobalJson.Data.Plans)
                {
                    string planTitle = GlobalJson.Data.Plans[plan.Key].Name;
                    string planId = plan.Key;

                    var newPage = new Views.NewPage(planId)
                    {
                        Title = planTitle,
                        AutomationId = planId,
                    };

                    var shellContent = new ShellContent
                    {
                        Content = newPage,
                        Route = planId
                    };

                    var newFlyoutItem = new FlyoutItem
                    {
                        Title = planTitle,
                        AutomationId = planId,
                        Icon = new FontImageSource
                        {
                            FontFamily = "MaterialOutlined",
                            Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Layers,
                            Color = Application.Current.RequestedTheme == AppTheme.Dark
                                    ? (Color)Application.Current.Resources["PrimaryDark"]
                                    : (Color)Application.Current.Resources["Primary"]
                        },
                        Items = { shellContent }
                    };

                    (Application.Current.Windows[0].Page as AppShell).Items.Add(newFlyoutItem);
                }
            }
        }
    }

    public static void ResetFlyoutItems()
    {
        // Alle ShellItems durchlaufen und Items entfernen, deren AutomationId nicht null ist
        if (Application.Current.Windows[0].Page is not AppShell appShell) return;

        foreach (var item in appShell.Items.ToList())
        {
            if (item is FlyoutItem flyoutItem)
                appShell.Items.Remove(flyoutItem);
        }

        Helper.AddMenuItem("Projektliste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Folder_open, "OpenProject");
        Helper.AddMenuItem("Projekt Details", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Home_work, "ProjectDetails");
        Helper.AddMenuItem("swisstopo Karte", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Map, "MapView");
        Helper.AddMenuItem("Pin Liste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Format_list_numbered, "PinList");
        Helper.AddMenuItem("Bericht exportieren", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Convert_to_text, "ExportSettings");
        Helper.AddDivider();
    }

    public static void ResetData()
    {
        // Reset Datenbank
        GlobalJson.Data.Client_name = null;
        GlobalJson.Data.Object_address = null;
        GlobalJson.Data.Working_title = null;
        GlobalJson.Data.Object_name = null;
        GlobalJson.Data.Creation_date = DateTime.Now;
        GlobalJson.Data.Project_manager = null;
        GlobalJson.Data.PlanPdf = null;
        GlobalJson.Data.Plans = null;
        GlobalJson.Data.PlanPath = null;
        GlobalJson.Data.ImagePath = null;
        GlobalJson.Data.ThumbnailPath = null;
        GlobalJson.Data.CustomPinsPath = null;
        GlobalJson.Data.ProjectPath = null;
        GlobalJson.Data.JsonFile = null;
    }
}
