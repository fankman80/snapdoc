using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using SnapDoc.Resources.Languages;

namespace SnapDoc.Controls;

public static class SnackbarExtensions
{
    public static async Task ShowSafeAsync(string message, string actionButtonText = "", bool includeDelay = false)
    {
        if (string.IsNullOrEmpty(actionButtonText))
            actionButtonText = AppResources.ok;

        if (includeDelay)
            await Task.Delay(100);

        // Prüfen, ob die App auf Windows läuft
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            // Alternative für Windows: Verwende einen Toast statt Snackbar
            var toast = Toast.Make(message, ToastDuration.Short);
            await toast.Show();
        }
        else
        {
            // Reguläre Snackbar für Android, iOS, macOS
            await Snackbar.Make(
                message: message,
                actionButtonText: actionButtonText,
                duration: TimeSpan.FromSeconds(3),
                visualOptions: Settings.SnackBarOptions
            ).Show();
        }
    }
}