namespace SnapDoc.Controls;

public partial class BusyOverlayPage : ContentPage
{
    public BusyOverlayPage(string? message = null)
    {
        InitializeComponent();

        MessageLabel.Text = string.IsNullOrWhiteSpace(message)
            ? "Bitte warten..."
            : message;
    }

    public void SetMessage(string? message)
    {
        MessageLabel.Text = string.IsNullOrWhiteSpace(message)
            ? "Bitte warten..."
            : message;
    }

    public void UpdateProgress(int current, int total, string? currentFileName = null)
    {
        ProgressBar.IsVisible = total > 0;
        ProgressDetailsLabel.IsVisible = total > 0;

        if (total > 0)
        {
            double percent = (double)current / total;
            ProgressBar.Progress = percent;
            ProgressDetailsLabel.Text = $"{current} von {total} Dateien ({percent:P0})";

            if (!string.IsNullOrWhiteSpace(currentFileName))
            {
                CurrentFileLabel.IsVisible = true;
                CurrentFileLabel.Text = currentFileName;
            }
            else
            {
                CurrentFileLabel.IsVisible = false;
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // Verhindert, dass der Benutzer das BusyOverlay mit der Zurück-Taste schließt.
        return true;
    }
}