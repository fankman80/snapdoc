#nullable disable
using BruTile.Predefined;
using BruTile.Web;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.Messaging;
using DocumentFormat.OpenXml.InkML;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Maui;
using Mapsui.Widgets.BoxWidgets;
using Mapsui.Widgets.ScaleBar;
using SkiaSharp;
using SnapDoc.Messages;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using System.Diagnostics;
using Color = Mapsui.Styles.Color;
using Font = Mapsui.Styles.Font;
using Map = Mapsui.Map;
using Point = NetTopologySuite.Geometries.Point;
using Size = Microsoft.Maui.Graphics.Size;

namespace SnapDoc.Views;

public partial class MapView : IQueryAttributable
{
    private readonly string planId;
    private string pinId = string.Empty;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;
    private readonly List<GeometryFeature> _features = [];
    private readonly Map map = new();
    private bool _mapInitialized = false;
    private bool _isDraggingPin;
    private GeometryFeature _draggedPin;
    private Mapsui.Styles.Image pinImage;
    private readonly TextBoxWidget _instructionWidget;
    private readonly Microsoft.Maui.Graphics.Color hexColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Primary"];
    private readonly MemoryLayer _measureLayer = new() { Name = "MeasureLayer" };
    private readonly List<GeometryFeature> _measureFeatures = [];
    private readonly List<MPoint> _measurePoints = [];
    private int _draggedMeasurePointIndex = -1;
    private bool _isPolygonClosed = false;
    private readonly Color _widgetColor;

    private PinItem pin;
    public PinItem Pin
    {
        get => pin;
        set
        {
            if (pin != value)
            {
                pin = value;
                OnPropertyChanged(nameof(Pin));
            }
        }
    }

