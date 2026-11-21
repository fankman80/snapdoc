#nullable enable
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

/// <summary>
/// Vervielfältigt Tabellenzeilen, die einen Wiederholungsmarker enthalten, z. B. ${repeat:pins}.
/// Es wird keine Tabelle mehr über einen Namen gesucht: Jede Tabelle (auch mehrere, auch in Kopf-/Fusszeilen
/// oder verschachtelt) wird anhand ihrer Vorlagenzeile erkannt. Die Zeile wird komplett geklont,
/// dadurch bleiben Zellformatierung, mehrere Absätze, Verbindungen, Listen usw. erhalten.
/// </summary>
public sealed class WordTableRenderer(
    ExportSession session,
    PlaceholderRegistry registry,
    TemplateParser parser,
    WordTextRenderer textRenderer)
{
    public void RenderTables(OpenXmlElement root, RenderScope scope, CancellationToken ct = default)
    {
        // Momentaufnahme: Die Liste wird verändert, während wir Zeilen ersetzen.
        foreach (var row in root.Descendants<W.TableRow>().ToList())
        {
            ct.ThrowIfCancellationRequested();

            var markers = parser.FindRepeatMarkers(row);
            if (markers.Count != 1)
                continue; // 0 = normale Zeile, >1 = vom Validator bereits als Fehler gemeldet

            // Zeilen innerhalb einer Vorlagenzeile werden rekursiv über deren Klon behandelt.
            bool insideOtherTemplateRow = row.Ancestors<W.TableRow>()
                .TakeWhile(a => !ReferenceEquals(a, root))
                .Any(a => parser.FindRepeatMarkers(a).Count > 0);
            if (insideOtherTemplateRow)
                continue;

            ExpandRow(row, markers[0], scope, ct);
        }
    }

    private void ExpandRow(W.TableRow templateRow, PlaceholderToken marker, RenderScope scope, CancellationToken ct)
    {
        if (!registry.TryGetSource(marker.Qualifier, out var source))
        {
            session.Error($"Unbekannte Wiederholungsquelle „{marker.Qualifier}“.", marker.Raw);
            return;
        }

        if (!scope.Satisfies(source.Requires))
        {
            session.Warn($"„{marker.Raw}“ hat an dieser Stelle keine Datengrundlage.", marker.Raw);
            return;
        }

        var items = source.Expand(scope, marker.Arguments).ToList();

        foreach (var itemScope in items)
        {
            ct.ThrowIfCancellationRequested();

            var clone = (W.TableRow)templateRow.CloneNode(true);

            // Erst einfügen, dann füllen: Bilder brauchen den Dokumentteil (Body/Kopfzeile/…) des Elements.
            templateRow.InsertBeforeSelf(clone);

            RenderTables(clone, itemScope, ct);       // verschachtelte Wiederholungen
            textRenderer.RenderRoot(clone, itemScope, ct);
        }

        templateRow.Remove();

        if (items.Count == 0)
            session.Report(ReportMessageSeverity.Info, $"„{marker.Raw}“: keine Daten, Tabellenzeile wurde entfernt.", marker.Raw);
    }
}
