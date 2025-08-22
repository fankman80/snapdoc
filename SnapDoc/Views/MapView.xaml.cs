#nullable disable

#if ANDROID
using Android.Webkit;
#endif

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
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

    public MapView()
    {
        InitializeComponent();

#if WINDOWS
        GeoAdminWebView.HandlerChanged += (s, e) =>
        {
            if (GeoAdminWebView.Handler.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 webview2)
            {
                webview2.WebMessageReceived += (sender2, args2) =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        string message = args2.TryGetWebMessageAsString();
                        //DisplayAlert("Nachricht aus JS", message, "OK");
                        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                        string pinkey = data["pinkey"];
                        string plankey = data["plankey"];
                        Shell.Current.GoToAsync($"setpin?planId={plankey}&pinId={pinkey}");
                    });
                };

                webview2.NavigationCompleted += async (sender2, args2) =>
                {
                    string json = GeneratePinJson();
                    await GeoAdminWebView.EvaluateJavaScriptAsync($"setMultipleMarkers({json});");
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
        public void InvokeAction(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                //_mapView.DisplayAlert("Nachricht aus JS", message, "OK");
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(message);
                string pinkey = data["pinkey"];
                string plankey = data["plankey"];
                Shell.Current.GoToAsync($"setpin?planId={plankey}&pinId={pinkey}");
            });
        }
    }

    public class CustomWebViewClient : WebViewClient
    {
        readonly MapView _mapView;

        public CustomWebViewClient(MapView mapView)
        {
            _mapView = mapView;
        }

        public override void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            base.OnPageFinished(view, url);

            // Marker setzen
            string pinJson = MapView.GeneratePinJson();
            view.EvaluateJavascript($"setMultipleMarkers({pinJson});", null);

            // JS-Funktion für C# definieren
            view.EvaluateJavascript("function sendToCSharp(msg) { jsBridge.invokeAction(msg); }", null);
        }
    }
