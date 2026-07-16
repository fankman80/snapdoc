#nullable disable
using CommunityToolkit.Maui.Views;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Views;

public partial class PopupDualResponse : Popup<string>
{
    private int countdown = 5;
    private readonly string OkText;
    private IDispatcherTimer timer;
    private bool _isClosing = false;

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
        if (_isClosing) return;
        _isClosing = true;

        StopTimer();

        try { await CloseAsync("Ok"); }
        catch (InvalidOperationException) {}
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        StopTimer();

        try { await CloseAsync(null); }
        catch (InvalidOperationException) { }
    }

    private void StopTimer()
    {
        timer?.Stop();
        timer = null;
    }

    private void StartTimer()
    {
        okButtonText.IsEnabled = false;
        okButtonText.Opacity = 0.5;
        okButtonText.Text = OkText + " (" + countdown + ")";

        timer = Application.Current.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (s, e) =>
        {
            countdown--;
            if (countdown > 0)
            {
                if (timer != null)
                    okButtonText.Text = OkText + " (" + countdown + ")";
            }
            else
            {
                StopTimer();
                okButtonText.Opacity = 1.0;
                okButtonText.Text = OkText;
                okButtonText.IsEnabled = true;
            }
        };
        timer.Start();
    }
}