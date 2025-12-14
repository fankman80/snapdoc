#nullable disable
using SnapDoc.Services;
using System.ComponentModel;
using System.Windows.Input;

namespace SnapDoc.ViewModels;

public partial class GeolocationViewModel : BaseViewModel
{
    public static GeolocationViewModel Instance { get; } = new GeolocationViewModel();
    private readonly string notAvailable = "not available";
    private CancellationTokenSource cts;
    private string _gpsButtonIcon;
    private Location _lastKnownLocation;

    public GeolocationViewModel()
    {
        ToggleGPSCommand = new Command(async () => await OnToggleGPSAsync());

        SettingsService.Instance.PropertyChanged += Settings_PropertyChanged;

        UpdateGPSButtonIcon();
    }

    public bool IsGpsActive
    {
        get => SettingsService.Instance.IsGpsActive;
        set
        {
            if (SettingsService.Instance.IsGpsActive != value)
            {
                SettingsService.Instance.IsGpsActive = value;
                OnPropertyChanged();
                UpdateGPSButtonIcon();

                // Einstellungen speichern
                SettingsService.Instance.SaveSettings();
            }
        }
    }

    public static bool IsListening => Geolocation.IsListeningForeground;
    public static bool IsNotListening => !IsListening;

    public String GPSButtonIcon
    {
        get => _gpsButtonIcon;
        set => SetProperty(ref _gpsButtonIcon, value);
    }

    public Location LastKnownLocation
    {
        get => _lastKnownLocation;
        private set => SetProperty(ref _lastKnownLocation, value);
    }

    private string _listeningLocationStatus;
    public string ListeningLocationStatus
    {
        get => _listeningLocationStatus;
        set => SetProperty(ref _listeningLocationStatus, value);
    }

    private string _listeningLocation;
    public string ListeningLocation
    {
        get => _listeningLocation;
        set => SetProperty(ref _listeningLocation, value);
    }

    public ICommand ToggleGPSCommand { get; }

    // ----------------------------------------------------------------------
    // 🔹 Ein/Aus-Logik für GPS
    // ----------------------------------------------------------------------
    public async Task OnToggleGPSAsync()
    {
        if (IsGpsActive)
        {
            IsGpsActive = false;
            StopListening();
            return;
        }

        // Berechtigung prüfen
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlertAsync(
                    "GPS deaktiviert",
                    "Standortberechtigung wurde nicht erteilt. GPS kann nicht aktiviert werden.",
                    "OK");
                return;
            }
        }

        // System-GPS prüfen
        if (!await IsSystemGpsEnabledAsync())
        {
            bool openSettings = await Shell.Current.DisplayAlertAsync(
                "GPS ist deaktiviert",
                "Um die Standorterkennung zu aktivieren, bitte GPS in den Systemeinstellungen einschalten.",
                "Einstellungen öffnen",
                "Abbrechen");

#if ANDROID
            if (openSettings)
            {
                var intent = new Android.Content.Intent(Android.Provider.Settings.ActionLocationSourceSettings);
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                Android.App.Application.Context.StartActivity(intent);
            }
#endif
            return;
        }

        // jetzt erst aktivieren
        IsGpsActive = true;
        await StartListeningAsync();

        if (LastKnownLocation == null)
        {
            var location = await GetCurrentLocationAsync();
            if (location != null)
                LastKnownLocation = location;
        }
    }

    private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.IsGpsActive))
        {
            OnPropertyChanged(nameof(IsGpsActive));
            UpdateGPSButtonIcon();
        }
    }

    private void UpdateGPSButtonIcon()
    {
        GPSButtonIcon = IsGpsActive ? Settings.GPSButtonOnIcon : Settings.GPSButtonOffIcon;
    }

    private async static Task<bool> IsSystemGpsEnabledAsync()
    {
#if ANDROID
    try
    {
        var locationManager = (Android.Locations.LocationManager)
            Android.App.Application.Context.GetSystemService(Android.Content.Context.LocationService);
        return locationManager.IsProviderEnabled(Android.Locations.LocationManager.GpsProvider)
            || locationManager.IsProviderEnabled(Android.Locations.LocationManager.NetworkProvider);
    }
    catch
    {
        return false;
    }
#else
        await Task.CompletedTask;
        return true; // Unter Windows immer true, da kein System-GPS-Schalter
#endif
    }

    // ----------------------------------------------------------------------
    // 🔹 Aktuelle Position einmalig abfragen, nur wenn GPS aktiv ist
    // ----------------------------------------------------------------------
    public async Task<Location> GetCurrentLocationAsync()
    {
        if (!SettingsService.Instance.IsGpsActive)
            return null;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                    return null;
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(SettingsService.Instance.GpsResponseTimeOut));
            cts = new CancellationTokenSource();
            var location = await Geolocation.Default.GetLocationAsync(request, cts.Token);

            if (location != null)
                LastKnownLocation = location;

            return location;
        }
        catch (Exception ex)
        {
            ListeningLocationStatus = $"Fehler bei Standortabfrage: {ex.Message}";
            return null;
        }
        finally
        {
            cts?.Dispose();
            cts = null;
        }
    }

    // ----------------------------------------------------------------------
    // 🔹 Kontinuierliches Tracking starten
    // ----------------------------------------------------------------------
    public async Task StartListeningAsync()
    {
        try
        {
            if (IsListening)
                return;

            Geolocation.LocationChanged += Geolocation_LocationChanged;

            var request = new GeolocationListeningRequest(GeolocationAccuracy.Best)
            {
                MinimumTime = TimeSpan.FromSeconds(SettingsService.Instance.GpsMinTimeUpdate)
            };

            var success = await Geolocation.StartListeningForegroundAsync(request);
            ListeningLocationStatus = success ? "GPS aktiv" : "GPS konnte nicht gestartet werden";

            OnPropertyChanged(nameof(IsListening));
            OnPropertyChanged(nameof(IsNotListening));
        }
        catch (Exception ex)
        {
            ListeningLocationStatus = $"Fehler beim Starten: {ex.Message}";
        }
    }

    // ----------------------------------------------------------------------
    // 🔹 Tracking stoppen
    // ----------------------------------------------------------------------
    public void StopListening()
    {
        try
        {
            Geolocation.LocationChanged -= Geolocation_LocationChanged;
            Geolocation.StopListeningForeground();
            ListeningLocationStatus = "GPS gestoppt";
        }
        catch (Exception ex)
        {
            ListeningLocationStatus = $"Fehler beim Stoppen: {ex.Message}";
        }

        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(IsNotListening));
    }

    // ----------------------------------------------------------------------
    // 🔹 Bei jeder Standortänderung automatisch aktualisieren
    // ----------------------------------------------------------------------
    private void Geolocation_LocationChanged(object sender, GeolocationLocationChangedEventArgs e)
    {
        LastKnownLocation = e.Location;
        ListeningLocation = FormatLocation(e.Location);
    }

    private string FormatLocation(Location location)
    {
        if (location == null)
            return notAvailable;

        return
            $"Latitude: {location.Latitude:F6}\n" +
            $"Longitude: {location.Longitude:F6}\n" +
            $"Accuracy: {location.Accuracy:F1} m\n" +
            $"Altitude: {(location.Altitude?.ToString("F1") ?? notAvailable)} m\n" +
            $"Speed: {(location.Speed?.ToString("F1") ?? notAvailable)} m/s\n" +
            $"Time: {location.Timestamp:T}";
    }

    public override void OnDisappearing()
    {
        cts?.Cancel();
        StopListening();
        base.OnDisappearing();
    }
}
