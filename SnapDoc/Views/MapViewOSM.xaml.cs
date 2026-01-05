#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.Widgets.ButtonWidgets;
using Mapsui.Widgets.InfoWidgets;
using Mapsui.Widgets.ScaleBar;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using Image = Microsoft.Maui.Controls.Image;
using Map = Mapsui.Map;
using Point = NetTopologySuite.Geometries.Point;

namespace SnapDoc.Views;

public partial class MapViewOSM : IQueryAttributable
{
    private string PlanId = string.Empty;
    private string PinId = string.Empty;
    private double lon = 8.226692;  // Default: Schweiz
    private double lat = 46.80121;
    private int zoom = 8;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;
    private readonly List<GeometryFeature> _features = [];
    private readonly MapControl mapControl = new();
    private readonly Map map = new();
    //private readonly object? _selectedPinId;
    private bool _mapInitialized = false;

    public MapViewOSM()
    {
        InitializeComponent();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(CreatePinLayer());

        MapControl.Map = map;
        map.Widgets.Clear();
        map.Widgets.Add(new ScaleBarWidget(map) { TextAlignment = Mapsui.Widgets.Alignment.Center, HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left, VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom });
        map.Widgets.Add(new ZoomInOutWidget { Margin = new MRect(20, 40), HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left, VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top });

        MapControl.Map.Tapped += OnMapTapped;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        if (_mapInitialized)
            return;

        _mapInitialized = true;

        await UpdateUiFromQueryAsync();

        foreach (var plan in GlobalJson.Data.Plans)
        {
            foreach (var pin in plan.Value.Pins ?? [])
            {
                if (pin.Value.GeoLocation != null)
                {
                    var loc = pin.Value.GeoLocation.WGS84;
                    AddPin(map, new Point(loc.Longitude, loc.Latitude), plan.Key, pin.Key);
                }
            }
        }

        var center = new MPoint(lon, lat);
        var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(center.X, center.Y).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(sphericalMercatorCoordinate, map.Navigator.Resolutions[zoom]);
    }

    private void AddPin(Map map, Point pos, string planId, string pinId)
    {
        // WGS84 → Spherical Mercator
        var (x, y) = SphericalMercator.FromLonLat(pos.X, pos.Y);
        double scale = (double)SettingsService.Instance.MapIconSize / 100;

        _features.Add(new GeometryFeature
        {
            Geometry = new Point(x, y),
            ["PinId"] = pinId,
            ["PlanId"] = planId,
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

    private async void OnMapTapped(object sender, MapEventArgs e)
    {
        var map = MapControl.Map;
        if (map == null) return;

        // Nur Pins-Layer abfragen
        var pinLayer = map.Layers.FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer == null) return;

        var mapInfo = e.GetMapInfo([pinLayer]);

        if (mapInfo?.Feature is GeometryFeature feature)
        {
            var pinId = feature["PinId"].ToString();
            var planId = feature["PlanId"].ToString();

            var popup = new PopupPinView(planId, pinId);
            var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

            if (result.Result == "edit")
                _mapInitialized = false;
        }
    }

    private void GetCoordinatesClicked(object sender, EventArgs e)
    {

    }
}
