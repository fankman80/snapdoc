namespace SnapDoc.Services;

/// <summary>
/// Merkt sich, ob gerade ein Popup offen ist, damit Seiten in OnDisappearing
/// nicht fälschlich aufräumen.
/// </summary>
public static class PopupTracker
{
    private static int _openCount;

    public static bool IsShowing => Volatile.Read(ref _openCount) > 0;

    public static async Task<T> RunAsync<T>(Func<Task<T>> showPopup)
    {
        Interlocked.Increment(ref _openCount);
        try
        {
            return await showPopup();
        }
        finally
        {
            Interlocked.Decrement(ref _openCount);
        }
    }
}
