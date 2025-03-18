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

                    // ShellContent mit explizit gesetzter Route (planId)
                    var shellContent = new ShellContent
                    {
                        Content = newPage,
                        Route = planId  // Hier wird der Routename definiert
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

                    // Registriere die Route für die Seite
                    Routing.RegisterRoute(planId, typeof(Views.NewPage));

                    // Füge den FlyoutItem zum AppShell hinzu
                    (Application.Current.Windows[0].Page as AppShell).Items.Add(newFlyoutItem);
                }
            }
        }
    }


    public static void ResetFlyoutItems()
    {
        // Liste für zu entfernende ShellItems erstellen
        var itemsToRemove = new List<ShellItem>();

        // Alle ShellItems durchlaufen und zu entfernende Items sammeln
        foreach (var shellitem in (Application.Current.Windows[0].Page as AppShell).Items)
        {
            if (shellitem.AutomationId != null)
                itemsToRemove.Add(shellitem);
        }

        // Jetzt die gesammelten Items entfernen, nachdem die Iteration abgeschlossen ist
        foreach (var shellitem in itemsToRemove)
        {
            (Application.Current.Windows[0].Page as AppShell).Items.Remove(shellitem);
        }

        Helper.AddMenuItem("Projektliste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Open_in_new, "OnProjectOpenClicked");
        Helper.AddMenuItem("Projekt Details", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Home_work, "OnProjectDetailsClicked");
        Helper.AddMenuItem("swisstopo Karte", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Map, "OnMapViewClicked");
        Helper.AddMenuItem("Pin Liste", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Format_list_numbered, "OnPinListClicked");
        Helper.AddMenuItem("Bericht exportieren", UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Download, "OnExportClicked");
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
