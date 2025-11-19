#nullable disable

using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc.Views;

public partial class PopupPlanSelector : Popup<PlanSelectorReturn>, INotifyPropertyChanged
{
    public ObservableCollection<PlanItem> PlanItems { get; set; }
    private static string selectedPlan;

    string actionText = "Verschieben";
    public string ActionText
    {
        get => actionText;
        set
        {
            if (actionText != value)
            {
                actionText = value;
                OnPropertyChanged();
            }
        }
    }

    private string infoText = "Ziel auswählen:";
    public string InfoText
    {
        get => infoText;
        set
        {
            if (infoText != value)
            {
                infoText = value;
                OnPropertyChanged();
            }
        }
    }

    public PopupPlanSelector(string planId, string cancelText = "Abbrechen")
    {
        InitializeComponent();

        okButtonText.IsVisible = false;
        cancelButtonText.Text = cancelText;

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
        await CloseAsync(new PlanSelectorReturn(selectedPlan, copyCheckBox.IsChecked));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private void OnCheckChanged(object sender, EventArgs e)
    {
        ActionText = copyCheckBox.IsChecked ? "Kopieren" : "Verschieben";

        if (okButtonText.IsVisible)
            InfoText = $"Pin auf folgenden Plan {ActionText.ToLower()}:";
    }

    private void OnPlanTapped(object sender, EventArgs e)
    {
        if (sender is Grid ve && ve.GestureRecognizers.FirstOrDefault() is TapGestureRecognizer tappedItem)
        {
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

            InfoText = $"Pin auf folgenden Plan {ActionText.ToLower()}:";

            okButtonText.IsVisible = true;
        }
    }
}