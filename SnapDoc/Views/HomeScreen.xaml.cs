using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class HomeScreen : ContentPage
{
    private readonly AuthService _authService = new();

    public HomeScreen() 
    {
        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    private async void OnSharepointClicked(object sender, EventArgs e)
    {
        // Entpackt das Tupel (bool, string)
        var (success, message) = await _authService.LoginAndFetchUserAsync();

        if (success)
        {
            await DisplayAlertAsync("Erfolg", $"Willkommen, {message}!", "OK");
        }
        else
        {
            // Zeigt im Fehlerfall die genaue Fehlermeldung aus der Exception an
            await DisplayAlertAsync("Fehler beim Login", message, "OK");
        }
    }
}