    public MapView(string _planId)
    {
        InitializeComponent();
        BindingContext = this;
        planId = _planId;

        _widgetColor = new Color((int)(hexColor.Red * 255), (int)(hexColor.Green * 255), (int)(hexColor.Blue * 255));

        _instructionWidget = CreateInstructionTextBox(AppResources.tippen_und_ziehen_messvorgang);
        _instructionWidget.Enabled = false; // initial unsichtbar
        _instructionWidget.BackColor = new Color(0, 0, 0, 180);

        _measureLayer.Features = _measureFeatures;
        _measureLayer.Style = null;

        LayerPicker.ItemsSource = Settings.SwissTopoLayers;
        LayerPicker.SelectedIndex = 1;

        map.Layers.Add(CreateSwissTopoLayer("ch.swisstopo.pixelkarte-farbe"));
        map.Layers.Add(CreatePinLayer());
        map.Layers.Add(_measureLayer);

        map.Widgets.Clear();
        map.Widgets.Add(new ScaleBarWidget(map) { MaxWidth = 180, Margin = new MRect(8), TextAlignment = Mapsui.Widgets.Alignment.Center, Font = new Font { FontFamily = "sans serif", Size = 14 }, HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left, VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom });
        map.Widgets.Add(_instructionWidget);

        MapControl.Map = map;
        map.Tapped += OnMapTapped;
        map.PointerPressed += OnPressed;
        map.PointerMoved += OnMoved;
        map.PointerReleased += OnReleased;

        WeakReferenceMessenger.Default.Register<PinDeletedMessage>(this, (r, m) =>
        {
            var pinIdToDelete = m.Value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var pinLayer = map?.Layers
                    .OfType<MemoryLayer>()
                    .FirstOrDefault(l => l.Name == "Pins");
                if (pinLayer != null)
                {
                    var featureToRemove = _features
                        .FirstOrDefault(f => f["PinId"]?.ToString() == pinIdToDelete);

                    if (featureToRemove != null)
                    {
                        _features.Remove(featureToRemove);
                        pinLayer.Features = [.. _features.Cast<IFeature>()];
                        pinLayer.DataHasChanged();
                        MapControl.RefreshGraphics();
                    }
                }
            });
        });
    }

    protected override bool OnBackButtonPressed()
    {
        // Zurück-Taste ignorieren
        return true;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        var appShell = Application.Current.Windows[0].Page as AppShell;
        appShell?.HighlightCurrentPlan(planId);

        if (!_mapInitialized)
        {
            _mapInitialized = true;
            LoadPins();
            await UpdateUiFromQueryAsync();
        }
    }

    private void AddPin(Map map, Point pos, string planId, string pinId)
    {
        var (x, y) = SphericalMercator.FromLonLat(pos.X, pos.Y);
        double scale = (double)SettingsService.Instance.MapIconSize / 100.0;

        var newFeature = new GeometryFeature
        {
            Geometry = new Point(x, y),
            ["PinId"] = pinId,
            ["PlanId"] = planId,
            Styles = { new ImageStyle { Image = pinImage, SymbolScale = scale, RelativeOffset = new RelativeOffset(0, 0.5) } }
        };

        _features.Add(newFeature);

        var layer = map.Layers.OfType<MemoryLayer>().FirstOrDefault(l => l.Name == "Pins");
        if (layer != null)
        {
            layer.Features = [.. _features.Cast<IFeature>()];
            layer.DataHasChanged();
            MapControl.RefreshGraphics();
        }
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

    private async void LoadPins()
    {
        _features.Clear();
        var pinLayer = map.Layers.OfType<MemoryLayer>().FirstOrDefault(l => l.Name == "Pins");
        pinLayer?.Features = [];

        string uri = SettingsService.Instance.MapIcons[SettingsService.Instance.MapIcon];
        if (uri == "themeColorPin")
            uri = new Uri(Helper.LoadSvgWithColor("customcolor.svg", "#999999", hexColor.ToRgbaHex())).AbsoluteUri;
        else
        {
            await MauiResourceLoader.CopyAppPackageFileAsync(Settings.CacheDirectory, uri);
            uri = new Uri(Path.Combine(Settings.CacheDirectory, uri)).AbsoluteUri;
        }

        pinImage = new Mapsui.Styles.Image { Source = uri };

        var plansToSearch = string.IsNullOrEmpty(planId)
            ? GlobalJson.Data.Plans
            : GlobalJson.Data.Plans.Where(p => p.Key == planId);

        foreach (var planEntry in plansToSearch)
        {
            var currentPlanId = planEntry.Key;
            var pins = planEntry.Value.Pins ?? [];
            foreach (var pinEntry in pins)
            {
                var p = pinEntry.Value;
                if (p.GeoLocation?.WGS84 != null)
                {
                    var loc = p.GeoLocation.WGS84;
                    AddPin(map, new Point(loc.Longitude, loc.Latitude), currentPlanId, pinEntry.Key);
                }
            }
        }
        MapControl.RefreshGraphics();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        string newPinId = string.Empty;
        if (query.TryGetValue("pinZoom", out var pinIdObj))
            newPinId = pinIdObj as string ?? string.Empty;

        if (newPinId != pinId)
        {
            pinId = newPinId;
            _ = UpdateUiFromQueryAsync();
        }
    }

    private async Task UpdateUiFromQueryAsync()
    {
        SetPosBtn.IsVisible = false;

        double targetLon = 8.226692;
        double targetLat = 46.80121;
        int targetZoom = 8; // Default Zoom für die Übersicht

        if (!string.IsNullOrEmpty(planId) && !string.IsNullOrEmpty(pinId))
        {
            if (GlobalJson.Data.Plans.TryGetValue(planId, out var plan) &&
                plan.Pins.TryGetValue(pinId, out var pinData))
            {
                Pin = new PinItem(pinData);
                SetPosBtn.IsVisible = true;

                if (pinData.GeoLocation?.WGS84 != null)
                {
                    targetLon = pinData.GeoLocation.WGS84.Longitude;
                    targetLat = pinData.GeoLocation.WGS84.Latitude;
                    targetZoom = 18; // Nah ran
                }
                else if (SettingsService.Instance.IsGpsActive)
                {
                    var location = await geoViewModel.TryGetLocationAsync();
                    if (location != null)
                    {
                        targetLon = location.Longitude;
                        targetLat = location.Latitude;
                        targetZoom = 18; // Auch hier nah ran
                    }
                }
            }
        }
        else if (SettingsService.Instance.IsGpsActive)
        {
            var location = await geoViewModel.TryGetLocationAsync();
            if (location != null)
            {
                targetLon = location.Longitude;
                targetLat = location.Latitude;
                targetZoom = 18;
            }
        }

        if (map?.Navigator != null)
        {
            var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(targetLon, targetLat).ToMPoint();
            map.Navigator.CenterOnAndZoomTo(sphericalMercatorCoordinate, map.Navigator.Resolutions[targetZoom]);
            MapControl.RefreshGraphics();
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

        await UpdateAndSavePinLocationAsync(planId, pinId, location.Longitude, location.Latitude);

        var pinLayer = map.Layers.OfType<MemoryLayer>().FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer != null)
        {
            var feature = pinLayer.Features
                                  .OfType<GeometryFeature>()
                                  .FirstOrDefault(f => f["PinId"]?.ToString() == pinId &&
                                                       f["PlanId"]?.ToString() == planId);

            if (feature != null)
            {
                var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                feature.Geometry = new Point(x, y);

                pinLayer.FeaturesWereModified();
                pinLayer.DataHasChanged();
            }
            else
            {
                AddPin(map, new Point(location.Longitude, location.Latitude), planId, pinId);
            }
        }

        var newCenter = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(newCenter, map.Navigator.Resolutions[18]);
    }

    private void OnRulerClicked(object sender, EventArgs e)
    {
        bool isActivating = RulerButton.Text != AppResources.abbrechen;
        _measurePoints.Clear();
        _draggedMeasurePointIndex = -1;
        _isPolygonClosed = false;

        _instructionWidget.Enabled = isActivating;
        RulerButton.Text = isActivating ? AppResources.abbrechen : AppResources.vermessung;

        if (!isActivating)
        {
            _measureFeatures.Clear();
            _measureLayer.FeaturesWereModified();
            _instructionWidget.Text = AppResources.tippen_und_ziehen_messvorgang;
        }
        MapControl.RefreshGraphics();
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
        if (map == null)
            return;

        // Nur Pins-Layer abfragen
        var pinLayer = map.Layers.FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer == null)
            return;

        var mapInfo = e.GetMapInfo([pinLayer]);

        if (mapInfo?.Feature is GeometryFeature feature)
        {
            var pinId = feature["PinId"].ToString();
            var planId = feature["PlanId"].ToString();
            var popup = new PopupPinView(planId, pinId);

            PopupOptions popupOptions = new()
            { CanBeDismissedByTappingOutsideOfPopup = true, Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(8), StrokeThickness = 0 } };
            var result = await this.ShowPopupAsync<string>(popup, popupOptions);

            if (result.Result == "edit")
                LoadPins();

            if (result.Result == "export")
            {
                var imageSize = new Size(2000, 2000);
                var imageBytes = await ExportMapAsImageAsync(imageSize, feature.Geometry.Centroid);

                if (imageBytes == null || imageBytes.Length == 0)
                    return;

                string filename = $"MAP_IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string folderPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath);
                string thumbFolderPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath);
                string filepath = Path.Combine(folderPath, filename);
                string thumbPath = Path.Combine(thumbFolderPath, filename);

                // 1. Hauptbild-Ordner aufräumen
                if (Directory.Exists(folderPath))
                {
                    var files = Directory.GetFiles(folderPath, "MAP_IMG_*.jpg");
                    foreach (var file in files) // Sicherer: Alle alten MAP_IMG löschen
                    {
                        try
                        {
                            File.Delete(file);
                            GlobalJson.Data.Plans[planId].Pins[pinId].Fotos.Remove(Path.GetFileName(file));
                        }
                        catch { /* Optional: Logging */ }
                    }
                }

                // 2. Thumbnail-Ordner aufräumen
                if (Directory.Exists(thumbFolderPath)) // Wichtig: Eigene Prüfung für den Thumb-Ordner
                {
                    var thumbFiles = Directory.GetFiles(thumbFolderPath, "MAP_IMG_*.jpg");
                    foreach (var file in thumbFiles)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* Optional: Logging */ }
                    }
                }



                await File.WriteAllBytesAsync(filepath, imageBytes);

                Thumbnail.Generate(filepath, thumbPath);

                Foto newImageData = new()
                {
                    AllowExport = true,
                    File = filename,
                    DateTime = DateTime.Now,
                    ImageSize = imageSize
                };
              
                GlobalJson.Data.Plans[planId].Pins[pinId].Fotos[filename] = newImageData;
                GlobalJson.SaveToFile();
            }
        }
    }

    private void OnPressed(object sender, MapEventArgs e)
    {
        var worldPos = map.Navigator.Viewport.ScreenToWorld(new Mapsui.Manipulations.ScreenPosition(e.ScreenPosition.X, e.ScreenPosition.Y));

        if (RulerButton.Text == AppResources.abbrechen)
        {
            double tolerance = map.Navigator.Viewport.Resolution * 15;
            int hitIndex = _measurePoints.FindIndex(p => p.Distance(worldPos) < tolerance);

            if (hitIndex == 0 && _measurePoints.Count >= 3)
            {
                _isPolygonClosed = true;
                _draggedMeasurePointIndex = 0; // Erlaubt das Verschieben des "Schlusspunkts"
            }
            else if (hitIndex != -1)
            {
                _draggedMeasurePointIndex = hitIndex;
            }
            else if (!_isPolygonClosed) // Nur neue Punkte hinzufügen, wenn noch nicht geschlossen
            {
                _measurePoints.Add(worldPos);
                _draggedMeasurePointIndex = _measurePoints.Count - 1;
            }

            map.Navigator.PanLock = true;
            UpdateMeasureLayer();
            return;
        }

        var pinLayer = map.Layers.FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer == null)
            return;

        var mapInfo = e.GetMapInfo([pinLayer]);
        if (mapInfo?.Feature is GeometryFeature feature && feature["PinId"] != null)
        {
            _draggedPin = feature;
            _isDraggingPin = true;

            map.Navigator.PanLock = true; // verhindert Verschieben
        }
    }

    private void OnMoved(object sender, MapEventArgs e)
    {
        var worldPos = map.Navigator.Viewport.ScreenToWorld(
            new Mapsui.Manipulations.ScreenPosition(e.ScreenPosition.X, e.ScreenPosition.Y));

        if (RulerButton.Text == AppResources.abbrechen && _draggedMeasurePointIndex != -1)
        {
            _measurePoints[_draggedMeasurePointIndex] = worldPos;
            UpdateMeasureLayer();
            return;
        }

        if (_isDraggingPin && _draggedPin != null)
        {
            _draggedPin.Geometry = new Point(worldPos.X, worldPos.Y);
            MapControl.RefreshGraphics();
            return;
        }
    }

    private async void OnReleased(object sender, MapEventArgs e)
    {
        map.Navigator.PanLock = false;

        if (_isDraggingPin && _draggedPin != null)
        {
            try
            {
                var planKey = _draggedPin["PlanId"]?.ToString();
                var pinKey = _draggedPin["PinId"]?.ToString();

                if (planKey != null && pinKey != null && _draggedPin.Geometry is Point point)
                {
                    var (lon, lat) = SphericalMercator.ToLonLat(point.X, point.Y);
                    await UpdateAndSavePinLocationAsync(planKey, pinKey, lon, lat);

                    var pinLayer = map.Layers.OfType<MemoryLayer>().FirstOrDefault(l => l.Name == "Pins");
                    pinLayer?.FeaturesWereModified();
                    pinLayer?.DataHasChanged();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Speichern: {ex.Message}");
            }
        }
        _draggedMeasurePointIndex = -1;
        CleanupDragging();
        MapControl.RefreshGraphics();
    }

    private void CleanupDragging()
    {
        _isDraggingPin = false;
        _draggedPin = null;
        map.Navigator.PanLock = false;
    }

    private void UpdateMeasureLayer()
    {
        _measureFeatures.Clear();
        if (_measurePoints.Count < 1)
            return;

        var coordinates = _measurePoints.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToList();

        foreach (var p in _measurePoints)
        {
            _measureFeatures.Add(new GeometryFeature(new Point(p.X, p.Y))
            {
                Styles = { new SymbolStyle { SymbolScale = 0.5,
                                             Fill = new Mapsui.Styles.Brush(Color.White),
                                             Outline = new Pen(Color.Black, 1) } }
            });
        }

        string measurementResult = "";
        if (_isPolygonClosed && _measurePoints.Count >= 3)
        {
            var (lon, lat) = SphericalMercator.ToLonLat(_measurePoints[0].X, _measurePoints[0].Y);
            double cosLat = Math.Cos(lat * Math.PI / 180.0);
            var ringCoords = new List<NetTopologySuite.Geometries.Coordinate>(coordinates) { coordinates[0] };
            var polygon = new NetTopologySuite.Geometries.Polygon(new NetTopologySuite.Geometries.LinearRing([.. ringCoords]));
            var polygonFeature = new GeometryFeature(polygon);
            polygonFeature.Styles.Add(new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush(new Color(_widgetColor.R, _widgetColor.G, _widgetColor.B, 128)),
                Line = new Pen(Color.Black, 1)
            });
            _measureFeatures.Add(polygonFeature);

            double realArea = polygon.Area * (cosLat * cosLat);
            measurementResult = realArea >= 1000000
                ? $"{(realArea / 1000000.0):0.##} km²"
                : $"{realArea:N1} m²";
        }
        else if (_measurePoints.Count > 1)
        {
            var lineFeature = new GeometryFeature(new NetTopologySuite.Geometries.LineString([.. coordinates]));
            lineFeature.Styles.Add(new VectorStyle
            {
                Line = new Pen { Color = _widgetColor, Width = 2 }
            });

            _measureFeatures.Add(lineFeature);
            double totalRealDistanceInMeters = 0;
            for (int i = 1; i < _measurePoints.Count; i++)
            {
                var (lon1, lat1) = SphericalMercator.ToLonLat(_measurePoints[i - 1].X, _measurePoints[i - 1].Y);
                var (lon2, lat2) = SphericalMercator.ToLonLat(_measurePoints[i].X, _measurePoints[i].Y);

                totalRealDistanceInMeters += Location.CalculateDistance(
                    lat1, lon1, lat2, lon2, DistanceUnits.Kilometers) * 1000;
            }

            measurementResult = totalRealDistanceInMeters >= 1000
                ? $"{(totalRealDistanceInMeters / 1000.0):0.##} km"
                : $"{totalRealDistanceInMeters:N1} m";
        }

        string prefix = _isPolygonClosed ? $"{AppResources.flaeche}" : $"{AppResources.distanz}";
        _instructionWidget.Text = $"{prefix}: {measurementResult}";
        _measureLayer.FeaturesWereModified();
        MapControl.RefreshGraphics();
    }

    private async void SetPinClicked(object sender, EventArgs e)
    {
        if (!SettingsService.Instance.IsGpsActive)
        {
            await ShowGpsDisabledMessageAsync();
            return;
        }

        var location = await geoViewModel.TryGetLocationAsync();
        if (location == null)
            return;

        var newCenter = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(newCenter, map.Navigator.Resolutions[18]);

        var currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        Models.Pin newPinData = new()
        {
            Pos = new Microsoft.Maui.Graphics.Point(0, 0),
            Anchor = new Microsoft.Maui.Graphics.Point(0, 0),
            Size = new Microsoft.Maui.Graphics.Size(0, 0),
            IsLockPosition = false,
            IsLockRotate = true,
            IsLockAutoScale = true,
            IsCustomPin = false,
            IsCustomIcon = false,
            IsWebMapPin = true,
            PinName = "",
            PinDesc = "",
            PinPriority = 0,
            PinLocation = "",
            PinIcon = SettingsService.Instance.DefaultPinIcon,
            Fotos = [],
            OnPlanId = planId,
            SelfId = currentDateTime,
            DateTime = DateTime.Now,
            PinColor = SKColors.Red,
            PinScale = 1,
            PinRotation = 0,
            GeoLocation = new GeoLocData(location),
            IsAllowExport = true,
        };

        // Sicherstellen, dass der Plan existiert
        if (GlobalJson.Data.Plans.TryGetValue(planId, out Plan plan))
        {
            plan.Pins ??= [];
            plan.Pins[currentDateTime] = newPinData;

            GlobalJson.Data.Plans[planId].PinCount += 1;
            GlobalJson.SaveToFile();
        }

        AddPin(map, new Point(location.Longitude, location.Latitude), planId, plan.Pins[currentDateTime].SelfId);
    }

    private static TextBoxWidget CreateInstructionTextBox(string text) => new()
    {
        Text = text,
        TextSize = 13,
        VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top,
        HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Center,
        Margin = new MRect(50),
        Padding = new MRect(8),
        CornerRadius = 4,
        BackColor = new Color(108, 117, 125, 128),
        TextColor = Color.White,
    };

    private static TileLayer CreateSwissTopoLayer(string layerName)
    {
        string version = "1.0.0";
        string style = "default";
        string time = "current"; // Stand der Karte
        string projection = "3857"; // Web Mercator für MAUI/OSM Kompatibilität
        string format = "jpeg";

        var swisstopoUrl = $"https://wmts.geo.admin.ch/{version}/{layerName}/{style}/{time}/{projection}/{{z}}/{{x}}/{{y}}.{format}";

        var tileSource = new HttpTileSource(
            new GlobalSphericalMercator(0, 18),
            swisstopoUrl,
            name: layerName
        );

        return new TileLayer(tileSource)
        {
            Name = "SwissTopo",
            Enabled = true
        };
    }

    public async Task<byte[]> ExportMapAsImageAsync(Size targetSize, NetTopologySuite.Geometries.Geometry targetCenter)
    {
        double exportResolution = 0.2;
        var center = new MPoint(targetCenter.Centroid.X, targetCenter.Centroid.Y);
        var exportViewport = new Viewport(
            center.X,
            center.Y,
            exportResolution,
            0,
            targetSize.Width,
            targetSize.Height
        );

        var tempMap = new Map();
        foreach (var layer in map.Layers)
        {
            tempMap.Layers.Add(layer);
        }

        tempMap.RefreshData(exportViewport);

        int timeoutCounter = 0;
        bool stillLoading = true;

        await Task.Delay(100);

        while (stillLoading && timeoutCounter < 50)
        {
            stillLoading = tempMap.Layers.OfType<TileLayer>().Any(l => l.Busy);
            if (stillLoading)
            {
                await Task.Delay(200);
                timeoutCounter++;
            }
        }

        return await Task.Run(() =>
        {
            try
            {
                var renderer = new Mapsui.Rendering.Skia.MapRenderer();
                var renderService = new Mapsui.Rendering.RenderService();

                using var bitmapStream = renderer.RenderToBitmapStream(
                    exportViewport,
                    tempMap.Layers,
                    renderService,
                    Color.White,
                    1.0f,
                    null,
                    Mapsui.Rendering.RenderFormat.Jpeg,
                    90
                );

                if (bitmapStream == null) return null;

                using var ms = new MemoryStream();
                bitmapStream.Position = 0;
                bitmapStream.CopyTo(ms);
                tempMap.Layers.Clear();

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Export Fehler: " + ex.Message);
                tempMap.Layers.Clear();
                return null;
            }
        });
    }

    private void OnLayerChanged(object sender, EventArgs e)
    {
        var picker = (Picker)sender;

        if (picker.SelectedItem is not MapViewItem selectedItem || map == null)
            return;

        var functionalLayers = map.Layers
            .Where(l => l.Name == "Pins" || l.Name == "MeasureLayer")
            .ToList();

        map.Layers.Clear();

        ILayer baseLayer;
        if (!string.IsNullOrEmpty(selectedItem.Id))
        {
            if (selectedItem.Id.Contains("OpenStreetMap"))
                baseLayer = OpenStreetMap.CreateTileLayer();
            else
                baseLayer = CreateSwissTopoLayer(selectedItem.Id);
        }
        else
        {
            baseLayer = OpenStreetMap.CreateTileLayer();
        }

        map.Layers.Add(baseLayer);

        foreach (var layer in functionalLayers)
        {
            map.Layers.Add(layer);
        }

        map.RefreshGraphics();
    }

    private async void OnDeleteButtonClicked(object sender, EventArgs e)
    {
        var popup = new PopupDualResponse(AppResources.wollen_sie_diesen_plan_wirklich_loeschen, okText: AppResources.loeschen, alert: true);

        var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
        if (result.Result == null)
            return;

        if (Application.Current.Windows[0].Page is not AppShell shell)
            return;

        // Shell-Navigation entfernen
        var shellContent = shell
            .FindByName<ShellContent>(planId);

        if (shellContent?.Parent is ShellSection section)
            section.Items.Remove(shellContent);

        // Masterliste bereinigen
        var masterItem = shell.AllPlanItems
            .FirstOrDefault(p => p.PlanId == planId);

        if (masterItem != null)
            shell.AllPlanItems.Remove(masterItem);

        if (!GlobalJson.Data.Plans.TryGetValue(planId, out var plan))
            return;

        // JSON + Files löschen
        plan = GlobalJson.Data.Plans[planId];

        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "gs_" + plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", plan.File));

        GlobalJson.Data.Plans.Remove(planId);
        GlobalJson.SaveToFile();

        // Anzeige neu aufbauen
        shell.ApplyFilterAndSorting();

        await Shell.Current.GoToAsync("//homescreen");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static async Task UpdateAndSavePinLocationAsync(string planId, string pinId, double lon, double lat)
    {
        if (string.IsNullOrEmpty(planId) || string.IsNullOrEmpty(pinId))
            return;

        var pin = GlobalJson.Data.Plans[planId].Pins[pinId];

        pin.GeoLocation ??= new GeoLocData();

        if (pin.GeoLocation.WGS84 == null ||
            Math.Abs(pin.GeoLocation.WGS84.Latitude - lat) > 0.0000001 ||
            Math.Abs(pin.GeoLocation.WGS84.Longitude - lon) > 0.0000001)
        {
            pin.GeoLocation.WGS84 = new LocationWGS84(lat, lon);
            pin.GeoLocation.Accuracy = 0;

            await pin.GeoLocation.UpdateCH1903Async();

            GlobalJson.SaveToFile();
        }
    }

    private void OnTitleChanged(object sender, EventArgs e)
    {
        if (sender is not Entry entry)
            return;

        // Titel speichern
        (Application.Current.Windows[0].Page as AppShell)
            ?.AllPlanItems.FirstOrDefault(i => i.PlanId == planId)!.Title = Title;

        GlobalJson.Data.Plans[planId].Name = Title;
        GlobalJson.SaveToFile();

        // Fokus entfernen
        entry.Unfocus();

#if ANDROID
        try
        {
            if (entry.Handler?.PlatformView is Android.Views.View nativeView)
            {
                var inputMethodManager = nativeView.Context?.GetSystemService(
                    Android.Content.Context.InputMethodService) as Android.Views.InputMethods.InputMethodManager;

                // Tastatur schließen
                inputMethodManager?.HideSoftInputFromWindow(nativeView.WindowToken, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Android keyboard hide failed: {ex.Message}");
        }
#endif

#if IOS
        try
        {
            UIKit.UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                if (entry.Handler?.PlatformView is UIKit.UITextField textField)
                {
                    textField.ResignFirstResponder(); // Tastatur schließen
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"iOS keyboard hide failed: {ex.Message}");
        }
#endif
    }
}