#nullable disable

using CommunityToolkit.Maui.Views;
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

        // Kopie der Items von der AppShell
        if (Application.Current.Windows[0].Page is AppShell shell)
            PlanItems = new ObservableCollection<PlanItem>(shell.PlanItems);
        else
            PlanItems = [];

        for (int i = PlanItems.Count - 1; i >= 0; i--)
        {
            if (PlanItems[i].PlanId == planId)
                PlanItems.RemoveAt(i);
        }

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

            for (int i = PlanItems.Count - 1; i >= 0; i--)
            {
                if (PlanItems[i] != tappedPlan)
                    PlanItems.RemoveAt(i);
            }

            PlanCollectionView.ItemsSource = null;
            PlanCollectionView.ItemsSource = PlanItems;

            selectedPlan = tappedItem.CommandParameter?.ToString();

            labelText.Text = "Pin auf folgenden Plan verschieben:";
            okButtonText.IsVisible = true;
        }
    }
}