#nullable disable

#if ANDROID
using Android.Webkit;
#endif

using Microsoft.Maui.Platform;
using System.Text;

namespace bsm24.Views;

public partial class MapView
{
    public MapView()
    {
        InitializeComponent();
        ShowCurrentLocationOnMap();

        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("MyCustomization", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.Settings.SetGeolocationEnabled(true);
            handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
            handler.PlatformView.Settings.JavaScriptEnabled = true;
            handler.PlatformView.SetWebChromeClient(new MyWebChromeClient());
#endif
        });
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