using Android.App;
using Android.Content.PM;
using Android.OS;
using AP = Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Android;
using Android.Content;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;
using Environment = Android.OS.Environment;

namespace SnapDoc.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                if (!Environment.IsExternalStorageManager)
                {
                    RequestManageExternalStoragePermission();
                }
            }
        }

        private void RequestManageExternalStoragePermission()
        {
            // Intent to request MANAGE_EXTERNAL_STORAGE
            Intent intent = new(AP.Settings.ActionManageAllFilesAccessPermission);
            StartActivity(intent);
        }
    }
}