#endif

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out var planIdObj)) PlanId = planIdObj as string ?? string.Empty;
        if (query.TryGetValue("pinId", out var pinIdObj)) PinId = pinIdObj as string ?? string.Empty;
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();

        var (lon, lat, zoom) = GetInitialMapCoordinates();

        GeoAdminWebView.Source = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile(lon, lat, zoom)
        };

        mapLayerPicker.ItemsSource = Settings.SwissTopoLayers.Select(item => item.Desc).ToList();
        mapLayerPicker.SelectedItem = Settings.SwissTopoLayers.FirstOrDefault()?.Desc;
    }

    private (double lon, double lat, double zoom) GetInitialMapCoordinates()
    {
        double lon, lat, zoom;

        if (!string.IsNullOrEmpty(PinId) && GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
        {
            var loc = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84;
            lon = loc.Longitude;
            lat = loc.Latitude;
            zoom = 18;
        }
        else if (GPSViewModel.Instance.IsRunning)
        {
            lon = GPSViewModel.Instance.Lon;
            lat = GPSViewModel.Instance.Lat;
            zoom = 18;
        }
        else
        {
            lon = 8.226692;
            lat = 46.80121;
            zoom = 8;
        }

        return (lon, lat, zoom);
    }

    private static string LoadHtmlFromFile(double lon, double lat, double zoom)
    {
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("SnapDoc.Resources.Raw.index.html")!;
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();

        htmlContent = htmlContent.Replace("{center_koord}", $"{lon.ToString(CultureInfo.InvariantCulture)}, {lat.ToString(CultureInfo.InvariantCulture)}");
        htmlContent = htmlContent.Replace("{mapzoom}", zoom.ToString());
        htmlContent = htmlContent.Replace("{icon}", SettingsService.Instance.MapIcons[SettingsService.Instance.MapIcon]);
        htmlContent = htmlContent.Replace("{iconzoom}", ((double)SettingsService.Instance.MapIconSize / 100).ToString(CultureInfo.InvariantCulture));
        htmlContent = htmlContent.Replace("#999999", ((Color)Application.Current.Resources["Primary"]).ToRgbaHex());
        htmlContent = htmlContent.Replace("#888888", ((Color)Application.Current.Resources["PrimaryDarkText"]).ToRgbaHex());

        return htmlContent;
    }

    private static string GeneratePinJson()
    {
        var positions = new List<object>();
        foreach (var plan in GlobalJson.Data.Plans)
        {
            foreach (var pin in plan.Value.Pins ?? new Dictionary<string, Pin>())
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
        if (GPSViewModel.Instance.IsRunning)
        {
            var popup = new PopupDualResponse("Sind Sie sicher dass Sie die Positionsdaten überschreiben wollen?");
            var result = await this.ShowPopupAsync<string>(popup, Settings.PopupOptions);
            if (result.Result != null)
            {
                Location location = new();
                if (GPSViewModel.Instance.IsRunning)
                {
                    location.Longitude = GPSViewModel.Instance.Lon;
                    location.Latitude = GPSViewModel.Instance.Lat;
                    location.Accuracy = GPSViewModel.Instance.Acc;
                }
                else
                    location = null;

                if (location != null)
                    GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation = new GeoLocData(location);

                GeoAdminWebView.Reload();
            }
        }
        else
        {
            var popup1 = new PopupAlert("Aktivieren Sie zuerst die Ortungsdienste, damit der Standort aktualisiert werden kann?");
            this.ShowPopup(popup1, Settings.PopupOptions);
        }
    }

    private async void GetCoordinatesClicked(object sender, EventArgs e)
    {
        string result = await GeoAdminWebView.EvaluateJavaScriptAsync("getMarkerCoordinates()");
        if (!string.IsNullOrEmpty(result))
        {
            result = result.Replace("\\\"", "\"");
            List<Coordinate> coordinates = JsonSerializer.Deserialize<List<Coordinate>>(result);

            foreach (var coord in coordinates)
            {
                GlobalJson.Data.Plans[coord.PlanKey].Pins[coord.PinKey].GeoLocation.WGS84.Longitude = coord.Lon;
                GlobalJson.Data.Plans[coord.PlanKey].Pins[coord.PinKey].GeoLocation.WGS84.Latitude = coord.Lat;
                GlobalJson.Data.Plans[coord.PlanKey].Pins[coord.PinKey].GeoLocation.Accuracy = 0;
            }

            // save data to file
            GlobalJson.SaveToFile();

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlert("", "Die Positionen aller Pins auf der Karte wurden aktualisiert.", "OK");
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
                await Application.Current.Windows[0].Page.DisplayAlert("", "KML-Datei wurde gespeichert", "OK");
            else
                await Toast.Make($"KML-Datei wurde gespeichert").Show();
        }
        else
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
                await Application.Current.Windows[0].Page.DisplayAlert("", "KML-Datei wurde nicht gespeichert", "OK");
            else
                await Toast.Make($"KML-Datei wurde nicht gespeichert").Show();
        }
        saveStream.Close();

        if (File.Exists(outputPath))
            File.Delete(outputPath);
    }

    private void MapLayerPicker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(mapLayerPicker.SelectedItem))
        {
            var selectedDesc = mapLayerPicker.SelectedItem?.ToString();
            var layer = Settings.SwissTopoLayers.FirstOrDefault(x => x.Desc == selectedDesc);
            if (layer != null)
            {
                var script = $"changeOverlayLayer('{layer.Id}');";
                GeoAdminWebView.EvaluateJavaScriptAsync(script);
            }
        }
    }

    private void OnMapLayerColorClicked(object sender, EventArgs e)
    {
        var layer = Settings.SwissTopoLayers[1].Id; //ch.swisstopo.pixelkarte-farbe
        var script = $"changeMapLayer('{layer}');";
        GeoAdminWebView.EvaluateJavaScriptAsync(script);
    }

    private void OnMapLayerRealClicked(object sender, EventArgs e)
    {
        var layer = Settings.SwissTopoLayers[5].Id; //ch.swisstopo.swissimage
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
