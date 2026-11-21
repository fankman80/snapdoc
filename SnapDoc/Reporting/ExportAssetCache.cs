#nullable enable
using SnapDoc.Services;

namespace SnapDoc.Reporting;

/// <summary>
/// Bereitet alle Bilddateien eines Exports vor (verkleinerte Fotos/Pläne, Pin-Icons) und löst Dateipfade auf.
/// Jeder Export bekommt ein eigenes Cache-Verzeichnis, das nur von diesem Export wieder gelöscht wird.
/// </summary>
public partial class ExportAssetCache(ExportOptions options) : IDisposable
{
    private readonly ExportOptions _options = options;
    private HashSet<string> _known = [with(StringComparer.OrdinalIgnoreCase)];
    private sealed record WorkItem(string SourcePath, string TargetPath, int MaxDimension);
    public string RootDirectory { get; } = Path.Combine(Settings.CacheDirectory, "report_" + Guid.NewGuid().ToString("N"));

    // ---------------------------------------------------------------- Vorbereitung

    public async Task PrepareAsync(ReportDataContext data, CancellationToken ct)
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(Path.Combine(RootDirectory, "customicons"));

        // Dateisuche läuft parallel zur Bildvorbereitung
        var scanTask = Task.Run(ScanProjectFiles, ct);

        await Task.WhenAll(
            PreProcessPhotosAsync(data, ct),
            PreProcessPlansAsync(data, ct));

        _known = await scanTask;

        await CopyPinIconsAsync(data, ct);

        foreach (var file in Directory.GetFiles(RootDirectory, "*", SearchOption.AllDirectories))
            _known.Add(file);
    }

    private HashSet<string> ScanProjectFiles()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(_options.ProjectDirectory))
            foreach (var f in Directory.GetFiles(_options.ProjectDirectory, "*.*", SearchOption.AllDirectories))
                set.Add(f);
        return set;
    }

    private async Task PreProcessPhotosAsync(ReportDataContext data, CancellationToken ct)
    {
        int maxDim = _options.MaxPhotoSize;
        if (maxDim <= 0) return;

        var work = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var photo in data.AllPins.SelectMany(p => p.Photos))
        {
            string source = Path.Combine(_options.ImageDirectory, photo.File);
            if (!_options.PhotoOverlay && photo.HasOverlay)
                source = Path.Combine(_options.ImageDirectory, "originals", photo.File);

            string target = Path.Combine(RootDirectory, Path.GetFileName(photo.File));
            work.TryAdd(target, new WorkItem(source, target, maxDim));
        }

        await RunResizeAsync(work.Values, _options.PhotoQuality, ct);
    }

    private async Task PreProcessPlansAsync(ReportDataContext data, CancellationToken ct)
    {
        int maxDim = _options.MaxPlanSize;
        if (maxDim <= 0) return;

        var work = new Dictionary<string, WorkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in data.Plans.Where(p => !string.IsNullOrEmpty(p.File)))
        {
            string source = Path.Combine(_options.PlanDirectory, plan.File);
            string target = Path.Combine(RootDirectory, Path.GetFileName(plan.File));
            work.TryAdd(target, new WorkItem(source, target, maxDim));
        }

        await RunResizeAsync(work.Values, _options.PlanQuality, ct);
    }

    private static async Task RunResizeAsync(IEnumerable<WorkItem> items, int quality, CancellationToken ct)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        int maxDegree = Math.Max(1, Environment.ProcessorCount - 1);
        await Parallel.ForEachAsync(
            list,
            new ParallelOptions { MaxDegreeOfParallelism = maxDegree, CancellationToken = ct },
            (work, _) =>
            {
                if (File.Exists(work.SourcePath))
                    Helper.BitmapResizer(work.SourcePath, work.TargetPath, work.MaxDimension, quality);
                return ValueTask.CompletedTask;
            });
    }

    private async Task CopyPinIconsAsync(ReportDataContext data, CancellationToken ct)
    {
        var icons = data.AllPins
            .Where(p => !string.IsNullOrEmpty(p.IconFile))
            .Select(p => p.IconCacheKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string icon in icons)
        {
            ct.ThrowIfCancellationRequested();

            if (icon.Contains("custompin_", StringComparison.OrdinalIgnoreCase))
                await CopyIconAsync(_options.CustomPinsRelativePath, icon);
            else if (icon.Contains("customicons", StringComparison.OrdinalIgnoreCase))
                await CopyIconAsync("customicons", Path.GetFileName(icon));
            else
                await MauiResourceLoader.CopyAppPackageFileAsync(RootDirectory, icon);
        }
    }

    private async Task CopyIconAsync(string relativeSourceDirectory, string icon)
    {
        string destination = Path.Combine(RootDirectory, icon);
        string source = Path.Combine(_options.DataDirectory, relativeSourceDirectory, icon);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        if (File.Exists(source))
        {
            File.Copy(source, destination, true);
            return;
        }

        // Fallback: Standard-Icon aus den App-Ressourcen
        string fallback = Path.Combine(RootDirectory, _options.DefaultPinIcon);
        if (!File.Exists(fallback))
            await MauiResourceLoader.CopyAppPackageFileAsync(RootDirectory, _options.DefaultPinIcon);

        if (File.Exists(fallback) && !string.Equals(destination, fallback, StringComparison.OrdinalIgnoreCase))
            File.Copy(fallback, destination, true);
    }

    // ---------------------------------------------------------------- Pfadauflösung

    public bool Exists(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return _known.Contains(path) || File.Exists(path);
    }

    public string ResolveTitleImage(ProjectInfo project) => Path.Combine(_options.ImageDirectory, project.TitleImageFile ?? string.Empty);

    public string ResolvePlanImage(ExportPlan plan)
    {
        string original = Path.Combine(_options.PlanDirectory, plan.File);
        string cached = Path.Combine(RootDirectory, plan.File);
        return _options.MaxPlanSize > 0 && Exists(cached) ? cached : original;
    }

    public string ResolvePhoto(ExportFoto photo)
    {
        string path = Path.Combine(_options.ImageDirectory, photo.File);
        string cached = Path.Combine(RootDirectory, Path.GetFileName(photo.File));

        if (_options.MaxPhotoSize > 0 && Exists(cached))
            return cached;
        if (!_options.PhotoOverlay && photo.HasOverlay)
            return Path.Combine(_options.ImageDirectory, "originals", photo.File);
        return path;
    }

    public string ResolvePinIcon(ExportPin pin) => Path.Combine(RootDirectory, pin.IconFile);

    public string? ResolveMapImage(ExportPin pin) =>
        pin.MapImageFile is null ? null : Path.Combine(_options.ImageDirectory, pin.MapImageFile);

    // ---------------------------------------------------------------- Aufräumen

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootDirectory))
                Directory.Delete(RootDirectory, true);
        }
        catch (IOException)
        {
            // Aufräumen ist best effort
        }
        catch (UnauthorizedAccessException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
