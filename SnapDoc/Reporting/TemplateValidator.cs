#nullable enable
using System.Globalization;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

public sealed record TemplateValidationResult(IReadOnlyList<ReportMessage> Messages)
{
    public bool HasErrors => Messages.Any(m => m.Severity == ReportMessageSeverity.Error);
}

/// <summary>
/// Prüft die Vorlage VOR dem Export und erzeugt verständliche Meldungen
/// (statt dass Fehler im Export still verschwinden).
/// Strukturfehler (z. B. ${repeat} ausserhalb einer Tabelle) sind Errors und brechen den Export ab,
/// alles andere (Tippfehler, ungültige Grössen) sind Warnings.
/// </summary>
public sealed class TemplateValidator(PlaceholderRegistry registry, TemplateParser parser)
{
    private static readonly CultureInfo SwissCulture = new("de-CH");

    public TemplateValidationResult Validate(TemplateDocument template)
    {
        var messages = new List<ReportMessage>();

        foreach (var root in template.Roots)
        {
            var tableNumbers = root.Descendants<W.Table>()
                .Select((t, i) => (t, i))
                .ToDictionary(x => (OpenXmlElement)x.t, x => x.i + 1);

            foreach (var row in root.Descendants<W.TableRow>())
            {
                var markers = parser.FindRepeatMarkers(row);
                if (markers.Count > 1)
                    messages.Add(Error($"Mehr als ein ${{repeat}} in derselben Tabellenzeile ({markers.Count} gefunden). Pro Zeile ist nur eine Quelle möglich.",
                        DescribeRow(root, row, tableNumbers)));
            }

            foreach (var paragraph in root.Descendants<W.Paragraph>())
            {
                string text = ParagraphText.GetText(paragraph);
                if (!text.Contains("${", StringComparison.Ordinal)) continue;

                string location = Describe(root, paragraph, text, tableNumbers);
                var tokens = parser.FindTokens(text);

                int opened = CountOccurrences(text, "${");
                if (opened > tokens.Count)
                    messages.Add(Warning("Unvollständiger oder ungültiger Platzhalter (fehlende „}“ oder ungültige Zeichen im Namen).", location));

                var available = ProvidedScope(paragraph);
                foreach (var token in tokens)
                    ValidateToken(token, paragraph, available, location, messages);
            }
        }

        return new TemplateValidationResult(messages);
    }

    // ------------------------------------------------------------------ einzelne Platzhalter

    private void ValidateToken(PlaceholderToken token, W.Paragraph paragraph, ScopeKind available, string location, List<ReportMessage> messages)
    {
        if (!registry.TryGet(token.Name, out var def))
        {
            string? suggestion = Suggest(token.Name);
            messages.Add(Warning(
                $"Unbekannter Platzhalter „{token.Raw}“." + (suggestion != null ? $" Meinten Sie „${{{suggestion}}}“?" : string.Empty),
                location));
            return;
        }

        bool inTable = paragraph.Ancestors<W.TableRow>().Any();

        if (def.Kind == PlaceholderKind.Marker)
        {
            if (!inTable)
            {
                messages.Add(Error($"„{token.Raw}“ muss in einer Tabellenzeile stehen.", location));
                return;
            }
            if (!registry.TryGetSource(token.Qualifier, out var source))
            {
                string known = string.Join(", ", registry.SourceNames);
                messages.Add(Error($"Unbekannte Wiederholungsquelle „{token.Qualifier ?? "(leer)"}“. Möglich: {known}.", location));
                return;
            }
            CheckArguments(token, source.AllowedArguments, location, messages);
            return;
        }

        if (token.Qualifier != null)
            messages.Add(Warning($"„{token.Raw}“: Der Teil nach dem Doppelpunkt wird nur bei ${{repeat:…}} verwendet.", location));

        CheckArguments(token, def.AllowedArguments, location, messages);

        foreach (string dim in new[] { "width", "height" })
            if (token.Arguments.Has(dim) && (!token.Arguments.TryGetFloat(dim, out float v) || v < 0))
                messages.Add(Warning($"„{token.Raw}“: {dim} muss eine Zahl ≥ 0 sein (in mm). Es wird der Standardwert verwendet.", location));

        string? format = token.Arguments.Get("format");
        if (format != null && def.ValueKind == PlaceholderValueKind.DateTime && !IsValidDateFormat(format))
            messages.Add(Warning($"„{token.Raw}“: Ungültiges Datumsformat „{format}“.", location));

        if ((def.RequiredScope & ~available) != ScopeKind.None)
        {
            ScopeKind missingScope = def.RequiredScope & ~available;
            string need = missingScope switch
            {
                var s when s.HasFlag(ScopeKind.Priority) => "${repeat:priorities}",
                var s when s.HasFlag(ScopeKind.Photo) => "${repeat:photos}",
                var s when s.HasFlag(ScopeKind.Pin) => "${repeat:pins} oder ${repeat:photos}",
                var s when s.HasFlag(ScopeKind.Plan) => "${repeat:plans}, ${repeat:pins} oder ${repeat:photos}",
                _ => "einer passenden Wiederholung"
            };
            messages.Add(Warning($"„{token.Raw}“ hat hier keine Datengrundlage. Er funktioniert nur in einer Tabellenzeile mit {need}.", location));
        }

        if (def.Kind == PlaceholderKind.Block && inTable)
            messages.Add(Warning($"„{token.Raw}“ fügt ganze Absätze und Seitenumbrüche ein und gehört deshalb nicht in eine Tabelle.", location));
    }

