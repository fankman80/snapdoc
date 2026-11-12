using Android.App;
using Android.Runtime;

namespace SnapDoc;

[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp()
    {
        // Remove Entry control underline
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            "NoUnderline", (h, v) =>
            {
                h.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(
                        Android.Graphics.Color.Transparent);
            });

        // Remove Editor control underline
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(
            "NoUnderline", (h, v) =>
            {
                h.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(
                        Android.Graphics.Color.Transparent);
            });

        // Remove DatePicker control underline
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping(
            "NoUnderline", (h, v) =>
            {
                h.PlatformView.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(
                        Android.Graphics.Color.Transparent);
            });

        return MauiProgram.CreateMauiApp();
    }
}