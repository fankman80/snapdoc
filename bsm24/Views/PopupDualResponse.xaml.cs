#nullable disable

using CommunityToolkit.Maui.Views;

namespace bsm24.Views;

public partial class PopupDualResponse : Popup
{
    public string ReturnValue { get; set; }
    private int countdown = 5;
    private readonly string OkText;

    public PopupDualResponse(string title, string okText = "Ok", string cancelText = "Abbrechen", bool alert = false)
	{
		InitializeComponent();
        titleText.Text = title;
        okButtonText.Text = okText;
        cancelButtonText.Text = cancelText;
        OkText = okText;
        if (alert)
            StartTimer();
    }
    private void OnOkClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = "Ok";
        CloseAsync(ReturnValue, cts.Token);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReturnValue = null;
        CloseAsync(ReturnValue, cts.Token);
    }

    private void StartTimer()
    {
        // Button deaktivieren und Countdown-Text anzeigen
        okButtonText.IsEnabled = false;
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
                okButtonText.Text = OkText;
                okButtonText.IsEnabled = true;
            }
        };

        // Timer starten
        timer.Start();
    }
}