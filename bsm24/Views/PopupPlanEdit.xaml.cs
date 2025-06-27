#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupPlanEdit : Popup<PlanEditReturn>
{
    public PopupPlanEdit(string name, string desc, bool gray, bool export = true, string okText = "Ok", string cancelText = "Abbrechen")
    {
        InitializeComponent();
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        name_entry.Text = name;
        desc_entry.Text = desc;
        allow_export.IsChecked = export;

        if (gray)
            grayscaleButtonText.Text = "Farben hinzufügen";
        else
            grayscaleButtonText.Text = "Farben entfernen";
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn(name_entry.Text, desc_entry.Text, allow_export.IsChecked));
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn("delete", null, true));
    }
    private async void OnGrayscaleClicked(object sender, EventArgs e)
    {
        await CloseAsync(new PlanEditReturn("grayscale", null, true));
    }
}
