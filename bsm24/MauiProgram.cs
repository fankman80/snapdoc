using Camera.MAUI;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using FFImageLoading.Maui;
using Microsoft.Maui.LifecycleEvents;
using Mopups.Hosting;
using MR.Gestures;
using UraniumUI;

namespace bsm24;
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
            .ConfigureMopups()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddMaterialSymbolsFonts();
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

        return builder.Build();
    }
}
