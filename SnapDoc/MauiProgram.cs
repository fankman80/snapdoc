using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using FFImageLoading.Maui;
using Microsoft.Maui.Platform;
using Camera.MAUI;
using MR.Gestures;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Globalization;
using UraniumUI;

#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace SnapDoc;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SetLanguage();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseFFImageLoading()
            .ConfigureMRGestures()
            .UseSkiaSharp()
            .UseSentry(options =>
            {
                options.Dsn = "https://b864c3fdd54cf3fe92c37b849cb6e9cd@o4511245885308928.ingest.de.sentry.io/4511245957267536";
                options.TracesSampleRate = 1.0;
                options.EnableLogs = true;
                options.AttachScreenshot = true;
                options.AttachStacktrace = true;
                options.IncludeBackgroundingStateInBreadcrumbs = true;
                options.IncludeTextInBreadcrumbs = true;
                options.IncludeTitleInBreadcrumbs = true;
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("OpenSans-Italic.ttf", "OpenSansItalic");
                fonts.AddFont("OpenSans-BoldItalic.ttf", "OpenSansBoldItalic");
                fonts.AddFont("MaterialSymbolsOutlined-Light.ttf", "MaterialOutlined");
            });

        builder.UseMauiCameraView();
        
#if IOS || MACCATALYST
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler>();
        });
#endif

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

    private static void SetLanguage()
    {
        string iniPath = Path.Combine(Settings.DataDirectory, "appsettings.ini");
        string lang = "system"; // Standardwert auf "system" ändern

        if (File.Exists(iniPath))
        {
            try
            {
                using var reader = new StreamReader(iniPath);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("\"SelectedAppLanguage\":", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':');
                        if (parts.Length > 1)
                        {
                            string cleanValue = parts[1].Replace("\"", "").Replace(",", "").Trim();
                            if (int.TryParse(cleanValue, out int index))
                                if (index >= 0 && index < Settings.Languages.Count)
                                    lang = Settings.Languages.Keys.ElementAt(index);
                        }
                        break;
                    }
                }
            }
            catch
            {
                lang = "system";
            }
        }

        if (lang != "system" && !string.IsNullOrWhiteSpace(lang))
        {
            try
            {
                var culture = new CultureInfo(lang);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch
            {
            }
        }
    }
}
