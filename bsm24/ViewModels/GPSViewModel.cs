#nullable disable
using GeolocatorPlugin;
using GeolocatorPlugin.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace bsm24.ViewModels;
public partial class GPSViewModel : INotifyPropertyChanged
{
    public static GPSViewModel Instance { get; } = new GPSViewModel();
    private const double MIN_MARK_INTERVAL = 1.0d;
    public event PropertyChangedEventHandler PropertyChanged;
    private string _gpsData;
    private FontImageSource _gpsButtonIcon;
    private double _lon;
    private double _lat;
    private double _acc;
    public string GPSData
    {
        get { return _gpsData; }
        set { _gpsData = value; OnPropertyChanged(nameof(GPSData)); }
    }
    public double Lon
    {
        get { return _lon; }
        set { _lon = value; OnPropertyChanged(nameof(Lon)); }
    }
    public double Lat
    {
        get { return _lat; }
        set { _lat = value; OnPropertyChanged(nameof(Lat)); }
    }
    public double Acc
    {
        get { return _acc; }
        set { _acc = value; OnPropertyChanged(nameof(Acc)); }
    }
    public bool IsRunning { get; set; }
    public FontImageSource GPSButtonIcon
    {
        get { return _gpsButtonIcon; }
        set { _gpsButtonIcon = value; OnPropertyChanged(nameof(GPSButtonIcon)); }
    }
    public Command ToggleGPSCommand { get; set; }

    private GPSViewModel()
    {
        ToggleGPSCommand = new Command(OnToggleGPS);
        GPSButtonIcon = new FontImageSource
        {
            FontFamily = "MaterialOutlined",
            Glyph = UraniumUI.Icons.MaterialSymbols.MaterialOutlined.Location_off,
            Color = Application.Current.RequestedTheme == AppTheme.Dark
                    ? (Color)Application.Current.Resources["PrimaryDark"]
                    : (Color)Application.Current.Resources["Primary"],
            Size=24
        };
    }

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnToggleGPS(object obj)
    {
        await Toggle(!IsRunning);

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

    /// <summary>
    /// Activates/Deactives the gps device
    /// </summary>
    /// <param name="isOn"></param>
    public async Task<bool> Toggle(bool isOn)
    {
        BasePermission gpsPermission = new LocationWhenInUse();
        var hasPermission = await Utils.CheckPermissions(gpsPermission, true);
        if (!hasPermission)
            return false;

        if (!isOn)
        {
            if (await CrossGeolocator.Current.StopListeningAsync())
            {
                CrossGeolocator.Current.PositionChanged -= CrossGeolocator_Current_PositionChanged;
                CrossGeolocator.Current.PositionError -= CrossGeolocator_Current_PositionError;
            }
            GPSData = string.Empty;
            IsRunning = false;
        }
        else
        {
            float minTime = .5f;
            if (await CrossGeolocator.Current.StartListeningAsync(TimeSpan.FromSeconds(minTime), MIN_MARK_INTERVAL, true, new ListenerSettings
            {
                ActivityType = ActivityType.AutomotiveNavigation,
                AllowBackgroundUpdates = true,
                DeferLocationUpdates = false,
                ListenForSignificantChanges = false,
                PauseLocationUpdatesAutomatically = false,
                ShowsBackgroundLocationIndicator = true,
            }))
            {
                CrossGeolocator.Current.PositionChanged += CrossGeolocator_Current_PositionChanged;
                CrossGeolocator.Current.PositionError += CrossGeolocator_Current_PositionError;
            }

            IsRunning = true;
        }

        return true;
    }


    /// <summary>
    /// Handles Position Changed events from the plugin
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossGeolocator_Current_PositionChanged(object sender, PositionEventArgs e)
    {
        GPSData = string.Format("Time: {0} \nLat: {1} \nLong: {2} \nAltitude: {3} \nAltitude Accuracy: {4} \nAccuracy: {5} \nHeading: {6} \nSpeed: {7}",
            e.Position.Timestamp,
            e.Position.Latitude,
            e.Position.Longitude,
            e.Position.Altitude,
            e.Position.AltitudeAccuracy,
            e.Position.Accuracy,
            e.Position.Heading,
            e.Position.Speed);

        Lon = e.Position.Longitude;
        Lat = e.Position.Latitude;
        Acc = e.Position.Accuracy;

        IsRunning = true;
    }

    /// <summary>
    /// Handles position errors
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossGeolocator_Current_PositionError(object sender, PositionErrorEventArgs e)
    {
        Console.WriteLine(e.Error.ToString());
    }

    /// <summary>
    /// Returns the Last Known Location of the device
    /// </summary>
    /// <returns></returns>
    internal async Task<Position> GetLastKnownLocation()
    {
        BasePermission gpsPermission = new LocationWhenInUse();
        var hasPermission = await Utils.CheckPermissions(gpsPermission, true);
        if (hasPermission)
        {
            var position = await CrossGeolocator.Current.GetLastKnownLocationAsync();
            CrossGeolocator_Current_PositionChanged(this, new PositionEventArgs(position));
            return position;
        }
        return null;
    }
}
