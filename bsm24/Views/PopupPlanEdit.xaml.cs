#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupPlanEdit : Popup
{
    public (string, string, bool) ReturnValue { get; set; }

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

    private void OnOkClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = (name_entry.Text, desc_entry.Text, allow_export.IsChecked);
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = (null, null, true);
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = ("delete", null, true);
        CloseAsync(ReturnValue, cts.Token);
    }
    private void OnGrayscaleClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = ("grayscale", null, true);
        CloseAsync(ReturnValue, cts.Token);
    }
}
