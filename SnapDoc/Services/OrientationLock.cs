#if ANDROID
using Android.Content.PM;
#elif IOS
using UIKit;
#endif
using System.Diagnostics;

namespace SnapDoc.Services;

/// <summary>
/// Sperrt die Bildschirmdrehung für eine einzelne Seite (wie die Systemkamera).
/// </summary>
public static class OrientationLock
{
#if ANDROID
    private static ScreenOrientation? _previous;
#elif IOS
    private static UIInterfaceOrientationMask? _current;

    /// <summary>
    /// Standard ohne Sperre, passend zur Info.plist:
    /// iPad alle vier Richtungen, iPhone alle ausser "auf dem Kopf".
    /// </summary>
    private static UIInterfaceOrientationMask DefaultMask =>
        UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad
            ? UIInterfaceOrientationMask.All
            : UIInterfaceOrientationMask.AllButUpsideDown;

    /// <summary>
    /// Wird vom AppDelegate abgefragt. Wird erst beim ersten Zugriff ausgewertet
    /// (immer auf dem Main Thread), damit UIDevice nicht aus einem
    /// statischen Initialisierer auf einem anderen Thread aufgerufen wird.
    /// </summary>
    public static UIInterfaceOrientationMask SupportedOrientations => _current ??= DefaultMask;
#endif

    public static void LockPortrait()
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        _previous ??= activity.RequestedOrientation;
        activity.RequestedOrientation = ScreenOrientation.Portrait;
#elif IOS
        Apply(UIInterfaceOrientationMask.Portrait);
#endif
    }

    public static void Unlock()
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        activity.RequestedOrientation = _previous ?? ScreenOrientation.Unspecified;
        _previous = null;
#elif IOS
        Apply(DefaultMask);
#endif
    }

#if IOS
    private static void Apply(UIInterfaceOrientationMask mask)
    {
        _current = mask;

        if (OperatingSystem.IsIOSVersionAtLeast(16))
        {
            foreach (var scene in UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>())
            {
                scene.Windows.FirstOrDefault(w => w.IsKeyWindow)?
                     .RootViewController?.SetNeedsUpdateOfSupportedInterfaceOrientations();

                scene.RequestGeometryUpdate(
                    new UIWindowSceneGeometryPreferencesIOS(mask),
                    error => Debug.WriteLine($"[CAM] GeometryUpdate: {error.LocalizedDescription}"));
            }
        }
        else
        {
            UIViewController.AttemptRotationToDeviceOrientation();
        }
    }
#endif
}
