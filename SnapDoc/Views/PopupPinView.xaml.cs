#nullable disable

using CommunityToolkit.Maui.Views;

namespace SnapDoc.Views;

public partial class PopupPinView : Popup<string>
{
    private readonly string planId;
    private readonly string pinId;
    public PinItem Pin { get; set; }

    public PopupPinView(string planId, string pinId)
    {
        InitializeComponent();
        this.planId = planId;
        this.pinId = pinId;
        Pin = new PinItem(GlobalJson.Data.Plans[planId].Pins[pinId]);

        BindingContext = this;
    }

    private async void OnGoToClicked(object sender, EventArgs e)
    {
        await CloseAsync("edit");
        await Shell.Current.GoToAsync($"setpin?planId={planId}&pinId={pinId}");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }
}