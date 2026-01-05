#nullable disable

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using Mapsui.Widgets.BoxWidgets;
using Mapsui.Widgets.ButtonWidgets;
using Mapsui.Widgets.InfoWidgets;
using Mapsui.Widgets.ScaleBar;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using Color = Mapsui.Styles.Color;
using Font = Mapsui.Styles.Font;
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
    private readonly Map map = new();
    private bool _mapInitialized = false;
    private bool _isDraggingPin;
    private GeometryFeature _draggedPin;
    private Mapsui.Styles.Image pinImage;
    private readonly TextBoxWidget _instructionWidget;
    private readonly ButtonWidget _toggleButton;
    private readonly RulerWidget _rulerWidget;

    public MapViewOSM()
    {
        InitializeComponent();

        // casting Colors from MAUI to Mapsui
        var c1 = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Primary"];
        var c2 = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["PrimaryDarkText"];
        var widgetColor = new Color((int)(c1.Red * 255), (int)(c1.Green * 255), (int)(c1.Blue * 255));
        var widgetDarkColor = new Color((int)(c2.Red * 255), (int)(c2.Green * 255), (int)(c2.Blue * 255));

        _instructionWidget = CreateInstructionTextBox(AppResources.tippen_ziehen_messen);
        _instructionWidget.Enabled = false; // initial unsichtbar
        _rulerWidget = new RulerWidget // Ruler am Anfang inaktiv
        {
            IsActive = false,
            Color = widgetColor,
            ColorOfBeginAndEndDots = widgetColor
        };
        _toggleButton = new ButtonWidget
        {
            Text = AppResources.distanz_messen,
            VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top,
            HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Right,
            CornerRadius = 4,
            BackColor = widgetColor,
            TextColor = widgetDarkColor,
            Margin = new MRect(10),
            Padding = new MRect(8),
            TextSize = 13,
            WithTappedEvent = (s, e) =>
            {
                _rulerWidget.IsActive = !_rulerWidget.IsActive;
                _instructionWidget.Enabled = _rulerWidget.IsActive;
                e.Map.RefreshGraphics();
            }
        };

        map.Widgets.Clear();

        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(CreatePinLayer());

        map.Widgets.Add(new ScaleBarWidget(map) { MaxWidth = 180, TextAlignment = Mapsui.Widgets.Alignment.Center, Font = new Font { FontFamily = "sans serif", Size = 14 }, HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left, VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom });
        map.Widgets.Add(_instructionWidget);
        map.Widgets.Add(_rulerWidget);
        map.Widgets.Add(_toggleButton);

        MapControl.Map = map;
        MapControl.Map.Tapped += OnMapTapped;
        MapControl.Map.PointerPressed += OnPressed;
        MapControl.Map.PointerMoved += OnMoved;
        MapControl.Map.PointerReleased += OnReleased;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        if (_mapInitialized)
            return;

        _mapInitialized = true;

        await UpdateUiFromQueryAsync();

        // load Pin image with app primary color
        string hexColor = ((Microsoft.Maui.Graphics.Color)Application.Current.Resources["Primary"]).ToRgbaHex();
        string uri = new Uri(Helper.LoadSvgWithColor("customcolor.svg", "#999999", hexColor)).AbsoluteUri;
        pinImage = new Mapsui.Styles.Image
        {
            Source = uri
        };

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
                    Image = pinImage,
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
            Style = null
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

        // 2Pin-Feature in der Map verschieben
        var pinLayer = map.Layers.OfType<MemoryLayer>().FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer != null)
        {
            var feature = pinLayer.Features
                                  .OfType<GeometryFeature>()
                                  .FirstOrDefault(f => f["PinId"]?.ToString() == PinId &&
                                                       f["PlanId"]?.ToString() == PlanId);

            if (feature != null)
            {
                // WGS84 → Spherical Mercator
                var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                feature.Geometry = new Point(x, y);

                // Layer updaten
                pinLayer.FeaturesWereModified();
                pinLayer.DataHasChanged();
            }
        }

        var newCenter = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(newCenter, map.Navigator.Resolutions[zoom]);
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

    private void OnPressed(object sender, MapEventArgs e)
    {
        // Nur Pins-Layer abfragen
        var pinLayer = map.Layers.FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer == null)
            return;

        var mapInfo = e.GetMapInfo([pinLayer]);

        if (mapInfo?.Feature is GeometryFeature feature &&
            feature["PinId"] != null)
        {
            _draggedPin = feature;
            _isDraggingPin = true;

            MapControl.Map.Navigator.PanLock = true; // verhindert Verschieben
        }
    }

    private void OnMoved(object sender, MapEventArgs e)
    {
        if (!_isDraggingPin || _draggedPin == null)
            return;

        var worldPos = MapControl.Map.Navigator.Viewport.ScreenToWorld(new Mapsui.Manipulations.ScreenPosition(e.ScreenPosition.X, e.ScreenPosition.Y));

        _draggedPin.Geometry = new Point(worldPos.X, worldPos.Y);

        var layer = map.Layers.OfType<MemoryLayer>()
                              .First(l => l.Name == "Pins");
        layer.FeaturesWereModified();
        layer.DataHasChanged();
    }

    private void OnReleased(object sender, MapEventArgs e)
    {
        if (!_isDraggingPin)
            return;

        _isDraggingPin = false;
        _draggedPin = null;
       
        MapControl.Map.Navigator.PanLock = false; // Karte wieder aktivieren
    }

    private async void GetCoordinatesClicked(object sender, EventArgs e)
    {
        var pinLayer = map.Layers.OfType<MemoryLayer>()
                                 .FirstOrDefault(l => l.Name == "Pins");

        if (pinLayer == null)
            return;

        foreach (var feature in pinLayer.Features.Cast<GeometryFeature>())
        {
            // Feature-Attribute
            var planKey = feature["PlanId"]?.ToString();
            var pinKey = feature["PinId"]?.ToString();

            if (planKey == null || pinKey == null)
                continue;

            var pin = GlobalJson.Data.Plans[planKey].Pins[pinKey];

            // Geometry ist Spherical Mercator -> zurück zu WGS84
            if (feature.Geometry is Point point)
            {
                var wgs84 = SphericalMercator.ToLonLat(point.X, point.Y);

                // Prüfen, ob sich die Koordinaten geändert haben
                pin.GeoLocation ??= new GeoLocData();

                if (pin.GeoLocation.WGS84.Latitude != wgs84.lat || pin.GeoLocation.WGS84.Longitude != wgs84.lon)
                {
                    pin.GeoLocation.WGS84.Latitude = wgs84.lat;
                    pin.GeoLocation.WGS84.Longitude = wgs84.lon;
                    pin.GeoLocation.Accuracy = 0;

                    await pin.GeoLocation.UpdateCH1903Async(); // optional: CH1903 konvertieren
                }
            }
        }

        // GlobalJson speichern
        GlobalJson.SaveToFile();

        // Feedback an User
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
            await Application.Current.Windows[0].Page.DisplayAlertAsync("", AppResources.pin_positionen_aktualisiert, AppResources.ok);
        else
            await Toast.Make(AppResources.pin_positionen_aktualisiert).Show();
    }

    private static TextBoxWidget CreateInstructionTextBox(string text) => new()
    {
        Text = text,
        TextSize = 13,
        VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top,
        HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Center,
        Margin = new MRect(10),
        Padding = new MRect(8),
        CornerRadius = 4,
        BackColor = new Color(108, 117, 125, 128),
        TextColor = Color.White,
    };
}
