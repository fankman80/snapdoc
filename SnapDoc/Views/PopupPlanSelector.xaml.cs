#nullable disable

using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc.Views;

public partial class PopupPlanSelector : Popup<PlanSelectorReturn>, INotifyPropertyChanged
{
    private readonly string PlanId;
    private static string selectedPlan;

    private ObservableCollection<PlanItem> _planItems;
    public ObservableCollection<PlanItem> PlanItems
    {
        get => _planItems;
        set
        {
            if (_planItems != value)
            {
                _planItems = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isPlanSelected;
    public bool IsPlanSelected
    {
        get => _isPlanSelected;
        set
        {
            if (_isPlanSelected != value)
            {
                _isPlanSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsNotDuplicateAtLocation => RadioButtonGroup.SelectedIndex == 0 || RadioButtonGroup.SelectedIndex == 1;

    public PopupPlanSelector(string planId)
    {
        InitializeComponent();

        BindingContext = this;

        PlanId = planId;
        RadioButtonGroup.SelectedIndex = 0;

        LoadingPlans();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanSelectorReturn(selectedPlan, RadioButtonGroup.SelectedIndex != 1));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private void OnRadioButtonChanged(object sender, EventArgs e)
    {
        LoadingPlans();

        OnPropertyChanged(nameof(IsNotDuplicateAtLocation));

        if (RadioButtonGroup.SelectedIndex == 2)
        {
            IsPlanSelected = true;
            selectedPlan = PlanId;
        }
        else
            IsPlanSelected = false;
    }

    private void LoadingPlans()
    {
        PlanItems ??= [];

        if (Application.Current.Windows[0].Page is not AppShell shell)
        {
            PlanItems.Clear();
            return;
        }

        var index = RadioButtonGroup.SelectedIndex;

        var filteredPlans = shell.PlanItems.Where(plan =>
        {
            if (index == 2) 
                return plan.PlanId == PlanId;

            return plan.PlanId != PlanId;
        }).ToList();

        PlanItems.Clear();

        foreach (var plan in filteredPlans)
        {
            PlanItems.Add(plan);
        }
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

            selectedPlan = tappedItem.CommandParameter?.ToString();

            IsPlanSelected = true;
        }
    }
}