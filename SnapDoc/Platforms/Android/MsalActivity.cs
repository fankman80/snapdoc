using Android.App;
using Android.Content;
using Android.Content.PM;

namespace SnapDoc.Platforms.Android;

[Activity(Exported = true, LaunchMode = LaunchMode.SingleTask, NoHistory = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
[IntentFilter([Intent.ActionView],
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataScheme = "msal00fdac1d-aa0a-49c1-a238-a46a88f69ce6",
    DataHost = "auth")]
public class MsalActivity : Microsoft.Identity.Client.BrowserTabActivity
{
}