#nullable enable
namespace SnapDoc.Reporting;

/// <summary>Welche Daten ein Platzhalter benötigt bzw. welche eine Wiederholungsquelle liefert.</summary>
[Flags]
public enum ScopeKind
{
    None = 0,
    Project = 1,
    Plan = 2,
    Pin = 4,
    Photo = 8,
    Priority = 16
}

/// <summary>Unveränderlicher Schnappschuss aller Einstellungen, die der Export braucht.</summary>
public sealed record ExportOptions(
    bool ExportPlans,
    bool ExportPosImages,
    bool ExportPhotos,
    bool PhotoOverlay,
    int MaxPlanSize,
    int PlanQuality,
    int MaxPhotoSize,
    int PhotoQuality,
    float PinExportSize,
    float PinPosCropSize,
    string PinLabelPrefix,
    double PinLabelFontSize,
    string DefaultPinIcon,
    string DataDirectory,
    string ProjectDirectory,
    string PlanDirectory,
    string ImageDirectory,
    string CustomPinsRelativePath);

public sealed record ProjectInfo(
    string ClientName,
    string ObjectAddress,
    string WorkingTitle,
    string ProjectNr,
    string ObjectName,
    string ProjectManager,
    DateTime CreationDate,
    string? TitleImageFile,
    SizeF TitleImageSize,
    DateTimeOffset ModifiedAt);

public sealed record ExportFoto(
    string File,
    SizeF ImageSize,
    bool HasOverlay,
    DateTime CapturedAt,
    DateTimeOffset ModifiedAt);

public sealed record ExportPin(
    int Number,
    string Name,
    string Description,
    string Location,
    string CreatedBy,
    string PriorityKey,
    string? PriorityColorHex,
    string? Wgs84,
    string? Ch1903,
    double? Latitude,
    double? Longitude,
    double? Ch1903X,
    double? Ch1903Y,
    GeolocationAccuracy? GeoAccuracy,
    DateTimeOffset? GeoTimestamp,
    DateTime CapturedAt,
    string IconFile,
    string IconCacheKey,
    bool IsCustomPin,
    bool IsCustomIcon,
    bool IsWebMapPin,
    bool IsLockAutoScale,
    float PinScale,
    float Rotation,
    PointF Position,
    PointF Anchor,
    SizeF Size,
    string ColorHex,
    string? MapImageFile,
    IReadOnlyList<ExportFoto> Photos,
    DateTimeOffset ModifiedAt);

public sealed record ExportPriority(
    string Name,
    string ColorHex,
    int PinCount);

public sealed record ExportPlan(
    string Key,
    string Name,
    string Description,
    string File,
    SizeF ImageSize,
    bool IsGrayscale,
    bool IsInvertColor,
    bool IsWebMap,
    string MeasureUnit,
    DateTimeOffset ModifiedAt,
    IReadOnlyList<ExportPin> Pins);

/// <summary>Alle zu exportierenden Daten (bereits gefiltert nach AllowExport, fortlaufend nummeriert).</summary>
public sealed class ReportDataContext(ProjectInfo project, ExportOptions options, IReadOnlyList<ExportPlan> plans, IReadOnlyList<ExportPriority> priorities)
{
    public ProjectInfo Project { get; } = project;
    public ExportOptions Options { get; } = options;
    public IReadOnlyList<ExportPlan> Plans { get; } = plans;
    public IReadOnlyList<ExportPriority> Priorities { get; } = priorities;
    public IEnumerable<ExportPin> AllPins => Plans.SelectMany(plan => plan.Pins);
}

/// <summary>Der aktuelle Datenkontext beim Rendern (Projekt → Plan → Pin → Foto).</summary>
public sealed record RenderScope(
    ReportDataContext Data,
    ExportPlan? Plan = null,
    ExportPin? Pin = null,
    ExportFoto? Photo = null,
    ExportPriority? Priority = null,
    int RowNumber = 0,
    int RowCount = 0)
{
    public static RenderScope ForProject(ReportDataContext data) => new(data);

    public ScopeKind Provided =>
        ScopeKind.Project
        | (Plan is null ? ScopeKind.None : ScopeKind.Plan)
        | (Pin is null ? ScopeKind.None : ScopeKind.Pin)
        | (Photo is null ? ScopeKind.None : ScopeKind.Photo)
        | (Priority is null ? ScopeKind.None : ScopeKind.Priority);

    public bool Satisfies(ScopeKind required) => (required & ~Provided) == ScopeKind.None;
}
