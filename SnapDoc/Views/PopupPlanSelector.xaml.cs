#nullable disable
using CommunityToolkit.Maui.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SnapDoc.Views;

public partial class PopupPlanSelector : Popup<PlanSelectorReturn>, INotifyPropertyChanged
{
    private readonly string PlanId;
    private string selectedPlan;
    private int selectedRadioButtonIndex;

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

    public bool IsNotDuplicateAtLocation => selectedRadioButtonIndex == 0 || selectedRadioButtonIndex == 1;
    public PopupPlanSelector(string planId)
    {
        InitializeComponent();

        BindingContext = this;

        PlanId = planId;
        selectedRadioButtonIndex = 0;

        LoadingPlans();
    }

    private int _selectedPlanOption = 0;
    public int SelectedPlanOption
    {
        get => _selectedPlanOption;
        set
        {
            if (_selectedPlanOption != value)
            {
                _selectedPlanOption = value;
                selectedRadioButtonIndex = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotDuplicateAtLocation));

                HandleOptionChanged();
            }
        }
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(new PlanSelectorReturn(selectedPlan, selectedRadioButtonIndex != 1)); }
        catch (InvalidOperationException) { }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private void HandleOptionChanged()
    {
        LoadingPlans();

        if (selectedRadioButtonIndex == 2)
        {
            IsPlanSelected = true;
            selectedPlan = PlanId;
        }
        else
        {
            IsPlanSelected = false;
        }
    }

    private void LoadingPlans()
    {
        PlanItems ??= [];

        if (Shell.Current is not AppShell shell)
        {
            PlanItems.Clear();
            return;
        }

        var index = selectedRadioButtonIndex;

        var filteredPlans = shell.PlanItems.Where(plan =>
        {
            if (plan.PlanId != null && plan.PlanId.Contains("webmap", StringComparison.OrdinalIgnoreCase))
                return false;

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
            if (ve.BindingContext is not PlanItem tappedPlan) return;

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
