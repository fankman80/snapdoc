#nullable disable
using SnapDoc.Views;

namespace SnapDoc;

public partial class LoadDataToView
{
    public static void LoadData(FileResult path)
    {
        if (path == null || string.IsNullOrEmpty(path.FullPath))
            return;

        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        if (GlobalJson.Data.Plans == null)
            return;

        foreach (var plan in GlobalJson.Data.Plans)
        {
            AddPlan(plan);
        }

        shell.ApplyFilterAndSorting();
    }

    public static void AddPlan(KeyValuePair<string, Models.Plan> plan)
    {
        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        string planId = plan.Key;
        string planTitle = plan.Value.Name;

        // 1. Die richtige Seite basierend auf der ID auswählen
        ContentPage page;
        if (planId.Contains("webmap", StringComparison.OrdinalIgnoreCase))
        {
            page = new MapViewOSM(planId) // Falls du den Konstruktor mit Parameter hast: new MapViewOSM(planId)
            {
                Title = planTitle,
                AutomationId = planId,
                PlanId = planId
            };
        }
        else
        {
            page = new NewPage(planId)
            {
                Title = planTitle,
                AutomationId = planId,
                PlanId = planId
            };
        }

        // 2. ShellContent mit der fertigen Instanz erzeugen
        var shellContent = new ShellContent
        {
            Content = page,      // Hier wird die fertige Seite übergeben
            Route = planId,      // Die planId ist die eindeutige Route
            Title = planTitle,
            AutomationId = planId
        };

        shell.Items.Add(shellContent);

        var item = new PlanItem(plan.Value)
        {
            Title = planTitle,
            PlanId = planId,
            PlanRoute = planId
        };

        if (!planId.Contains("webmap"))
        {
            item.Thumbnail = Path.Combine(
                Settings.DataDirectory,
                GlobalJson.Data.ProjectPath,
                GlobalJson.Data.PlanPath,
                "thumbnails",
                plan.Value.File);
        }

        shell.AllPlanItems.Add(item);
    }

    public static void ResetData()
    {
        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        shell.PlanItems.Clear();
        shell.AllPlanItems.Clear();

        var itemsToRemove = shell.Items.Where(item =>
            item.Items.Any(section => section.Items.Any(content => content.Content is Views.NewPage))
        ).ToList();

        foreach (var item in itemsToRemove)
        {
            shell.Items.Remove(item);
        }

        // Reset Datenbank
        GlobalJson.Data.Client_name = null;
        GlobalJson.Data.Object_address = null;
        GlobalJson.Data.Working_title = null;
        GlobalJson.Data.Project_nr = null;
        GlobalJson.Data.Object_name = null;
        GlobalJson.Data.Creation_date = DateTime.Now;
        GlobalJson.Data.Project_manager = null;
        GlobalJson.Data.Plans = null;
        GlobalJson.Data.PlanPath = null;
        GlobalJson.Data.ImagePath = null;
        GlobalJson.Data.ThumbnailPath = null;
        GlobalJson.Data.CustomPinsPath = null;
        GlobalJson.Data.ProjectPath = null;
        GlobalJson.Data.JsonFile = null;
    }
}
