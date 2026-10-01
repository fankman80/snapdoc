#if ANDROID
using Android.Content.PM;
#elif IOS
using UIKit;
#endif
using System.Diagnostics;

namespace SnapDoc.Services;

// Sperrt die Bildschirmdrehung für eine einzelne Seite (wie die Systemkamera).
public static class OrientationLock
{
#if ANDROID
    private static ScreenOrientation? _previous;
#elif IOS
    private static UIInterfaceOrientationMask? _current;
    private static UIInterfaceOrientationMask DefaultMask =>
        UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Pad
            ? UIInterfaceOrientationMask.All
            : UIInterfaceOrientationMask.AllButUpsideDown;
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
