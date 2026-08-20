using Foundation;
using Microsoft.Identity.Client;
using UIKit;

namespace SnapDoc
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool OpenUrl(UIApplication app, NSUrl url, NSDictionary options)
        {
            if (AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(url))
                return true;

            return base.OpenUrl(app, url, options);
        }
    }
}
