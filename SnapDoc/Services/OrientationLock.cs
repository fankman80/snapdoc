#if ANDROID
using Android.Content.PM;
#elif IOS
using UIKit;
#endif
using System.Diagnostics;

namespace SnapDoc.Services;

/// <summary>
/// Sperrt die Bildschirmdrehung fuer eine einzelne Seite (wie die Systemkamera).
/// </summary>
public static class OrientationLock
{
#if ANDROID
    private static ScreenOrientation? _previous;
#elif IOS
    /// <summary>Wird vom AppDelegate abgefragt.</summary>
    public static UIInterfaceOrientationMask SupportedOrientations { get; private set; }
        = UIInterfaceOrientationMask.AllButUpsideDown;
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
        Apply(UIInterfaceOrientationMask.AllButUpsideDown);
#endif
    }

#if IOS
    private static void Apply(UIInterfaceOrientationMask mask)
    {
        SupportedOrientations = mask;

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
