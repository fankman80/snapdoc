using SnapDoc.Controls;

namespace SnapDoc.Services;

public static class BusyService
{
    private static BusyOverlayPage? _overlay;

    public static bool IsShowing => _overlay != null;


    public static async Task ShowAsync(string? message = null)
    {
        if (_overlay != null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_overlay != null)
                return;

            _overlay = new BusyOverlayPage(message);

            await Shell.Current.Navigation.PushModalAsync(
                _overlay,
                false);
        });
    }

    public static async Task SetMessageAsync(string? message)
    {
        var overlay = _overlay;

        if (overlay == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            overlay.SetMessage(message);
        });
    }

    public static async Task HideAsync()
    {
        var overlay = _overlay;

        if (overlay == null)
            return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (Shell.Current.Navigation.ModalStack.Contains(overlay))
                {
                    await Shell.Current.Navigation.PopModalAsync(false);
                }
            }
            finally
            {
                _overlay = null;
            }
        });
    }
}