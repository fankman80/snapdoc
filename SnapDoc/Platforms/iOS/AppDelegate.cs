using Foundation;
using Microsoft.Identity.Client;
using SnapDoc.Services;
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

        /// <summary>
        /// iOS fragt hier ab, welche Ausrichtungen gerade erlaubt sind.
        /// Die CameraPage sperrt damit auf Hochformat, alle anderen Seiten bleiben frei drehbar.
        /// </summary>
        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow forWindow)
            => OrientationLock.SupportedOrientations;
    }
}
