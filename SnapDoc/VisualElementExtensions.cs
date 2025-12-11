namespace SnapDoc;

public static class VisualElementExtensions
{
    public static Point GetAbsolutePosition(this VisualElement view)
    {
        if (view?.Handler?.PlatformView == null)
            return new Point(0, 0);

#if ANDROID
        return GetAbsolutePosition_Android(view);
#elif IOS || MACCATALYST
        return GetAbsolutePosition_iOS(view);
#elif WINDOWS
        return GetAbsolutePosition_Windows(view);
#else
        return new Point(0, 0);
#endif
    }

    // -------------------------
    // ANDROID
    // -------------------------
#if ANDROID
    private static Point GetAbsolutePosition_Android(VisualElement view)
    {
        var nativeView = (Android.Views.View)view.Handler.PlatformView;
        int[] location = new int[2];
        nativeView.GetLocationOnScreen(location);

        int statusBar = GetStatusBarHeight();
        int actionBar = GetActionBarHeight(view);

        int totalInset = statusBar + actionBar;

        return new Point(
            location[0],
            location[1] - totalInset
        );
    }

    private static int GetStatusBarHeight()
    {
        int resourceId = Android.App.Application.Context.Resources
            .GetIdentifier("status_bar_height", "dimen", "android");

        if (resourceId > 0)
            return Android.App.Application.Context.Resources
                .GetDimensionPixelSize(resourceId);

        return 0;
    }

    private static int GetActionBarHeight(VisualElement view)
    {
        // Shell TopBar height
        var context = view.Handler.MauiContext?.Context;
        if (context == null) return 0;

        var tv = new Android.Util.TypedValue();
        if (context.Theme.ResolveAttribute(Android.Resource.Attribute.ActionBarSize, tv, true))
        {
            return Android.Util.TypedValue.ComplexToDimensionPixelSize(
                tv.Data, context.Resources.DisplayMetrics);
        }

        return 0;
    }
#endif

    // -------------------------
    // iOS / MACCATALYST
    // -------------------------
#if IOS || MACCATALYST
    private static Point GetAbsolutePosition_iOS(VisualElement view)
    {
        var nativeView = (UIKit.UIView)view.Handler.PlatformView;

        var p = nativeView.ConvertPointToView(new CoreGraphics.CGPoint(0, 0), null);

        // Safe area top inset (Notch / StatusBar)
        var inset = UIKit.UIApplication.SharedApplication.KeyWindow?.SafeAreaInsets.Top ?? 0;

        return new Point(p.X, p.Y - inset);
    }
#endif

    // -------------------------
    // WINDOWS
    // -------------------------
#if WINDOWS
    private static Point GetAbsolutePosition_Windows(VisualElement view)
    {
        return new Point(0, 0);
    }
#endif
}