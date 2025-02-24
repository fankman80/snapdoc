#nullable disable

using Mopups.Pages;
using Mopups.Services;

namespace bsm24.Views;

public partial class PopupDualResponse : PopupPage
{
    TaskCompletionSource<string> _taskCompletionSource;
    public Task<string> PopupDismissedTask => _taskCompletionSource.Task;
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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _taskCompletionSource = new TaskCompletionSource<string>();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _taskCompletionSource.SetResult(ReturnValue);
    }

    private async void PopupPage_BackgroundClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
    }

    private async void OnOkClicked(object sender, EventArgs e)
    {
        ReturnValue = "Ok";
        await MopupService.Instance.PopAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        ReturnValue = null;
        await MopupService.Instance.PopAsync();
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