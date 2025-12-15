#nullable disable
namespace SnapDoc;

public partial class LoadDataToView
{
    public static void LoadData(FileResult path)
    {
        if (path == null || string.IsNullOrEmpty(path.FullPath))
            return;

        if (GlobalJson.Data.Plans == null)
            return;

        foreach (var plan in GlobalJson.Data.Plans)
        {
            AddPlan(plan);
        }
    }

    public static void AddPlan(KeyValuePair<string, Models.Plan> plan)
    {
        string planId = plan.Key;
        string planTitle = plan.Value.Name;
        string thumbnail = plan.Value.File;

        // Neue Plan-Seite mit Übergabe der ID erstellen
        var newPage = new Views.NewPage(planId)
        {
            Title = planTitle,
            AutomationId = planId,
            PlanId = planId,
        };

        // ShellContent erzeugen und mit eindeutiger Route versehen
        var shellContent = new ShellContent
        {
            Content = newPage,
            Route = planId,
            Title = planTitle
        };

        // Seite zur Shell dynamisch hinzufügen
        (Application.Current.Windows[0].Page as AppShell).Items.Add(shellContent);

        // PlanItem für ein Flyout- oder Menü-Item hinzufügen
        (Application.Current.Windows[0].Page as AppShell).PlanItems.Add(new PlanItem(GlobalJson.Data.Plans[plan.Key])
        {
            Title = planTitle,
            PlanId = planId,
            PlanRoute = planId,
            Thumbnail = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", thumbnail)
        });
    }

    public static void ResetData()
    {
        (Application.Current.Windows[0].Page as AppShell).PlanItems.Clear();

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
