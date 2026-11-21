#nullable enable
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

public enum PlaceholderKind
{
    /// <summary>Liefert Text (unterstützt prefix/suffix/fallback).</summary>
    Text,
    /// <summary>Liefert fertige Word-Elemente (Bilder, Content Controls …) an der Platzhalterposition.</summary>
    Inline,
    /// <summary>Fügt ganze Absätze nach dem Platzhalter-Absatz ein (z. B. Planbilder).</summary>
    Block,
    /// <summary>Steuerzeichen ohne Ausgabe (z. B. ${repeat:pins}).</summary>
    Marker
}

/// <summary>Alles, was ein Resolver braucht, um einen Platzhalter zu füllen.</summary>
public sealed record PlaceholderCall(
    PlaceholderToken Token,
    RenderScope Scope,
    W.Paragraph Paragraph,
    W.RunProperties? RunProperties,
    ExportSession Session)
{
    public ArgumentSet Args => Token.Arguments;
    public ReportDataContext Data => Scope.Data;

    /// <summary>Neuer Run mit der Formatierung der Stelle, an der der Platzhalter stand.</summary>
    public W.Run NewRun()
    {
        var run = new W.Run();
        if (RunProperties != null)
            run.RunProperties = (W.RunProperties)RunProperties.CloneNode(true);
        return run;
    }
}

public sealed class PlaceholderResult
{
    public static readonly PlaceholderResult Empty = new()
    {
        Elements = Array.Empty<OpenXmlElement>()
    };

    public string? Text { get; private init; }

    public IReadOnlyList<OpenXmlElement>? Elements
    {
        get;
        private init;
    }

    public IReadOnlyList<OpenXmlElement> Blocks
    {
        get;
        private init;
    } = Array.Empty<OpenXmlElement>();

    /// <summary>
    /// Hintergrundfarbe (RRGGBB) für die umgebende Tabellenzelle,
    /// z. B. Priorität.
    /// </summary>
    public string? CellFillHex { get; private init; }

    public static PlaceholderResult FromText(
        string? text,
        string? cellFillHex = null) =>
        new()
        {
            Text = text ?? string.Empty,
            CellFillHex = cellFillHex
        };

    public static PlaceholderResult FromElement(
        OpenXmlElement element) =>
        new()
        {
            Elements = new OpenXmlElement[] { element }
        };

    public static PlaceholderResult FromElements(
        IEnumerable<OpenXmlElement> elements) =>
        new()
        {
            Elements = elements.ToArray()
        };

    public static PlaceholderResult FromBlocks(
        IEnumerable<OpenXmlElement> blocks) =>
        new()
        {
            Elements = Array.Empty<OpenXmlElement>(),
            Blocks = blocks.ToArray()
        };
}

public sealed record PlaceholderDefinition(
    string Name,
    PlaceholderKind Kind,
    ScopeKind RequiredScope,
    IReadOnlyList<string> AllowedArguments,
    Func<PlaceholderCall, PlaceholderResult> Resolve);

/// <summary>Definiert, worüber ${repeat:name} iteriert.</summary>
public sealed record RepeatSourceDefinition(
    string Name,
    ScopeKind Provides,
    ScopeKind Requires,
    IReadOnlyList<string> AllowedArguments,
    Func<RenderScope, ArgumentSet, IEnumerable<RenderScope>> Expand);

/// <summary>
/// Zentrale Liste aller Platzhalter und Wiederholungsquellen.
/// Neuer Platzhalter = ein Register()-Aufruf, kein Eingriff in Renderer oder Parser.
/// </summary>
public sealed class PlaceholderRegistry
{
    private readonly Dictionary<string, PlaceholderDefinition> _placeholders = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, RepeatSourceDefinition> _sources = [with(StringComparer.OrdinalIgnoreCase)];

    public void Register(PlaceholderDefinition definition) => _placeholders[definition.Name] = definition;
    public void RegisterSource(RepeatSourceDefinition definition) => _sources[definition.Name] = definition;

    public bool TryGet(string name, out PlaceholderDefinition definition) =>
        _placeholders.TryGetValue(name, out definition!);

    public bool TryGetSource(string? name, out RepeatSourceDefinition source)
    {
        source = null!;
        return name != null && _sources.TryGetValue(name, out source!);
    }

    public IEnumerable<string> PlaceholderNames => _placeholders.Keys;
    public IEnumerable<string> SourceNames => _sources.Keys;
}
