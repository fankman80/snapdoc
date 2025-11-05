#nullable disable
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using SnapDoc.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnapDoc.ViewModels;

public partial class GPSViewModel : INotifyPropertyChanged
{
    public static GPSViewModel Instance { get; } = new GPSViewModel();
    public event PropertyChangedEventHandler PropertyChanged;

    // 🔹 Event für Standortänderungen
    public event Action<Location> LocationUpdated;

    private string _gpsData;
    private FontImageSource _gpsButtonIcon;
    private double _lon;
    private double _lat;
    private double _acc;

    public string GPSData
    {
        get => _gpsData;
        set { _gpsData = value; OnPropertyChanged(); }
    }

    public double Lon
    {
        get => _lon;
        set { _lon = value; OnPropertyChanged(); }
    }

    public double Lat
    {
        get => _lat;
        set { _lat = value; OnPropertyChanged(); }
    }

    public double Acc
    {
        get => _acc;
        set { _acc = value; OnPropertyChanged(); }
    }

    public bool IsRunning { get; set; }

    public FontImageSource GPSButtonIcon
    {
        get => _gpsButtonIcon;
        set { _gpsButtonIcon = value; OnPropertyChanged(); }
    }

    private string _gpsButtonText = "AUS";
    public string GPSButtonText
    {
        get => _gpsButtonText;
        set { _gpsButtonText = value; OnPropertyChanged(); }
    }

    public Command ToggleGPSCommand { get; }

    private CancellationTokenSource _gpsToken;

    private GPSViewModel()
    {
        ToggleGPSCommand = new Command(OnToggleGPS); // 🔹 Command initialisieren
        UpdateGPSButtonIcon();
    }

    private async void OnToggleGPS(object obj)
    {
        GPSButtonText = IsRunning ? "AUS" : "laden..."; // Text sofort setzen
        UpdateGPSButtonIcon();

        await Toggle(!IsRunning);
    }

    private void UpdateGPSButtonIcon()
    {
        GPSButtonIcon = new FontImageSource
        {
            FontFamily = "MaterialOutlined",
            Glyph = IsRunning
                    ? UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Where_to_vote
                    : UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Location_off,
            Color = Application.Current.RequestedTheme == AppTheme.Dark
                    ? (Color)Application.Current.Resources["PrimaryDark"]
                    : (Color)Application.Current.Resources["Primary"],
            Size = 24
        };
    }

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Aktiviert/deaktiviert die Standortabfrage.
    /// </summary>
    public async Task<bool> Toggle(bool isOn)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            GPSData = "Standortberechtigung verweigert.";
            GPSButtonText = "AUS";
            return false;
        }

        if (!isOn)
        {
            _gpsToken?.Cancel();
            _gpsToken = null;
            GPSData = string.Empty;
            IsRunning = false;
            GPSButtonText = "AUS"; // 🔹 Hier auf AUS setzen
        }
        else
        {
            _gpsToken = new CancellationTokenSource();
            IsRunning = true;
            GPSButtonText = "laden..."; // 🔹 Text sofort ändern

            // 🔹 Sofort letzte bekannte Position laden
            await LoadLastKnownLocationAsync();

            // 🔹 Danach dauerhaft GPS-Updates starten
            _ = Task.Run(() => StartListeningAsync(_gpsToken.Token));
        }

        return true;
    }

    /// <summary>
    /// Lädt die letzte bekannte Position (sofort, ohne neue Abfrage)
    /// </summary>
    private async Task LoadLastKnownLocationAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync();
            if (location != null)
            {
                Lat = location.Latitude;
                Lon = location.Longitude;
                Acc = location.Accuracy ?? 0;
                GPSData = $"Letzter Standort: {Lat:F6}, {Lon:F6} (±{Acc:F1}m)";
                GPSButtonText = "AN";
                LocationUpdated?.Invoke(location);
            }
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Wiederholte Standortabfrage
    /// </summary>
    private async Task StartListeningAsync(CancellationToken token)
    {
        var request = new GeolocationRequest(
            GeolocationAccuracy.Best,
            TimeSpan.FromSeconds(SettingsService.Instance.GpsMinTimeUpdate));

        while (!token.IsCancellationRequested)
        {
            try
            {
                Location location = null;

                // 🔹 Wiederholen, bis GPS einen Fix liefert
                while (!token.IsCancellationRequested && location == null)
                {
                    location = await Geolocation.Default.GetLocationAsync(request, token);
                    if (location == null)
                        await Task.Delay(1000, token);
                }

                if (location != null)
                {
                    Lat = location.Latitude;
                    Lon = location.Longitude;
                    Acc = location.Accuracy ?? 0;

                    GPSData = string.Format(
                        "Zeit: {0}\nLat: {1:F6}\nLon: {2:F6}\nGenauigkeit: {3:F1} m",
                        location.Timestamp.LocalDateTime,
                        Lat,
                        Lon,
                        Acc);

                    // 🔹 Event feuern
                    LocationUpdated?.Invoke(location);
                }
            }
            catch (TaskCanceledException)
            {
                // Task wurde gestoppt -> ignorieren
            }
            catch (Exception ex)
            {
                GPSData = $"Fehler: {ex.Message}";
            }

            // 🔹 Wartezeit zwischen Updates
            await Task.Delay(TimeSpan.FromSeconds(SettingsService.Instance.GpsMinTimeUpdate), token);
        }
    }
}