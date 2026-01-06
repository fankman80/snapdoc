#nullable disable
using Microsoft.Maui.Controls;

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
        string planId = plan.Key;
        string planTitle = plan.Value.Name;
        string thumbnail = plan.Value.File;

        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        // Neue Plan-Seite
        var newPage = new Views.NewPage(planId)
        {
            Title = planTitle,
            AutomationId = planId,
            PlanId = planId,
        };

        // ShellContent erzeugen
        var shellContent = new ShellContent
        {
            Content = newPage,
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
            Thumbnail = Path.Combine(
                Settings.DataDirectory,
                GlobalJson.Data.ProjectPath,
                GlobalJson.Data.PlanPath,
                "thumbnails",
                thumbnail)
        };

        shell.AllPlanItems.Add(item);
    }

    public static void ResetData()
    {
        (Application.Current.Windows[0].Page as AppShell).PlanItems.Clear();
        (Application.Current.Windows[0].Page as AppShell).AllPlanItems.Clear();

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
