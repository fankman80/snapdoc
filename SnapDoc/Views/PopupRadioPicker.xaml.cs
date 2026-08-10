#nullable disable
using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupRadioPicker : Popup<string>
{
    private readonly string _initialSelection;

    public PopupRadioPicker(List<string> items, string currentSelection)
    {
        InitializeComponent();

        _initialSelection = currentSelection ?? string.Empty;

        BindableLayout.SetItemsSource(RadioGroupContainer, items);
        RadioButtonGroup.SetSelectedValue(RadioGroupContainer, _initialSelection);
    }

    private async void OnRadioButtonCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton radioButton)
        {
            var selectedValue = radioButton.Value?.ToString();

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