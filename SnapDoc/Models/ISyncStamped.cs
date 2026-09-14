using SnapDoc.Services;

namespace SnapDoc.Models;

public interface ISyncStamped
{
    DateTimeOffset ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

public static class SyncStampExtensions
{
    public static bool IsDeleted(this ISyncStamped? item) => item?.DeletedAt != null;
    public static bool IsLive(this ISyncStamped? item) => item != null && item.DeletedAt == null;

    public static void Touch(this ISyncStamped? item)
    {
        if (item == null || SyncStampGate.IsSuspended) return;
        item.ModifiedAt = SyncClock.Now();
        item.ModifiedBy = SyncClock.DeviceId;
    }

    /// <summary>Setzt die Tombstone. Ersetzt jedes bisherige Remove().</summary>
    public static void MarkDeleted(this ISyncStamped? item)
    {
        if (item == null || item.DeletedAt != null) return;
        var now = SyncClock.Now();
        item.DeletedAt = now;
        item.ModifiedAt = now;
        item.ModifiedBy = SyncClock.DeviceId;
    }

    public static class SyncStampGate
    {
        private static readonly AsyncLocal<int> _depth = new();
        public static bool IsSuspended => _depth.Value > 0;

        public static IDisposable Suspend()
        {
            _depth.Value++;
            return new Scope();
        }

        private sealed class Scope : IDisposable
        {
            private bool _done;
            public void Dispose()
            {
                if (_done) return;
                _done = true;
                _depth.Value--;
            }
        }
    }
}