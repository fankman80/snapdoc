#nullable disable

#if ANDROID
using Android.Webkit;
#endif

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using SnapDoc.Models;
using SnapDoc.Services;
using SnapDoc.ViewModels;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnapDoc.Views;

public partial class MapView : IQueryAttributable
{
    public string PlanId = string.Empty;
    public string PinId = string.Empty;
    private double lon = 8.226692;  // Default: Schweiz
    private double lat = 46.80121;
    private int zoom = 8;
    private readonly GeolocationViewModel geoViewModel = GeolocationViewModel.Instance;

    public MapView()
    {
        InitializeComponent();

#if WINDOWS
        GeoAdminWebView.HandlerChanged += async (s, e) =>
        {
            if (GeoAdminWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webview2)
            {
                // Nachrichten von JS abfangen
                webview2.WebMessageReceived += (sender2, args2) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        string message = args2.TryGetWebMessageAsString();
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                        string pinkey = data["pinkey"];
                        string plankey = data["plankey"];
                        Shell.Current.GoToAsync($"setpin?planId={plankey}&pinId={pinkey}");
                    });
                };

                // Erst CoreWebView2 initialisieren
                if (webview2.CoreWebView2 == null)
                    await webview2.EnsureCoreWebView2Async();

                // JS ausführen, sobald das DOM geladen ist
                webview2.CoreWebView2.DOMContentLoaded += async (sender2, args2) =>
                {
                    await webview2.ExecuteScriptAsync(await GenerateIconString());
                };
            }
        };
#endif

#if ANDROID
        GeoAdminWebView.HandlerChanged += (s, e) =>
        {
            if (GeoAdminWebView.Handler?.PlatformView is Android.Webkit.WebView nativeWebView)
            {
                nativeWebView.Settings.JavaScriptEnabled = true;
                nativeWebView.Settings.DomStorageEnabled = true;
                nativeWebView.Settings.SetGeolocationEnabled(true);
                nativeWebView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
                nativeWebView.Settings.AllowContentAccess = true;
                nativeWebView.Settings.AllowFileAccess = true;
                nativeWebView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;

                // JS Bridge registrieren
                nativeWebView.AddJavascriptInterface(new JsBridge(this), "jsBridge");

                // PageFinished überschreiben
                nativeWebView.SetWebViewClient(new CustomWebViewClient(this));
            }
        };
#endif
    }

#if ANDROID
    // Bridge-Klasse
    public class JsBridge(MapView mapView) : Java.Lang.Object
    {
        readonly MapView _mapView = mapView;

        [JavascriptInterface]
        [Java.Interop.Export("invokeAction")]
        public static void InvokeAction(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                string pinkey = data["pinkey"];
                string plankey = data["plankey"];
                Shell.Current.GoToAsync($"setpin?planId={plankey}&pinId={pinkey}");
            });
        }
    }

    public class CustomWebViewClient(MapView mapView) : WebViewClient
    {
        public override async void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            base.OnPageFinished(view, url);

            view.EvaluateJavascript(await mapView.GenerateIconString(), null);

            // JS-Bridge definieren
            view.EvaluateJavascript("function sendToCSharp(msg) { jsBridge.invokeAction(msg); }", null);
        }
    }
