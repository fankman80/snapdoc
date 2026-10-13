using System.Text.Json;

namespace SnapDoc.Services;

/// <summary>
/// Persistenter Sync-Zustand pro Projekt (Datei ".snapdoc_sync" im Projektordner).
/// Ueberlebt App-Neustarts und Projektwechsel.
/// </summary>
internal static class SyncState
{
    public const string FileName = ".snapdoc_sync";

    private static readonly Lock _lock = new();
    private static string? _projectDir;
    private static StateData _data = new();

    private sealed class StateData
    {
        public bool Dirty { get; set; }
        public List<UploadEntry> PendingUploads { get; set; } = [];
        public List<string> PendingDeletes { get; set; } = [];
        public List<string> PurgedCloudFiles { get; set; } = [];
    }

    public sealed class UploadEntry
    {
        /// <summary>Relativ zum Projektordner, sonst absolut (z.B. customicons).</summary>
        public string LocalPath { get; set; } = "";
        public string SubFolder { get; set; } = "";
    }

    // === Bindung ===================================================

    public static void Bind(string jsonFilePath)
    {
        string? dir = Path.GetDirectoryName(jsonFilePath);
        if (string.IsNullOrEmpty(dir)) return;

        lock (_lock)
        {
            if (string.Equals(dir, _projectDir, StringComparison.OrdinalIgnoreCase)) return;
            _projectDir = dir;
            _data = Load(dir);
        }
    }

    public static void Unbind()
    {
        lock (_lock)
        {
            _projectDir = null;
            _data = new StateData();
        }
    }

    // === Dirty =====================================================

    public static bool IsDirty { get { lock (_lock) return _data.Dirty; } }

    public static bool HasPendingWork
    {
        get
        {
            lock (_lock)
                return _projectDir != null &&
                       (_data.Dirty || _data.PendingUploads.Count > 0 || _data.PendingDeletes.Count > 0);
        }
    }

    public static void MarkDirty()
    {
        lock (_lock)
        {
            if (_projectDir == null || _data.Dirty) return;
            _data.Dirty = true;
            Save();
        }
    }

    public static void ClearDirty()
    {
        lock (_lock)
        {
            if (!_data.Dirty) return;
            _data.Dirty = false;
            Save();
        }
    }

    // === Uploads ===================================================

    public static void AddUpload(string localFullPath, string subFolder)
    {
        lock (_lock)
        {
            if (_projectDir == null || string.IsNullOrEmpty(localFullPath)) return;

            string stored = ToStoredPath(localFullPath);
            string sub = NormalizeCloudPath(subFolder);

            if (!_data.PendingUploads.Any(u => u.LocalPath == stored && u.SubFolder == sub))
                _data.PendingUploads.Add(new UploadEntry { LocalPath = stored, SubFolder = sub });

            // Datei wird (wieder) gebraucht -> geplante Loeschung hinfaellig
            string cloudPath = NormalizeCloudPath($"{sub}/{Path.GetFileName(localFullPath)}");
            _data.PendingDeletes.RemoveAll(p => p.Equals(cloudPath, StringComparison.OrdinalIgnoreCase));
            _data.PurgedCloudFiles.RemoveAll(p => p.Equals(cloudPath, StringComparison.OrdinalIgnoreCase));

            Save();
        }
    }

    public static List<(UploadEntry Entry, string FullPath)> GetUploads()
    {
        lock (_lock)
        {
            if (_projectDir == null) return [];
            return [.. _data.PendingUploads.Select(u => (u, ResolveStoredPath(u.LocalPath)))];
        }
    }

    public static void RemoveUpload(UploadEntry entry)
    {
        lock (_lock)
        {
            if (_data.PendingUploads.Remove(entry)) Save();
        }
    }

    // === Cloud-Loeschungen =========================================

    public static void AddDelete(string relativeCloudPath) => AddDeletes([relativeCloudPath]);

    public static void AddDeletes(IEnumerable<string> relativeCloudPaths)
    {
        lock (_lock)
        {
            if (_projectDir == null) return;

            bool changed = false;
            foreach (var raw in relativeCloudPaths)
            {
                string path = NormalizeCloudPath(raw);
                if (string.IsNullOrEmpty(path)) continue;
                if (_data.PurgedCloudFiles.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
                if (_data.PendingDeletes.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;

                _data.PendingDeletes.Add(path);
                changed = true;
            }
            if (changed) Save();
        }
    }

    public static List<string> GetPendingDeletes()
    {
        lock (_lock) return [.. _data.PendingDeletes];
    }

    public static void CompleteDelete(string relativeCloudPath)
    {
        lock (_lock)
        {
            _data.PendingDeletes.RemoveAll(p => p.Equals(relativeCloudPath, StringComparison.OrdinalIgnoreCase));
            if (!_data.PurgedCloudFiles.Contains(relativeCloudPath, StringComparer.OrdinalIgnoreCase))
                _data.PurgedCloudFiles.Add(relativeCloudPath);
            Save();
        }
    }

    public static void DropDelete(string relativeCloudPath)
    {
        lock (_lock)
        {
            if (_data.PendingDeletes.RemoveAll(p => p.Equals(relativeCloudPath, StringComparison.OrdinalIgnoreCase)) > 0)
                Save();
        }
    }

    /// <summary>Haelt die "bereits geloescht"-Liste klein.</summary>
    public static void RetainPurged(HashSet<string> stillRelevant)
    {
        lock (_lock)
        {
            if (_data.PurgedCloudFiles.RemoveAll(p => !stillRelevant.Contains(p)) > 0) Save();
        }
    }

    // === Hilfsfunktionen ===========================================

    public static string NormalizeCloudPath(string? path)
        => (path ?? "").Replace('\\', '/').Trim('/');

    private static string ToStoredPath(string fullPath)
    {
        if (_projectDir == null) return fullPath;
        string rel = Path.GetRelativePath(_projectDir, fullPath);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? fullPath : rel;
    }

    private static string ResolveStoredPath(string stored)
        => Path.IsPathRooted(stored) || _projectDir == null ? stored : Path.Combine(_projectDir, stored);

    private static StateData Load(string dir)
    {
        try
        {
            string path = Path.Combine(dir, FileName);
            if (!File.Exists(path)) return new StateData();
            return JsonSerializer.Deserialize<StateData>(File.ReadAllText(path)) ?? new StateData();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sync-Zustand konnte nicht gelesen werden: {ex.Message}");
            return new StateData();
        }
    }

    /// <summary>Atomar schreiben.</summary>
    private static void Save()
    {
        if (_projectDir == null || !Directory.Exists(_projectDir)) return;
        try
        {
            string path = Path.Combine(_projectDir, FileName);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sync-Zustand konnte nicht gespeichert werden: {ex.Message}");
        }
    }
}
