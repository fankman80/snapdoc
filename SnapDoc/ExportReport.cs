#nullable enable
using SnapDoc.Reporting;

namespace SnapDoc;

/// <summary>
/// Dünne Kompatibilitätsschicht: ersetzt die alte, 1100 Zeilen lange ExportReport-Klasse.
/// Bestehende Aufrufe <c>await ExportReport.DocX(vorlage, ziel)</c> funktionieren weiter.
/// </summary>
public static class ExportReport
{
    private static readonly ReportExporter Exporter = new();

    public static async Task<ReportExportResult> DocX(string templateDoc, string savePath, CancellationToken ct = default)
    {
        var result = await ReportExporter.ExportAsync(templateDoc, savePath, ct);

        foreach (var message in result.Messages)
            System.Diagnostics.Debug.WriteLine($"Report-Export {message}");

        return result;
    }
}
