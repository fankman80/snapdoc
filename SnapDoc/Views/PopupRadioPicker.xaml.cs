#nullable disable
using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupRadioPicker : Popup<string>
{
    private readonly string _initialSelection;

    public PopupRadioPicker(List<string> items, string currentSelection)
    {
        InitializeComponent();

        BindableLayout.SetItemsSource(RadioGroup, items);

        _initialSelection = currentSelection ?? string.Empty;
        RadioGroup.SelectedItem = currentSelection;
    }

    private async void OnSelectedItemChanged(object sender, EventArgs e)
    {
        if (sender is UraniumUI.Material.Controls.RadioButtonGroupView radioGroup)
        {
            var selectedValue = radioGroup.SelectedItem?.ToString();

            if (selectedValue == _initialSelection)
                return;

            try { await CloseAsync(selectedValue); }
            catch (InvalidOperationException) { }
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }
}