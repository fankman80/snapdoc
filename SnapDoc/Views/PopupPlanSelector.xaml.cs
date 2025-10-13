#nullable disable

using CommunityToolkit.Maui.Views;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Collections.ObjectModel;

namespace SnapDoc.Views;

public partial class PopupPlanSelector : Popup<string>
{
    public ObservableCollection<PlanItem> PlanItems { get; set; }
    private static string selectedPlan;

    public PopupPlanSelector(string planId, string okText = "Verschieben", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        okButtonText.IsVisible = false;
        cancelButtonText.Text = cancelText;
        labelText.Text = "Ziel auswählen:";

        // Zugriff auf die AppShell
        if (Application.Current.Windows[0].Page is AppShell shell)
            PlanItems = shell.PlanItems;
        else
            PlanItems = [];

        foreach (var item in PlanItems)
            if (item.PlanId != planId)
                item.IsVisible = true;
            else
                item.IsVisible = false;

        BindingContext = this;
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(selectedPlan);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private void OnPlanTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tappedItem)
        {
            // direktes PlanItem aus dem BindingContext
            if (ve.BindingContext is not PlanItem tappedPlan)
                return;

            foreach (var item in PlanItems)
                item.IsVisible = item == tappedPlan;

            PlanCollectionView.ItemsSource = null;
            PlanCollectionView.ItemsSource = PlanItems;

            selectedPlan = tappedItem.CommandParameter?.ToString();

            labelText.Text = "Pin auf folgenden Plan verschieben:";
            okButtonText.IsVisible = true;
        }
    }
}