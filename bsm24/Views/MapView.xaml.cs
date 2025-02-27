#nullable disable

#if ANDROID
using Android.Webkit;
using Microsoft.Maui.Platform;
#endif

namespace bsm24.Views;

public partial class MapView
{
    public MapView()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        //ShowCurrentLocationOnMap();

        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.Settings.SetGeolocationEnabled(true);
            handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            handler.PlatformView.Settings.JavaScriptEnabled = true;
            handler.PlatformView.SetWebChromeClient(new MyWebChromeClient());
#endif
        });

        string baseUrl;

#if ANDROID
        baseUrl = "file:///android_asset/";
#elif IOS
    baseUrl = NSBundle.MainBundle.BundlePath + "/";
#elif WINDOWS
    baseUrl = "ms-appx-web:///Assets/";
#else
    baseUrl = string.Empty; // Fallback
#endif

        // HTML-Datei von Ressourcen laden
        var htmlSource = new HtmlWebViewSource
        {
            BaseUrl = baseUrl,
            Html = LoadHtmlFromFile()
        };

        GeoAdminWebView.Source = htmlSource;

        GeoAdminWebView.Navigated += (s, e) =>
        {
            var longitude = 8.391505323137489;
            var latitude = 46.99582639810278;
            string script = $"setMarkerPosition({longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
            GeoAdminWebView.Eval(script);
        };
        //SetMarkerPosition(8.391505323137489, 46.99582639810278);

    }

    private static string LoadHtmlFromFile()
    {
        var assembly = typeof(MapView).Assembly;
        using var stream = assembly.GetManifestResourceStream("bsm24.Resources.Raw.map.html");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void SetMarkerPosition(double longitude, double latitude)
    {
        // Überprüfe, ob die WebView geladen ist
        if (GeoAdminWebView != null)
        {
            string script = $"setMarkerPosition({longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});";
            GeoAdminWebView.Eval(script);
        }
    }

    private async void ShowCurrentLocationOnMap()
    {
        var location = await Helper.GetCurrentLocationAsync();
        if (location != null)
        {
            Functions.LLtoSwissGrid(location.Latitude, location.Longitude, out double swissEasting, out double swissNorthing);
            string formattedEasting = swissEasting.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string formattedNorthing = swissNorthing.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string geoAdminUrl = $"https://map.geo.admin.ch/#/map?lang=de&center={formattedEasting},{formattedNorthing}&z=12";
            GeoAdminWebView.Source = geoAdminUrl;
        }
        else
            GeoAdminWebView.Source = "https://map.geo.admin.ch";
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