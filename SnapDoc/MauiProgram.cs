using Camera.MAUI;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using FFImageLoading.Maui;
using Microsoft.Maui.Platform;
using MR.Gestures;
using SkiaSharp.Views.Maui.Controls.Hosting;
using UraniumUI;
using Shiny;
using Shiny.Locations;

#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace SnapDoc;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCameraView()
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseFFImageLoading()
            .ConfigureMRGestures()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddMaterialSymbolsFonts();
            })
            .UseShiny()
            .UseGps<LocationDelegate>(true, new GpsRequest
            {
                UseBackground = false,
                Accuracy = GpsAccuracy.High,
                Interval = TimeSpan.FromSeconds(3),
                ThrottledInterval = TimeSpan.FromSeconds(3)
            });

        // Registriere die AppShell
        builder.Services.AddSingleton<AppShell>();

        // Registriere den FileSaver
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);

#if WINDOWS
        // Place Windows Screen-Center
        builder.ConfigureLifecycleEvents(events =>
        {
            events.AddWindows(windowsLifecycleBuilder =>
            {
                windowsLifecycleBuilder.OnWindowCreated(window =>
                {
                    window.ExtendsContentIntoTitleBar = false;
                    var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

                    if (appWindow is not null)
                    {
                        Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
                        if (displayArea is not null)
                        {
                            var CenteredPosition = appWindow.Position;
                            CenteredPosition.X = ((displayArea.WorkArea.Width - appWindow.Size.Width) / 2);
                            CenteredPosition.Y = ((displayArea.WorkArea.Height - appWindow.Size.Height) / 2);
                            appWindow.Move(CenteredPosition);
                        }
                    }
                });
            });
        });
#endif

#if ANDROID
        // Android Picker: Entfernt die Unterstreichung, fügt Pfeil hinzu und setzt Padding
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("AddArrowIcon", (handler, view) =>
        {
            if (handler.PlatformView is Android.Widget.EditText editText)
            {
                editText.Background = null; // Unterstreichung weg

                // Padding setzen (links, oben, rechts, unten)
                editText.SetPadding(
                    (int)handler.PlatformView.Context.ToPixels(14), // links
                    0,                                             // oben
                    (int)handler.PlatformView.Context.ToPixels(14), // rechts
                    0                                              // unten
                );

                var drawable = AndroidX.Core.Content.ContextCompat.GetDrawable(
                    editText.Context,
                    Resource.Drawable.mtrl_dropdown_arrow
                );

                if (drawable != null)
                {
                    // Theme prüfen
                    var theme = Application.Current?.RequestedTheme;

                    if (theme == AppTheme.Dark)
                        drawable.SetTint(Android.Graphics.Color.White);
                    else
                        drawable.SetTint(Android.Graphics.Color.Black);

                    // Icon rechts einsetzen
                    editText.SetCompoundDrawablesWithIntrinsicBounds(null, null, drawable, null);
                }
            }
        });
#endif

        return builder.Build();
    }
}
