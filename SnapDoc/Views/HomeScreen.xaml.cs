using SnapDoc.Services;

namespace SnapDoc.Views;

public partial class HomeScreen : ContentPage
{
    private readonly AuthService _authService;

    public HomeScreen(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }
}