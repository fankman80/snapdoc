using SnapDoc.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.Views;

public partial class HomeScreen : ContentPage, INotifyPropertyChanged
{
    private readonly AuthService _authService = new();
    private bool _isLoggedIn;
    private string _currentuserName = string.Empty;
    public string CurrentUserName
    {
        get => _currentuserName;
        set { _currentuserName = value; OnPropertyChanged(); }
    }
    private string _currentUserEmail = string.Empty;
    public string CurrentUserEmail
    {
        get => _currentUserEmail;
        set { _currentUserEmail = value; OnPropertyChanged(); }
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set
        {
            _isLoggedIn = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotLoggedIn)); // Gegenteil für den Login-Button
        }
    }

    public bool NotLoggedIn => !IsLoggedIn;

    public HomeScreen(AuthService authService) 
    {
        InitializeComponent();
        _authService = authService;
        BindingContext = this;
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    private async void OnSharepointClicked(object sender, EventArgs e)
    {
        var (success, userName, userEmail) = await _authService.LoginAndFetchUserAsync();

        if (success)
        {
            // Speichere den Service global für den SaveManager
            SaveManager.CurrentAuth = _authService;

            // UI-Status aktualisieren
            CurrentUserName = userName; // Enthält den Anzeigenamen des Users
            CurrentUserEmail = userEmail; // Enthält die E-Mail-Adresse des Users
            IsLoggedIn = true;

            await DisplayAlertAsync("Erfolg", $"Eingeloggt als: {userName}", "OK");
        }
        else
        {
            await DisplayAlertAsync("Fehler", $"Login fehlgeschlagen: {userName}", "OK");
        }
    }

    private void OnLogoutClicked(object sender, EventArgs e)
    {
        SaveManager.CurrentAuth = null;
        IsLoggedIn = false;
        CurrentUserName = string.Empty;
        CurrentUserEmail = string.Empty;
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
    }
}