    private static void CheckArguments(PlaceholderToken token, IReadOnlyList<string> allowed, string location, List<ReportMessage> messages)
    {
        foreach (string name in token.Arguments.Names)
            if (!allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
                messages.Add(Warning(
                    $"„{token.Raw}“: Unbekannter Parameter „{name}“." + (allowed.Count > 0 ? $" Erlaubt: {string.Join(", ", allowed)}." : " Dieser Platzhalter hat keine Parameter."),
                    location));
    }

    private static bool IsValidDateFormat(string format)
    {
        try { _ = new DateTime(2000, 1, 1).ToString(format, SwissCulture); return true; }
        catch (FormatException) { return false; }
    }

    /// <summary>Welche Datenebenen sind an dieser Stelle verfügbar (abhängig von umgebenden Wiederholungszeilen)?</summary>
    private ScopeKind ProvidedScope(W.Paragraph paragraph)
    {
        var scope = ScopeKind.Project;
        foreach (var row in paragraph.Ancestors<W.TableRow>())
        {
            foreach (var marker in parser.FindRepeatMarkers(row))
            {
                if (registry.TryGetSource(marker.Qualifier, out var source))
                    scope |= source.Provides;
            }
        }
        return scope;
    }

    private static string Describe(OpenXmlElement root, W.Paragraph paragraph, string text, Dictionary<OpenXmlElement, int> tableNumbers)
    {
        string prefix = root switch { W.Header => "Kopfzeile, ", W.Footer => "Fusszeile, ", _ => string.Empty };

        var row = paragraph.Ancestors<W.TableRow>().LastOrDefault();
        if (row != null)
            return prefix + DescribeRow(root, row, tableNumbers);

        string snippet = text.Length > 40 ? text[..40] + "…" : text;
        return $"{prefix}Absatz „{snippet}“";
    }

    private static string DescribeRow(OpenXmlElement root, W.TableRow row, Dictionary<OpenXmlElement, int> tableNumbers)
    {
        string prefix = root switch { W.Header => "Kopfzeile, ", W.Footer => "Fusszeile, ", _ => string.Empty };
        if (row.Parent is W.Table table && tableNumbers.TryGetValue(table, out int t))
        {
            int r = table.Elements<W.TableRow>().ToList().IndexOf(row) + 1;
            return $"{prefix}Tabelle {t}, Zeile {r}";
        }
        return prefix + "Tabelle";
    }

    private string? Suggest(string name)
    {
        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (string candidate in registry.PlaceholderNames)
        {
            int d = Levenshtein(name.ToLowerInvariant(), candidate.ToLowerInvariant());
            if (d < bestDistance) { bestDistance = d; best = candidate; }
        }
        return bestDistance <= 3 ? best : null;
    }

    private static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0) { count++; index += value.Length; }
        return count;
    }

    private static ReportMessage Warning(string text, string? location) => new(ReportMessageSeverity.Warning, text, location);
    private static ReportMessage Error(string text, string? location) => new(ReportMessageSeverity.Error, text, location);
}
