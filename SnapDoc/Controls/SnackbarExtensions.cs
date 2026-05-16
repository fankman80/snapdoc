using CommunityToolkit.Maui.Alerts;
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

        await Snackbar.Make(
            message: message,
            actionButtonText: actionButtonText,
            duration: TimeSpan.FromSeconds(3),
            visualOptions: Settings.SnackBarOptions
        ).Show();
    }
}