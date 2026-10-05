using Foundation;
using Microsoft.Identity.Client;
using SnapDoc.Services;
using UIKit;
using System.Diagnostics.CodeAnalysis;

namespace SnapDoc;

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
    [Export("application:supportedInterfaceOrientationsForWindow:")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Objective-C Delegate-Methode, muss Instanzmethode sein")]
    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Signatur durch iOS-Selector vorgegeben")]
    public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow forWindow)
        => OrientationLock.SupportedOrientations;
}