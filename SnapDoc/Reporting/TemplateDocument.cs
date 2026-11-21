#nullable enable
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

namespace SnapDoc.Reporting;

/// <summary>
/// Die Word-Vorlage im Speicher. Die Originaldatei wird nie verändert.
/// <see cref="Roots"/> enthält Body, alle Kopf- und alle Fusszeilen.
/// </summary>
public partial class TemplateDocument : IDisposable
{
    private readonly MemoryStream _stream;
    private bool _documentDisposed;
    public WordprocessingDocument Document { get; }
    public MainDocumentPart MainPart => Document.MainDocumentPart!;
    public IReadOnlyList<OpenXmlElement> Roots { get; }
    private TemplateDocument(MemoryStream stream, WordprocessingDocument document)
    {
        _stream = stream;
        Document = document;

        var roots = new List<OpenXmlElement>();
        var main = document.MainDocumentPart!;
        if (main.Document?.Body != null)
            roots.Add(main.Document.Body);
        foreach (var header in main.HeaderParts)
            if (header.Header != null) roots.Add(header.Header);
        foreach (var footer in main.FooterParts)
            if (footer.Footer != null) roots.Add(footer.Footer);
        Roots = roots;
    }

    public static TemplateDocument Open(string templatePath)
    {
        var stream = new MemoryStream();
        using (var file = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            file.CopyTo(stream);
        stream.Position = 0;

        var document = WordprocessingDocument.Open(stream, true);
        if (document.MainDocumentPart == null)
        {
            document.Dispose();
            stream.Dispose();
            throw new InvalidDataException("Die Vorlage ist keine gültige Word-Datei (kein Hauptdokument).");
        }
        return new TemplateDocument(stream, document);
    }

    /// <summary>Speichert das bearbeitete Dokument. Danach ist die Instanz nicht mehr verwendbar.</summary>
    public void SaveTo(string outputPath)
    {
        Document.Save();
        Document.Dispose(); // schreibt das Paket in den MemoryStream
        _documentDisposed = true;

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _stream.Position = 0;
        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        _stream.CopyTo(output);
    }

    public void Dispose()
    {
        if (!_documentDisposed)
        {
            Document.Dispose();
            _documentDisposed = true;
        }

        _stream.Dispose();

        GC.SuppressFinalize(this);
    }
}
