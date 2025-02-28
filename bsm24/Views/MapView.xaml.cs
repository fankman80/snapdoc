#nullable disable

#if ANDROID
using Android.Webkit;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.Maui.Platform;
#endif

namespace bsm24.Views;

public partial class MapView : IQueryAttributable
{
    public string PlanId;
    public string PinId;
    public MapView()
    {
        InitializeComponent();
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("planId", out object value1))
            PlanId = value1 as string;
        if (query.TryGetValue("pinId", out object value2))
            PinId = value2 as string;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.Settings.JavaScriptEnabled = true;
            handler.PlatformView.Settings.DomStorageEnabled = true;
            handler.PlatformView.Settings.SetGeolocationEnabled(true);
            handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            handler.PlatformView.Settings.AllowContentAccess = true;
            handler.PlatformView.Settings.AllowFileAccess = true;
            handler.PlatformView.SetWebChromeClient(new Android.Webkit.WebChromeClient());
#endif
        });

        double lon, lat, zoom;
        if (PinId != null)
        {
            lon = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Longitude;
            lat = GlobalJson.Data.Plans[PlanId].Pins[PinId].GeoLocation.WGS84.Latitude;
            zoom = 18;
        }
        else
        {             
            var location = await Helper.GetCurrentLocationAsync();
            if (location != null)
            {
                lon = location.Longitude;
                lat = location.Latitude;
                zoom = 12;
            }
            else
            {
                lon = 8.226692;
                lat = 46.80121;
                zoom = 8;
            }
        }

        // HTML-Datei von Ressourcen laden
        var htmlSource = new HtmlWebViewSource
        {
            Html = LoadHtmlFromFile(lon, lat, zoom)
        };

        GeoAdminWebView.Source = htmlSource;

        GeoAdminWebView.Navigated += (s, e) =>
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
                            positionsJson += $"{{ lon: {lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}, lat: {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)} }},";
                        }
                    }
                }
            }
            positionsJson = positionsJson.TrimEnd(',') + "]";

            // Übergib das JavaScript Array an die Funktion 'setMultipleMarkers'
            string script = $"setMultipleMarkers({positionsJson});";
            GeoAdminWebView.Eval(script);
        };
    }

    private static string LoadHtmlFromFile(double lon, double lat, double zoom)
    {
        // Lade das HTML-Template
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("bsm24.Resources.Raw.map.html");
        using var reader = new StreamReader(stream);
        string htmlContent = reader.ReadToEnd();

        // Ersetze die Platzhalter für die Koordinaten im HTML
        string _center_koord = lon.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string _zoom = zoom.ToString();
        htmlContent = htmlContent.Replace("{center_koord}", _center_koord);
        htmlContent = htmlContent.Replace("{zoom}", _zoom);

        return htmlContent;
    }
}

#if ANDROID
internal class MyWebChromeClient : WebChromeClient
{
    public static async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
            return status;
        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
            return status;
        if (Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>())
            // Prompt the user with additional information as to why the permission is needed
        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status;
    }

    public override void OnGeolocationPermissionsShowPrompt(string origin, GeolocationPermissions.ICallback callback)
    {
        base.OnGeolocationPermissionsShowPrompt(origin, callback);
        callback.Invoke(origin, true, false);
    }
}
#endif