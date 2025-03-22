#nullable disable

#if ANDROID
using Android.Webkit;
#endif

using bsm24.Models;
using bsm24.Services;
using bsm24.ViewModels;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
using Mopups.Services;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace bsm24.Views;

public partial class MapView : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public MapView()
    {
#if ANDROID
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
        {
            handler.PlatformView.Settings.JavaScriptEnabled = true;
            handler.PlatformView.Settings.DomStorageEnabled = true;
            handler.PlatformView.Settings.SetGeolocationEnabled(true);
            handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            handler.PlatformView.Settings.AllowContentAccess = true;
            handler.PlatformView.Settings.AllowFileAccess = true;
            handler.PlatformView.Settings.MixedContentMode = MixedContentHandling.AlwaysAllow;
            handler.PlatformView.SetWebViewClient(new CustomWebViewClient());
        });
#endif
        InitializeComponent();
        mapLayerPicker.PropertyChanged += MapLayerPicker_PropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        double lon, lat, zoom;

        if (PinId != null)
        {
            SetPosBtn.IsEnabled = true;
            if (GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation != null)
            {
                lon = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Longitude;
                lat = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Latitude;
                zoom = 18;
            }
            else
            {
                if (GPSViewModel.Instance.IsRunning)
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
            }
        }
        else
        {
            if (GPSViewModel.Instance.IsRunning)
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
        }

        var htmlSource = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile(lon, lat, zoom),
        };

        GeoAdminWebView.Source = htmlSource;

#if WINDOWS
        GeoAdminWebView.Navigated += (s, e) =>
        {
            GeoAdminWebView.EvaluateJavaScriptAsync(Generatescript());
        };
#endif

        mapLayerPicker.ItemsSource = Settings.SwissTopoLayers.Select(item => item.Desc).ToList(); // load map-layers to picker
        mapLayerPicker.SelectedItem = Settings.SwissTopoLayers[0].Desc;
    }


#if ANDROID
    public class CustomWebViewClient : WebViewClient
    {
        public override void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            base.OnPageFinished(view, url);
            view.EvaluateJavascript(Generatescript(), null);
        }
    }
#endif

    private static string LoadHtmlFromFile(double lon, double lat, double zoom)
    {
        // Lade das HTML-Template
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("bsm24.Resources.Raw.index.html");
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();

        // Ersetze die Platzhalter für die Koordinaten im HTML
        string _center_koord = lon.ToString(CultureInfo.InvariantCulture) + ", " + lat.ToString(CultureInfo.InvariantCulture);
        string _zoom = zoom.ToString();

        htmlContent = htmlContent.Replace("{maplayer}", "ch.swisstopo.pixelkarte-farbe");
        htmlContent = htmlContent.Replace("{center_koord}", _center_koord);
        htmlContent = htmlContent.Replace("{mapzoom}", _zoom);
        htmlContent = htmlContent.Replace("{icon}", SettingsService.Instance.MapIcons[SettingsService.Instance.MapIcon]);
        htmlContent = htmlContent.Replace("{iconzoom}", ((double)SettingsService.Instance.MapIconSize / 100).ToString(CultureInfo.InvariantCulture));
        htmlContent = htmlContent.Replace("#999999", ((Color)Application.Current.Resources["Primary"]).ToRgbaHex());

        return htmlContent;
    }

    private static string Generatescript()
    {
        string positionsJson = "[";
        foreach (var plan in GlobalJson.Data.Plans)
        {
            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
            {
                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                {
                    if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                    {
                        var lon = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.Longitude;
                        var lat = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.Latitude;
                        var pindesc = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc;
                        var pinlocation = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation;
                        var pinname = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName;
                        positionsJson += $"{{ lon: {lon.ToString(CultureInfo.InvariantCulture)}, lat: {lat.ToString(CultureInfo.InvariantCulture)}, pinname: '{pinname}', pinlocation: '{pinlocation}', pindesc: '{pindesc}', plankey: '{plan.Key}', pinkey: '{pin.Key}'}},";
                    }
                }
            }
        }
        positionsJson = positionsJson.TrimEnd(',') + "]";
        return $"setMultipleMarkers({positionsJson});";
    }
    private async void SetPosClicked(object sender, EventArgs e)
    {
        if (GPSViewModel.Instance.IsRunning)
        {
            var popup = new PopupDualResponse("Sind Sie sicher dass Sie die Positionsdaten überschreiben wollen?");
            await MopupService.Instance.PushAsync(popup);
            var result = await popup.PopupDismissedTask;
            if (result != null)
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
            await MopupService.Instance.PushAsync(popup1);
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
        }
    }

    private async void KmlExportClicked(object sender, EventArgs e)
    {
        string outputPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ProjectPath + ".kml");

        List<(double Latitude, double Longitude, string Name, DateTime Time)> coordinates = [];
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
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc,
                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].DateTime));
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

    private async void OnButtonPressed(object sender, EventArgs e)
    {
        var button = (Button)sender;
        await button.ScaleTo(0.8, 150); // Animation für Button-Verkleinerung
        button.Text = "gespeichert";
    }

    private async void OnButtonReleased(object sender, EventArgs e)
    {
        var button = (Button)sender;
        await button.ScaleTo(1.0, 150); // Animation für Button-Rückkehr zur Normalgröße
        await Task.Delay(1500);
        button.Text = "Speichern";
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
    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("plankey")]
    public string PlanKey { get; set; }

    [JsonPropertyName("pinkey")]
    public string PinKey { get; set; }
}