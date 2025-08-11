namespace SnapDoc.Views;

public partial class HomeScreen : ContentPage
{
    public HomeScreen()
    {
        InitializeComponent();
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }
}
