#nullable disable
using SnapDoc.Services;
using SnapDoc.Views;

namespace SnapDoc;

public partial class LoadDataToView
{
    public static void LoadData(FileResult path)
    {
        if (path == null || string.IsNullOrEmpty(path.FullPath)) return;
        if (Shell.Current is not AppShell shell) return;
        if (GlobalJson.Data.Plans == null) return;

        foreach (var plan in GlobalJson.Data.Plans)
            AddPlan(plan);

        shell.ApplyFilterAndSorting();
    }

    public static void AddPlan(KeyValuePair<string, Models.Plan> plan)
    {
        if (Shell.Current is not AppShell shell) return;

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
                SettingsService.Instance.ProjectPath,
                GlobalJson.Data.PlanPath,
                "thumbnails",
                plan.Value.File);
        }

        shell.AllPlanItems.Add(item);
    }

    public static void ResetData()
    {
        ClearAllPlansFromShell();

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
    }

    public static void ClearAllPlansFromShell()
    {
        if (Shell.Current is not AppShell shell)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var planIds = shell.AllPlanItems
                    .Where(p => p?.PlanId != null)
                    .Select(p => p.PlanId)
                    .ToHashSet();

                for (int i = shell.Items.Count - 1; i >= 0; i--)
                {
                    var shellItem = shell.Items[i];
                    if (shellItem?.Items == null) continue;

                    for (int j = shellItem.Items.Count - 1; j >= 0; j--)
                    {
                        var section = shellItem.Items[j];
                        if (section?.Items == null) continue;

                        for (int k = section.Items.Count - 1; k >= 0; k--)
                        {
                            var content = section.Items[k];

                            // Pruefen, ob dieser Content zu den Plaenen gehoert
                            if (content?.Route != null && planIds.Contains(content.Route))
                            {
                                try
                                {
                                    section.Items.RemoveAt(k);
                                }
                                catch (Exception)
                                {
                                    // Faengt MAUI-interne Fehler lautlos ab
                                }
                            }
                        }

                        // Wenn die Sektion leer ist, ebenfalls ueber Index entfernen
                        if (section.Items.Count == 0)
                        {
                            try
                            {
                                shellItem.Items.RemoveAt(j);
                            }
                            catch { }
                        }
                    }
                }

                // Daten im Shell-Modell komplett leeren
                shell.PlanItems.Clear();
                shell.AllPlanItems.Clear();
                shell.ApplyFilterAndSorting();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Leeren der Shell-Plaene: {ex.Message}");
            }
        });
    }
}