#endif

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out var planIdObj))
            PlanId = planIdObj as string ?? string.Empty;
        if (query.TryGetValue("pinId", out var pinIdObj)) 
            PinId = pinIdObj as string ?? string.Empty;

        UpdateUiFromQuery();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateUiFromQuery();

        GeoAdminWebView.Source = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile()
        };
    }

    private async void UpdateUiFromQuery()
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
                Location location = await geoViewModel.GetCurrentLocationAsync() ?? geoViewModel.LastKnownLocation;

                // Zoom auf GPS
                lon = location.Longitude;
                lat = location.Latitude;
                zoom = 18;
            }
        }
        else if (SettingsService.Instance.IsGpsActive)
        {
            Location location = await geoViewModel.GetCurrentLocationAsync() ?? geoViewModel.LastKnownLocation;

            // Zoom auf GPS (wenn kein Pin)
            lon = location.Longitude;
            lat = location.Latitude;
            zoom = 18;
        }
    }

    private static async Task<string> LoadSvgMarkupAsync(string rawFileName, string newColor)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(rawFileName);
        using var reader = new StreamReader(stream);
        string svgText = await reader.ReadToEndAsync();

        // Farbe ersetzen
        svgText = svgText.Replace("#999999", newColor, StringComparison.OrdinalIgnoreCase);

        // Whitespace entfernen
        svgText = svgText.Trim();

        return svgText;
    }

    private async Task<string> GenerateIconString()
    {
        string icon = SettingsService.Instance.MapIcons[SettingsService.Instance.MapIcon];
        if (icon == "themeColorPin")
        {
            string hexColor = ((Color)Application.Current.Resources["Primary"]).ToRgbaHex();
            icon = await LoadSvgMarkupAsync("customcolor.svg", hexColor);
            icon = icon.Replace("'", "\\'").Replace("\r", "").Replace("\n", "");
        }

        double scale = (double)SettingsService.Instance.MapIconSize / 100;
        string pinJson = GeneratePinJson();

        return $@"initMarkersFromBridge(
                [{lon.ToString(CultureInfo.InvariantCulture)}, {lat.ToString(CultureInfo.InvariantCulture)}],
                {zoom},
                '{icon}',
                {scale.ToString(CultureInfo.InvariantCulture)},
                {pinJson});";
    }

    private static string LoadHtmlFromFile()
    {
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("SnapDoc.Resources.Raw.index.html")!;
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();
        htmlContent = htmlContent.Replace("#999999", ((Color)Application.Current.Resources["Primary"]).ToRgbaHex());
        htmlContent = htmlContent.Replace("#888888", ((Color)Application.Current.Resources["PrimaryDarkText"]).ToRgbaHex());

        return htmlContent;
    }

    private static string GeneratePinJson()
    {
        var positions = new List<object>();
        foreach (var plan in GlobalJson.Data.Plans)
        {
            foreach (var pin in plan.Value.Pins ?? [])
            {
                if (pin.Value.GeoLocation != null)
                {
                    var loc = pin.Value.GeoLocation.WGS84;
                    positions.Add(new
                    {
                        lon = loc.Longitude,
                        lat = loc.Latitude,
                        pinname = pin.Value.PinName,
                        pinlocation = pin.Value.PinLocation,
                        pindesc = pin.Value.PinDesc,
                        plankey = plan.Key,
                        pinkey = pin.Key
                    });
                }
            }
        }
        return JsonSerializer.Serialize(positions);
    }

    private async void SetPosClicked(object sender, EventArgs e)
    {
        if (!SettingsService.Instance.IsGpsActive)
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("Standortdienste deaktiviert", "Bitte aktivieren Sie die Standortdienste im System und in der App, um Positionsdaten nutzen zu können.", "OK");
            else
                await Toast.Make($"Bitte aktivieren Sie die Standortdienste im System und in der App, um Positionsdaten nutzen zu können.").Show();
            return;
        }
        
        Location location = await geoViewModel.GetCurrentLocationAsync() ?? geoViewModel.LastKnownLocation;

        lon = location.Longitude;
        lat = location.Latitude;
        zoom = 18;

        GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation = new GeoLocData(location);

        GeoAdminWebView.Reload();
    }

    private async void GetCoordinatesClicked(object sender, EventArgs e)
    {
        var script = "getMarkerCoordinates()";
        string result = await GeoAdminWebView.EvaluateJavaScriptAsync(script);

        if (!string.IsNullOrEmpty(result))
        {
            result = result.Replace("\\\"", "\"");
            List<Coordinate> coordinates = JsonSerializer.Deserialize<List<Coordinate>>(result);

            foreach (var coord in coordinates)
            {
                var pin = GlobalJson.Data.Plans[coord.PlanKey].Pins[coord.PinKey];

                // Prüfen, ob sich die Koordinaten geändert haben
                if (pin.GeoLocation.WGS84.Latitude != coord.Lat || pin.GeoLocation.WGS84.Longitude != coord.Lon)
                {
                    pin.GeoLocation.WGS84.Latitude = coord.Lat;
                    pin.GeoLocation.WGS84.Longitude = coord.Lon;
                    pin.GeoLocation.Accuracy = 0;

                    await pin.GeoLocation.UpdateCH1903Async();
                }
            }

            // save data to file
            GlobalJson.SaveToFile();

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", "Die Positionen aller Pins auf der Karte wurden aktualisiert.", "OK");
            else
                await Toast.Make($"Die Positionen aller Pins auf der Karte wurden aktualisiert.").Show();
        }
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
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", "KML-Datei wurde gespeichert.", "OK");
            else
                await Toast.Make($"KML-Datei wurde gespeichert.").Show();
        }
        else
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlertAsync("", "KML-Datei wurde nicht gespeichert.", "OK");
            else
                await Toast.Make($"KML-Datei wurde nicht gespeichert.").Show();
        }
        saveStream.Close();

        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private void OnMapLayerColorClicked(object sender, EventArgs e)
    {
        var layer = Settings.SwissTopoLayers[SettingsService.Instance.MapOverlay1].Id; //ch.swisstopo.pixelkarte-farbe
        var script = $"changeMapLayer('{layer}');";
        GeoAdminWebView.EvaluateJavaScriptAsync(script);
    }

    private void OnMapLayerRealClicked(object sender, EventArgs e)
    {
        var layer = Settings.SwissTopoLayers[SettingsService.Instance.MapOverlay2].Id; //ch.swisstopo.swissimage
        var script = $"changeMapLayer('{layer}');";
        GeoAdminWebView.EvaluateJavaScriptAsync(script);
    }
}

public class Coordinate
{
    [JsonPropertyName("lon")] public double Lon { get; set; }
    [JsonPropertyName("lat")] public double Lat { get; set; }
    [JsonPropertyName("plankey")] public string PlanKey { get; set; } = string.Empty;
    [JsonPropertyName("pinkey")] public string PinKey { get; set; } = string.Empty;
}
