using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Identity.Client;

namespace SnapDoc.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
              WindowSoftInputMode = SoftInput.AdjustResize)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Window is null)
                return;

            // Ab Android 15 (API 35) ist Edge-to-Edge erzwungen – die alten APIs nur noch darunter verwenden
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                WindowCompat.SetDecorFitsSystemWindows(Window, false);
                Window.SetStatusBarColor(global::Android.Graphics.Color.Transparent);
            }

            // Helle Icons in Status- und Navigationsleiste (gilt für alle Versionen)
            _ = new WindowInsetsControllerCompat(Window, Window.DecorView)
            {
                AppearanceLightStatusBars = false,
                AppearanceLightNavigationBars = false
            };
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);
        }
    }
}