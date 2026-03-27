#nullable disable
using BruTile.Predefined;
using BruTile.Web;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using DocumentFormat.OpenXml.Presentation;
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
using Mapsui.Widgets.InfoWidgets;
using Mapsui.Widgets.ScaleBar;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Resources.Languages;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using System.Diagnostics;
using System.Security.AccessControl;
using Color = Mapsui.Styles.Color;
using Font = Mapsui.Styles.Font;
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
    private readonly RulerWidget _rulerWidget;
    private readonly Microsoft.Maui.Graphics.Color hexColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Primary"];

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

    public MapViewOSM()
    {
        InitializeComponent();

        BindingContext = this;

        // casting Colors from MAUI to Mapsui
        var c1 = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Primary"];
        var widgetColor = new Color((int)(hexColor.Red * 255), (int)(hexColor.Green * 255), (int)(hexColor.Blue * 255));

        _instructionWidget = CreateInstructionTextBox(AppResources.tippen_ziehen_messen);
        _instructionWidget.Enabled = false; // initial unsichtbar
        _rulerWidget = new RulerWidget // Ruler am Anfang inaktiv
        {
            IsActive = false,
            Color = widgetColor,
            ColorOfBeginAndEndDots = widgetColor
        };

        LayerPicker.ItemsSource = Settings.SwissTopoLayers;
        LayerPicker.SelectedIndex = 1;
        map.Widgets.Clear();
        map.Layers.Add(CreateSwissTopoLayer("ch.swisstopo.pixelkarte-farbe"));
        map.Layers.Add(CreatePinLayer());

        map.Widgets.Add(new ScaleBarWidget(map) { MaxWidth = 180, Margin = new MRect(8), TextAlignment = Mapsui.Widgets.Alignment.Center, Font = new Font { FontFamily = "sans serif", Size = 14 }, HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left, VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom });
        map.Widgets.Add(_instructionWidget);
        map.Widgets.Add(_rulerWidget);

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
        string uri = new Uri(Helper.LoadSvgWithColor("customcolor.svg", "#999999", hexColor.ToRgbaHex())).AbsoluteUri;
        pinImage = new Mapsui.Styles.Image { Source = uri };

        foreach (var pin in GlobalJson.Data.Plans[PlanId].Pins ?? [])
        {
            if (pin.Value.GeoLocation != null)
            {
                var loc = pin.Value.GeoLocation.WGS84;
                AddPin(map, new Point(loc.Longitude, loc.Latitude), PlanId, pin.Key);
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
        double scale = (double)SettingsService.Instance.MapIconSize / 100.0;

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

        _ = UpdateUiFromQueryAsync();
    }

    private async Task UpdateUiFromQueryAsync()
    {
        if (!string.IsNullOrEmpty(PlanId) && !string.IsNullOrEmpty(PinId))
        {
            Pin = new PinItem(GlobalJson.Data.Plans[PlanId].Pins[PinId]);
            SetPosBtn.IsVisible = true;

            if (GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
            {
                // Zoom auf Pin
                lon = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Longitude;
                lat = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Latitude;
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

    private async void OnRulerClicked(object sender, EventArgs e)
    {
        _rulerWidget.IsActive = !_rulerWidget.IsActive;
        _instructionWidget.Enabled = _rulerWidget.IsActive;

        if (_rulerWidget.IsActive)
            RulerButton.Text = AppResources.abbrechen;
        else
            RulerButton.Text = AppResources.distanz_messen;

        MapControl.Map.RefreshGraphics();
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
            var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);

            if (result.Result == "edit")
                _mapInitialized = false;

            if (result.Result == "export")
            {
                var imageSize = new System.Drawing.Size(2000, 2000);
                var imageBytes = await ExportMapAsImageAsync(imageSize, feature.Geometry.Centroid);

                if (imageBytes == null || imageBytes.Length == 0)
                    return;

                string filename = $"MAP_IMG_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                string filepath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, filename);
                string thumbPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ThumbnailPath, filename);

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
        // Nur Pins-Layer abfragen
        var pinLayer = map.Layers.FirstOrDefault(l => l.Name == "Pins");
        if (pinLayer == null)
            return;

        var mapInfo = e.GetMapInfo([pinLayer]);
        if (mapInfo?.Feature is GeometryFeature feature && feature["PinId"] != null)
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

    private async void OnReleased(object sender, MapEventArgs e)
    {
        if (!_isDraggingPin || _draggedPin == null)
        {
            _isDraggingPin = false;
            _draggedPin = null;
            MapControl.Map.Navigator.PanLock = false;
            return;
        }

        try
        {
            var planKey = _draggedPin["PlanId"]?.ToString();
            var pinKey = _draggedPin["PinId"]?.ToString();

            if (planKey != null && pinKey != null && _draggedPin.Geometry is Point point)
            {
                var pin = GlobalJson.Data.Plans[planKey].Pins[pinKey];
                var wgs84 = SphericalMercator.ToLonLat(point.X, point.Y);

                pin.GeoLocation ??= new GeoLocData();

                if (pin.GeoLocation.WGS84.Latitude != wgs84.lat || pin.GeoLocation.WGS84.Longitude != wgs84.lon)
                {
                    pin.GeoLocation.WGS84.Latitude = wgs84.lat;
                    pin.GeoLocation.WGS84.Longitude = wgs84.lon;
                    pin.GeoLocation.Accuracy = 0;

                    await pin.GeoLocation.UpdateCH1903Async();

                    // GlobalJson speichern
                    GlobalJson.SaveToFile();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fehler beim Speichern des Pins: {ex.Message}");
        }
        finally
        {
            _isDraggingPin = false;
            _draggedPin = null;
            MapControl.Map.Navigator.PanLock = false;
        }
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

        lon = location.Longitude;
        lat = location.Latitude;
        zoom = 18;

        var newCenter = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
        map.Navigator.CenterOnAndZoomTo(newCenter, map.Navigator.Resolutions[zoom]);

        var currentDateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        Models.Pin newPinData = new()
        {
            Pos = new Microsoft.Maui.Graphics.Point(0,0),
            Anchor = new Microsoft.Maui.Graphics.Point(0, 0),
            Size = new Microsoft.Maui.Graphics.Size(0,0),
            IsLockPosition = false,
            IsLockRotate = true,
            IsLockAutoScale = true,
            IsCustomPin = false,
            IsCustomIcon = false,
            PinName = "WebMap-Pin",
            PinDesc = "",
            PinPriority = 0,
            PinLocation = "",
            PinIcon = SettingsService.Instance.DefaultPinIcon,
            Fotos = [],
            OnPlanId = PlanId,
            SelfId = currentDateTime,
            DateTime = DateTime.Now,
            PinColor = SKColors.Red,
            PinScale = 1,
            PinRotation = 0,
            GeoLocation = new GeoLocData(location),
            IsAllowExport = true,
        };

        // Sicherstellen, dass der Plan existiert
        if (GlobalJson.Data.Plans.TryGetValue(PlanId, out Plan plan))
        {
            plan.Pins ??= [];
            plan.Pins[currentDateTime] = newPinData;

            GlobalJson.Data.Plans[PlanId].PinCount += 1;
            GlobalJson.SaveToFile();
        }

        AddPin(map, new Point(lon, lat), PlanId, plan.Pins[currentDateTime].SelfId);
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

    public async Task<byte[]> ExportMapAsImageAsync(System.Drawing.Size targetSize, NetTopologySuite.Geometries.Geometry targetCenter)
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
        foreach (var layer in MapControl.Map.Layers)
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

        var baseLayers = map.Layers.Where(l => l.Name != "Pins").ToList();
        foreach (var layer in baseLayers)
        {
            map.Layers.Remove(layer);
        }

        if (!string.IsNullOrEmpty(selectedItem.Id))
        {
            if (selectedItem.Id.Contains("OpenStreetMap"))
                map.Layers.Insert(0, OpenStreetMap.CreateTileLayer());
            else
                map.Layers.Insert(0, CreateSwissTopoLayer(selectedItem.Id));
        }
        else
            map.Layers.Insert(0, OpenStreetMap.CreateTileLayer());

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
            .FindByName<ShellContent>(PlanId);

        if (shellContent?.Parent is ShellSection section)
            section.Items.Remove(shellContent);

        // Masterliste bereinigen
        var masterItem = shell.AllPlanItems
            .FirstOrDefault(p => p.PlanId == PlanId);

        if (masterItem != null)
            shell.AllPlanItems.Remove(masterItem);

        if (!GlobalJson.Data.Plans.TryGetValue(PlanId, out var plan))
            return;

        // JSON + Files löschen
        plan = GlobalJson.Data.Plans[PlanId];

        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "gs_" + plan.File));
        DeleteIfExists(Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, "thumbnails", plan.File));

        GlobalJson.Data.Plans.Remove(PlanId);

        // save data to file
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
}