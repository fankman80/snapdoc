#nullable enable
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace SnapDoc.Reporting;

public enum ReportMessageSeverity { Info, Warning, Error }

public sealed record ReportMessage(ReportMessageSeverity Severity, string Text, string? Location = null)
{
    public override string ToString() => Location is null ? $"[{Severity}] {Text}" : $"[{Severity}] {Location}: {Text}";
}

public sealed record ReportExportResult(bool Success, string? OutputPath, IReadOnlyList<ReportMessage> Messages, Exception? Exception = null);

/// <summary>
/// Zustand genau eines Exportvorgangs. Ersetzt die früheren statischen Felder
/// (imageRelationshipIds, storeItemId) – parallele Exporte stören sich nicht mehr.
/// </summary>
public sealed class ExportSession
{
    private readonly Dictionary<(OpenXmlPart Part, string Path), string> _imageIds = [];
    private readonly HashSet<string> _messageKeys = [];
    private readonly List<ReportMessage> _messages = [];
    private uint _nextDrawingId;
    private int _nextShapeId = 1;
    public string TemplatePath { get; }
    public string OutputPath { get; }
    public string TemplateName => Path.GetFileName(TemplatePath);
    public string ReportFileName => Path.GetFileName(OutputPath);
    public DateTime ExportedAt { get; }

    public ExportSession(TemplateDocument template, ReportDataContext data, ExportAssetCache assets, string templatePath, string outputPath)
    {
        Template = template;
        Data = data;
        Assets = assets;

        TemplatePath = templatePath;
        OutputPath = outputPath;
        ExportedAt = DateTime.Now;

        uint maxId = template.Roots
            .SelectMany(root => root.Descendants<WP.DocProperties>())
            .Select(properties => properties.Id?.Value ?? 0u)
            .DefaultIfEmpty(0u)
            .Max();

        _nextDrawingId = maxId + 1;
    }

    public TemplateDocument Template { get; }
    public ReportDataContext Data { get; }
    public ExportOptions Options => Data.Options;
    public ExportAssetCache Assets { get; }
    public string StoreItemId { get; private set; } = string.Empty;
    public IReadOnlyList<ReportMessage> Messages => _messages;
    public void InitializePinStore() => StoreItemId = PinNumberStore.Write(Template.MainPart, Data);
    public uint NextDrawingId() => _nextDrawingId++;
    public int NextShapeId() => _nextShapeId++;

    // ---------------------------------------------------------------- Meldungen

    public void Report(ReportMessageSeverity severity, string text, string? location = null)
    {
        if (_messageKeys.Add($"{severity}|{location}|{text}"))
            _messages.Add(new ReportMessage(severity, text, location));
    }

    public void Warn(string text, string? location = null) => Report(ReportMessageSeverity.Warning, text, location);
    public void Error(string text, string? location = null) => Report(ReportMessageSeverity.Error, text, location);

    // ---------------------------------------------------------------- Bilder

    /// <summary>
    /// Liefert die Relationship-Id eines Bildes im Part (Body/Kopf-/Fusszeile), in dem <paramref name="host"/> liegt.
    /// Jedes Bild wird pro Part nur einmal eingebettet.
    /// </summary>
    public string GetImageRelationshipId(OpenXmlElement host, string imagePath)
    {
        OpenXmlElement? root = host as OpenXmlPartRootElement
            ?? host.Ancestors().FirstOrDefault(a => a is W.Document || a is W.Header || a is W.Footer);

        OpenXmlPart part = root switch
        {
            W.Header h when h.HeaderPart != null => h.HeaderPart,
            W.Footer f when f.FooterPart != null => f.FooterPart,
            _ => Template.MainPart
        };

        if (_imageIds.TryGetValue((part, imagePath), out string? existing))
            return existing;

        var type = Path.GetExtension(imagePath).ToLowerInvariant() switch
        {
            ".png" => ImagePartType.Png,
            ".gif" => ImagePartType.Gif,
            ".bmp" => ImagePartType.Bmp,
            ".tif" or ".tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Jpeg
        };

        ImagePart imagePart = part switch
        {
            MainDocumentPart m => m.AddImagePart(type),
            HeaderPart h => h.AddImagePart(type),
            FooterPart f => f.AddImagePart(type),
            _ => throw new InvalidOperationException("Unbekannter Dokumentteil für Bild.")
        };

        using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                   bufferSize: 65536, options: FileOptions.SequentialScan))
            imagePart.FeedData(stream);

        string id = part.GetIdOfPart(imagePart);
        _imageIds[(part, imagePath)] = id;
        return id;
    }
}
