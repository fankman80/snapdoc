using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace SnapDoc.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static MainActivity? Instance { get; private set; }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            Instance = this;
            base.OnCreate(savedInstanceState);

            if (Window != null)
            {
                WindowCompat.SetDecorFitsSystemWindows(Window, false);
                Window.SetStatusBarColor(global::Android.Graphics.Color.Transparent);
            }
        }
        public void UpdatePlatformColors(bool isLightStatus)
        {
            if (Window == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var decorView = Window.DecorView;
                if (decorView == null) return;

                var controller = WindowCompat.GetInsetsController(Window, decorView);
                controller?.AppearanceLightStatusBars = isLightStatus;
            });
        }
    }
}
