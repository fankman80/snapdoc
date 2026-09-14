using SnapDoc.Models;

namespace SnapDoc.Services;

public static class SyncClock
{
    private static readonly Lock _lock = new();
    private static DateTimeOffset _last = DateTimeOffset.MinValue;
    private static string? _deviceId;

    /// <summary>Stabile ID dieses Geräts – überlebt App-Neustarts.</summary>
    public static string DeviceId
    {
        get
        {
            if (!string.IsNullOrEmpty(_deviceId)) return _deviceId;

            _deviceId = Preferences.Get("snapdoc_device_id", string.Empty);
            if (string.IsNullOrEmpty(_deviceId))
            {
                _deviceId = Guid.NewGuid().ToString("N")[..12];
                Preferences.Set("snapdoc_device_id", _deviceId);
            }
            return _deviceId;
        }
    }

    /// <summary>
    /// Streng monoton steigende Zeitstempel. Schützt gegen Rücksprünge
    /// der Systemzeit (NTP-Korrektur, manuelles Verstellen) – sonst könnte
    /// eine neue Änderung älter aussehen als eine alte und beim Merge
    /// verworfen werden.
    /// </summary>
    public static DateTimeOffset Now()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now <= _last) now = _last.AddTicks(1);
            _last = now;
            return now;
        }
    }

    /// <summary>
    /// > 0 => "a" ist neuer und gewinnt. Bei gleichem Zeitstempel
    /// entscheidet die Geräte-ID, damit BEIDE Geräte unabhängig
    /// voneinander zum selben Ergebnis kommen.
    /// </summary>
    public static int Compare(ISyncStamped? a, ISyncStamped? b)
    {
        if (a == null || b == null) return 0;
        int cmp = a.ModifiedAt.CompareTo(b.ModifiedAt);
        return cmp != 0 ? cmp : string.CompareOrdinal(a.ModifiedBy ?? "", b.ModifiedBy ?? "");
    }

    /// <summary>
    /// Kollisionsfreie ID. Ersetzt das bisherige
    /// DateTime.Now.ToString("yyyyMMdd_HHmmss") – siehe Schritt 7.3.
    /// </summary>
    public static string NewId()
        => $"{DateTime.Now:yyyyMMdd_HHmmss}_{DeviceId[..4]}{Random.Shared.Next(0x1000, 0xFFFF):x}";
}