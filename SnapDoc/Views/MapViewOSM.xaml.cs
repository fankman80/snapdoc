#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Styles;
using Map = Mapsui.Map;
using Point = NetTopologySuite.Geometries.Point;
using Image = Microsoft.Maui.Controls.Image;

namespace SnapDoc.Views;

public partial class MapViewOSM : IQueryAttributable
{
    public string PlanId = string.Empty;
    public string PinId = string.Empty;
    private double lon = 8.226692;  // Default: Schweiz
    private double lat = 46.80121;
    private int zoom = 8;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;
    private readonly List<GeometryFeature> _features = [];
    private readonly MapControl mapControl = new();
    private readonly Map map = new();

    public MapViewOSM()
    {
        InitializeComponent();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(CreatePinLayer());

        MapControl.Map = map;
        map.Widgets.Clear();
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        await UpdateUiFromQueryAsync();

        foreach (var plan in GlobalJson.Data.Plans)
        {
            foreach (var pin in plan.Value.Pins ?? [])
            {
                if (pin.Value.GeoLocation != null)
                {
                    var loc = pin.Value.GeoLocation.WGS84;
                    AddPin(map, new Point(loc.Longitude, loc.Latitude));
                }
            }
        }

        var center = new MPoint(lon, lat);
        var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(center.X, center.Y).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(sphericalMercatorCoordinate, map.Navigator.Resolutions[zoom]);
    }

    private void AddPin(Map map, Point pos)
    {
        // WGS84 → Spherical Mercator
        var (x, y) = SphericalMercator.FromLonLat(pos.X, pos.Y);
        double scale = (double)SettingsService.Instance.MapIconSize / 100;

        _features.Add(new GeometryFeature
        {
            Geometry = new Point(x, y),
            Styles =
            {
                new ImageStyle
                {
                    Image = ImageStyles.CreatePinStyle().Image, // optional eigener Style
                    SymbolScale = scale,
                    RelativeOffset = new RelativeOffset(0, 0.5)
                }
            }
        });

        var layer = map.Layers.OfType<MemoryLayer>().First(l => l.Name == "Pins");
        layer.FeaturesWereModified();
        layer.DataHasChanged();
    }


    private MemoryLayer CreatePinLayer()
    {
        return new MemoryLayer
        {
            Name = "Pins",
            Features = _features,
            Style = null // wichtig
        };
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out var planIdObj))
            PlanId = planIdObj as string ?? string.Empty;
        if (query.TryGetValue("pinId", out var pinIdObj))
            PinId = pinIdObj as string ?? string.Empty;

        _ = UpdateUiFromQueryAsync(); // fire & forget
    }

    private async Task UpdateUiFromQueryAsync()
    {
        if (!string.IsNullOrEmpty(PlanId) &&
            !string.IsNullOrEmpty(PinId) &&
            GlobalJson.Data.Plans.TryGetValue(PlanId, out var plan) &&
            plan.Pins.TryGetValue(PinId, out var pin))
        {
            var file = pin.PinIcon;
            if (file.Contains("customicons", StringComparison.OrdinalIgnoreCase))
                file = Path.Combine(Settings.DataDirectory, file);
        
            SetPosBtn.IsVisible = true;
            SetPosBtn.FindByName<Image>("SetPosBtnIcon").Source = file;

            if (pin.GeoLocation != null)
            {
                // Zoom auf Pin
                lon = pin.GeoLocation.WGS84.Longitude;
                lat = pin.GeoLocation.WGS84.Latitude;
                zoom = 18;
            }
            else if (SettingsService.Instance.IsGpsActive)
            {
                var location = await geoViewModel.TryGetLocationAsync();
                if (location == null)
                    return;

                // Zoom auf GPS
                lon = location.Longitude;
                lat = location.Latitude;
                zoom = 18;
            }
        }
        else if (SettingsService.Instance.IsGpsActive)
        {
            var location = await geoViewModel.TryGetLocationAsync();
            if (location == null)
                return;

            // Zoom auf GPS (wenn kein Pin)
            lon = location.Longitude;
            lat = location.Latitude;
            zoom = 18;
        }
    }

    private async void SetPosClicked(object sender, EventArgs e)
    {
        if (!SettingsService.Instance.IsGpsActive)
        {
            await ShowGpsDisabledMessageAsync();
            return;
        }

        var location = await geoViewModel.TryGetLocationAsync();
        if (location == null)
            return;

        lon = location.Longitude;
        lat = location.Latitude;
        zoom = 18;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation =
            new GeoLocData(location);
    }

    private static async Task ShowGpsDisabledMessageAsync()
    {
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
            await Application.Current.Windows[0].Page
                .DisplayAlertAsync(
                    AppResources.standortdienste_deaktiviert,
                    AppResources.standortdienste_aktivieren_aufforderung,
                    AppResources.ok);
        else
            await Toast
                .Make(AppResources.standortdienste_aktivieren_aufforderung)
                .Show();
    }

    private async void KmlExportClicked(object sender, EventArgs e)
    {
        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".kml");

        List<(double Latitude, double Longitude, string Name, DateTime Time, string Desc)> coordinates = [];
        foreach (var plan in GlobalJson.Data.Plans)
        {
            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
            {
                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                {
                    if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                    {
                        coordinates.Add((GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.Latitude,
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.Longitude,
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName,
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].DateTime,
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc));
                    }
                }
            }
        }

        KmlGenerator.GenerateKml(outputPath, coordinates);

        var saveStream = File.Open(outputPath, FileMode.Open);
        var fileSaveResult = await FileSaver.Default.SaveAsync(GlobalJson.Data.ProjectPath + ".kml", saveStream);
        if (fileSaveResult.IsSuccessful)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.kml_gespeichert, AppResources.ok);
            else
                await Toast.Make(AppResources.kml_gespeichert).Show();
        }
        else
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.kml_nicht_gespeichert, AppResources.ok);
            else
                await Toast.Make(AppResources.kml_nicht_gespeichert).Show();
        }
        saveStream.Close();

        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private void GetCoordinatesClicked(object sender, EventArgs e)
    {

    }

    private void OnMapLayerColorClicked(object sender, EventArgs e)
    {

    }

    private void OnMapLayerRealClicked(object sender, EventArgs e)
    {

    }
}
