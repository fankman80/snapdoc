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
        bool isWebMap = planId.Contains("webmap", StringComparison.OrdinalIgnoreCase);

        ContentPage page;
        if (isWebMap)
        {
            page = new MapView(planId)
            {
                Title = planTitle,
                AutomationId = planId,
            };
        }
        else
        {
            page = new NewPage(planId)
            {
                Title = planTitle,
                AutomationId = planId,
            };
        }

        var shellContent = new ShellContent
        {
            Content = page,
            Route = planId,
            Title = planTitle,
            AutomationId = planId
        };

        shell.Items.Add(shellContent);

        var item = new PlanItem(plan.Value)
        {
            Title = planTitle,
            PlanId = planId,
            PlanRoute = planId,
            IsWebMapPlan = isWebMap
        };

        if (!isWebMap)
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
        AppShell.ClearAllPlansFromShell();

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
