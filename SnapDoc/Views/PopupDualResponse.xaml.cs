#nullable disable

using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupDualResponse : Popup<string>
{
    private int countdown = 5;
    private readonly string OkText;

    public PopupDualResponse(string title, string okText = null, string cancelText = null, bool alert = false)
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText ?? AppResources.ok;
        cancelButtonText.Text = cancelText ?? AppResources.abbrechen;
        OkText = okText;
        if (alert)
            StartTimer();
    }
    private async void OnOkClicked(object sender, EventArgs e)
    {
        await CloseAsync("Ok");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await CloseAsync(null);
    }

    private void StartTimer()
    {
        // Button deaktivieren und Countdown-Text anzeigen
        okButtonText.IsEnabled = false;
        okButtonText.Opacity = 0.5;
        okButtonText.Text = OkText + " ("+ countdown +")";

        // Dispatcher-Timer starten
        var timer = Application.Current.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) =>
        {
            countdown--;
            if (countdown > 0)
            {
                // Aktualisiere den Button-Text
                okButtonText.Text = OkText + " (" + countdown + ")";
            }
            else
            {
                // Timer stoppen, Button aktivieren und Text zurücksetzen
                timer.Stop();
                okButtonText.Opacity = 1.0;
                okButtonText.Text = OkText;
                okButtonText.IsEnabled = true;
            }
        };

        // Timer starten
        timer.Start();
    }
}