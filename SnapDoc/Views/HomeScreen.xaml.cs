using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class HomeScreen : ContentPage
{
    public HomeScreen(AuthService authService)
    {
        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }
}
