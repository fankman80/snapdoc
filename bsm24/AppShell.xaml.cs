#nullable disable

using bsm24.Services;
using bsm24.Views;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;

namespace bsm24;

public partial class AppShell : Shell
{
    public ObservableCollection<PlanItem> PlanItems { get; set; }

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("open_project", typeof(OpenProject));
        Routing.RegisterRoute("icongallery", typeof(IconGallery));
        Routing.RegisterRoute("setpin", typeof(SetPin));
        Routing.RegisterRoute("imageview", typeof(ImageViewPage));
        Routing.RegisterRoute("project_details", typeof(ProjectDetails));
        Routing.RegisterRoute("loadPdfImages", typeof(LoadPDFPages));
        Routing.RegisterRoute("pinList", typeof(PinList));
        Routing.RegisterRoute("exportSettings", typeof(ExportSettings));
        Routing.RegisterRoute("mapview", typeof(MapView));

        PlanItems = [];

        BindingContext = this;
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var popup = new PopupSettings();
        await this.ShowPopupAsync<string>(popup, Settings.popupOptions);
    }

    public void OnTitleClicked(object sender, EventArgs e)
    {
        if (SettingsService.Instance.IsProjectLoaded)
        {
            var projectDetails = new ProjectDetails();
            projectDetails.OnTitleCaptureClicked(null, null);
        }
    }

    private async void OnNavigateTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
        {
            var parameter = tap.CommandParameter?.ToString();
            if (!string.IsNullOrWhiteSpace(parameter))
            {
#if WINDOWS     
                Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID     
                Shell.Current.FlyoutIsPresented = false;
#endif
                await Shell.Current.GoToAsync(parameter);
            }
        }
    }

    private async void OnPlanTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tap)
        {
            var parameter = tap.CommandParameter?.ToString();
            if (!string.IsNullOrWhiteSpace(parameter))
            {
#if WINDOWS     
                Shell.Current.FlyoutIsPresented = true;
#endif
#if ANDROID     
                Shell.Current.FlyoutIsPresented = false;
#endif
                await Shell.Current.GoToAsync($"//{parameter}");
            }
        }
    }

    private void OnReorderCompleted(object sender, EventArgs e)
    {
        if ((sender as CollectionView).ItemsSource is ObservableCollection<PlanItem> reorderedItems)
        {
            var updatedPlans = reorderedItems.Select(item => item.PlanRoute).ToList();
            AppShell.UpdatePlansOrder(updatedPlans);

            // Speichern
            GlobalJson.SaveToFile();
        }
    }

    private static void UpdatePlansOrder(List<string> updatedPlanOrder)
    {
        var plansList = GlobalJson.Data.Plans.ToList();
        var reorderedPlans = updatedPlanOrder.Select(planRoute =>
        {
            return plansList.FirstOrDefault(p => p.Key == planRoute);
        }).Where(p => p.Key != null).ToList();

        GlobalJson.Data.Plans = reorderedPlans.ToDictionary(p => p.Key, p => p.Value);
    }
}