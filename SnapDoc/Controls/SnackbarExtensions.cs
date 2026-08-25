using SnapDoc.Resources.Languages;

#if WINDOWS
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
#else
using CommunityToolkit.Maui.Alerts;
#endif

namespace SnapDoc.Controls;

public static class SnackbarExtensions
{
    public static async Task ShowSafeAsync(string message, string title = "", string actionButtonText = "", bool includeDelay = false)
    {
        if (string.IsNullOrEmpty(actionButtonText))
            actionButtonText = AppResources.ok;

        if (includeDelay)
            await Task.Delay(100);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
#if WINDOWS
                // Statischer Aufruf direkt über den Typnamen "AppNotificationManager"
                if (AppNotificationManager.IsSupported())
                {
                    var xml = new AppNotificationBuilder()
                        .AddText(title)
                        .AddText(message)
                        .BuildNotification();

                    AppNotificationManager.Default.Show(xml);
                }
#else
                await Snackbar.Make(
                    message: message,
                    actionButtonText: actionButtonText,
                    duration: TimeSpan.FromSeconds(3),
                    visualOptions: Settings.SnackBarOptions
                ).Show();
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler bei der Toast-Anzeige: {ex.Message}");
            }
        });
    }
}