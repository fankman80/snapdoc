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

    protected override bool OnBackButtonPressed()
    {
        // Verhindert, dass der Benutzer das BusyOverlay mit der Zurück-Taste schließt.
        return true;
    }
}