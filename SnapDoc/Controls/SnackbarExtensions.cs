using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Extensions;
using SnapDoc.Resources.Languages;
using SnapDoc.Views;

namespace SnapDoc.Controls;

public static class SnackbarExtensions
{
    public static async Task ShowSafeAsync(string message, string actionButtonText = "", bool includeDelay = false)
    {
        if (string.IsNullOrEmpty(actionButtonText))
            actionButtonText = AppResources.ok;

        if (includeDelay)
            await Task.Delay(100);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                // Plattform-Verzweigung fuer Windows
                if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
                {
                    // Veralteten MainPage-Zugriff vermeiden und Fenster sicher abfragen
                    Page? activePage = Shell.Current?.CurrentPage
                        ?? Application.Current?.Windows.FirstOrDefault()?.Page;

                    if (activePage != null)
                    {
                        await activePage.ShowPopupAsync(
                            new PopupAlert(message, string.Empty, actionButtonText),
                            Settings.PopupOptions
                        );
                    }
                }
                else
                {
                    await Snackbar.Make(
                        message: message,
                        actionButtonText: actionButtonText,
                        duration: TimeSpan.FromSeconds(3),
                        visualOptions: Settings.SnackBarOptions
                    ).Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler bei der Toast-Anzeige: {ex.Message}");
            }
        });
    }
}