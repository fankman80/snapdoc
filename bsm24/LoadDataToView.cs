#nullable disable

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
                        AutomationId = planId
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
                        Items = { shellContent },
                    };

                    (Application.Current.Windows[0].Page as AppShell).Items.Add(newFlyoutItem);
                }
            }
        }
    }

    public static void ResetFlyoutItems()
    {
        // Alle ShellItems durchlaufen und Items entfernen, deren AutomationId nicht null ist
        foreach (var shellitem in (Application.Current.Windows[0].Page as AppShell).Items.ToList())
        {
            if (shellitem.AutomationId != null)
                (Application.Current.Windows[0].Page as AppShell).Items.Remove(shellitem);
        }

        Helper.AddMenuItem("Projektliste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Folder_open, "OnProjectOpenClicked");
        Helper.AddMenuItem("Projekt Details", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Home_work, "OnProjectDetailsClicked");
        Helper.AddMenuItem("swisstopo Karte", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Map, "OnMapViewClicked");
        Helper.AddMenuItem("Pin Liste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Format_list_numbered, "OnPinListClicked");
        Helper.AddMenuItem("Bericht exportieren", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Convert_to_text, "OnExportClicked");
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
