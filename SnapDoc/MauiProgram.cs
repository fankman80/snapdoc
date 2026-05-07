using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using FFImageLoading.Maui;
using Camera.MAUI;
using MR.Gestures;
using SkiaSharp.Views.Maui.Controls.Hosting;
using System.Globalization;
using UraniumUI;
using Mopups.Hosting;


#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

#if ANDROID
using Microsoft.Maui.Platform;
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
            .ConfigureMopups()
            .UseSkiaSharp()
            .UseSentry(options =>
            {
                options.Dsn = "https://b864c3fdd54cf3fe92c37b849cb6e9cd@o4511245885308928.ingest.de.sentry.io/4511245957267536";
                options.TracesSampleRate = 0.1;
                options.EnableLogs = false;
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

        builder.Services.AddMopupsDialogs();
        builder.UseMauiCameraView();
        
#if IOS || MACCATALYST
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler>();
        });
#endif

        // Entfernt Entry Rahmen, Padding und blauer Strich
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("MyBorderlessCustomization", (handler, view) =>
        {
            if (view is BorderlessEntry)
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.MinWidth = 0;
                handler.PlatformView.MinHeight = 0;
#endif
            }
        });

        // Entfernt Editor Rahmen, Padding und blauer Strich
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("MyBorderlessEditorCustomization", (handler, view) =>
        {
            if (view is BorderlessEditor)
            {
#if ANDROID
                handler.PlatformView.Background = null;
                handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
                handler.PlatformView.Layer.BorderWidth = 0;
                handler.PlatformView.TextContainerInset = UIKit.UIEdgeInsets.Zero; // Entfernt inneres Padding
                handler.PlatformView.TextContainer.LineFragmentPadding = 0;
#elif WINDOWS
                handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Resources["TextControlBorderThemeThicknessFocused"] = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
                handler.PlatformView.MinWidth = 0;
                handler.PlatformView.MinHeight = 0;
#endif
            }
        });

        // Registriere die AppShell
        builder.Services.AddSingleton<AppShell>();

        // Registriere den FileSaver
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);

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

public partial class BorderlessEntry : Microsoft.Maui.Controls.Entry
{
}

public partial class BorderlessEditor : Microsoft.Maui.Controls.Editor
{
}