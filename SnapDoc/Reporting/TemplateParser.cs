#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

public sealed class ArgumentSet(IReadOnlyDictionary<string, string> values)
{
    public static readonly ArgumentSet Empty = new(new Dictionary<string, string>());
    private readonly IReadOnlyDictionary<string, string> _values = values;
    public IEnumerable<string> Names => _values.Keys;
    public bool Has(string name) => _values.ContainsKey(name);
    public string? Get(string name) => _values.TryGetValue(name, out string? value) ? value : null;

    public bool TryGetFloat(string name, out float value)
    {
        value = 0;
        return _values.TryGetValue(name, out string? text)
               && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
    
    public float GetFloat(string name, float fallback) => TryGetFloat(name, out float value) ? value : fallback;
}

/// <summary>
/// Ein im Text gefundener Platzhalter.
/// Start und Length beziehen sich auf den zusammengesetzten
/// Absatztext und sind damit unabhängig davon, auf wie viele
/// Word-Runs der Platzhalter verteilt ist.
/// </summary>
public sealed record PlaceholderToken(string Name, string? Qualifier, ArgumentSet Arguments, int Start, int Length, string Raw)
{
    public int End => Start + Length;
}

/// <summary>
/// Hilfsfunktionen, um den Text eines Absatzes
/// über alle Runs hinweg zu betrachten.
/// </summary>
internal static class ParagraphText
{
    internal sealed record Segment(W.Text Text, int Start, int End);

    /// <summary>
    /// Alle Text-Elemente des Absatzes.
    /// Text aus verschachtelten Absätzen, beispielsweise
    /// innerhalb von Textfeldern, wird nicht berücksichtigt.
    /// </summary>
    public static List<Segment> GetSegments(W.Paragraph paragraph)
    {
        var segments = new List<Segment>();

        int position = 0;

        foreach (W.Text text in paragraph.Descendants<W.Text>())
        {
            W.Paragraph? ownerParagraph = text.Ancestors<W.Paragraph>().FirstOrDefault();

            if (!ReferenceEquals(ownerParagraph, paragraph))
                continue;

            string value = text.Text ?? string.Empty;
            segments.Add(new Segment(text, position, position + value.Length));
            position += value.Length;
        }
        return segments;
    }

    public static string GetText(W.Paragraph paragraph)
    {
        return string.Concat(GetSegments(paragraph).Select(segment => segment.Text.Text));
    }
}

/// <summary>
/// Erkennt Platzhalter innerhalb eines Textes.
///
/// Unterstützte Syntax:
///
/// ${pin_name}
/// ${pin_location|fallback=Keine Angabe}
/// ${pin_fotoList|width=40|height=80}
/// ${pin_captureDate|format=dd.MM.yyyy}
/// ${creation_date|format=dddd, dd. MMMM yyyy|culture=de-CH}
/// ${repeat:pins}
/// ${repeat:pins|priority=Wichtig}
/// ${repeat:photos}
/// ${repeat:plans}
///
/// Die alte Syntax mit Schrägstrichen wird nicht unterstützt.
/// </summary>
public sealed partial class TemplateParser
{
    /// <summary>
    /// Erkennt:
    ///
    /// ${name}
    /// ${name|key=value}
    /// ${repeat:source}
    /// ${repeat:source|key=value}
    ///
    /// Platzhalternamen und Qualifier bestehen bewusst nur aus
    /// Buchstaben, Zahlen und Unterstrichen.
    /// </summary>
    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::(?<qualifier>[A-Za-z_][A-Za-z0-9_]*))?(?<args>\|[^}]*)?\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>
    /// Findet alle gültigen Platzhalter im angegebenen Text.
    /// </summary>
    public IReadOnlyList<PlaceholderToken> FindTokens(string text)
    {
        var tokens = new List<PlaceholderToken>();

        if (string.IsNullOrEmpty(text))
            return tokens;

        foreach (Match match in PlaceholderRegex().Matches(text))
        {
            string name = match.Groups["name"].Value;
            string? qualifier = match.Groups["qualifier"].Success ? match.Groups["qualifier"].Value : null;
            string rawArguments = match.Groups["args"].Success ? match.Groups["args"].Value : string.Empty;

            ArgumentSet arguments = ParseArguments(rawArguments);

            tokens.Add(new PlaceholderToken(name, qualifier, arguments, match.Index, match.Length, match.Value));
        }

        return tokens;
    }

    /// <summary>
    /// Sucht alle ${repeat:...}-Marker innerhalb
    /// der direkten Zellen einer Tabellenzeile.
    ///
    /// Verschachtelte Tabellen werden nicht versehentlich
    /// der äusseren Tabellenzeile zugerechnet.
    /// </summary>
    public IReadOnlyList<PlaceholderToken> FindRepeatMarkers(W.TableRow row)
    {
        var markers = new List<PlaceholderToken>();

        foreach (W.TableCell cell in row.Elements<W.TableCell>())
        {
            foreach (W.Paragraph paragraph in cell.Elements<W.Paragraph>())
            {
                string text = ParagraphText.GetText(paragraph);

                if (!text.Contains("${repeat", StringComparison.Ordinal))
                    continue;

                IEnumerable<PlaceholderToken> repeatTokens = FindTokens(text).Where(token =>
                            token.Name.Equals("repeat", StringComparison.OrdinalIgnoreCase));

                markers.AddRange(repeatTokens);
            }
        }

        return markers;
    }

    /// <summary>
    /// Liest benannte Parameter:
    ///
    /// |width=40|height=80
    ///
    /// wird zu:
    ///
    /// width  = 40
    /// height = 80
    ///
    /// Leerzeichen im Wert bleiben erhalten,
    /// damit beispielsweise
    ///
    /// |prefix=Standort:
    ///
    /// korrekt funktioniert.
    /// </summary>
    private static ArgumentSet ParseArguments(string rawArguments)
    {
        if (string.IsNullOrEmpty(rawArguments))
            return ArgumentSet.Empty;

        var values = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase);

        // Das erste Zeichen ist immer '|'.
        ReadOnlySpan<char> argumentText = rawArguments.AsSpan(1);

        foreach (string part in argumentText.ToString().Split('|'))
        {
            if (part.Length == 0)
                continue;

            int separatorIndex = part.IndexOf('=');

            string name;
            string value;

            if (separatorIndex < 0)
            {
                name = part.Trim();
                value = string.Empty;
            }
            else
            {
                name = part[..separatorIndex].Trim();
                value = part[(separatorIndex + 1)..];
            }

            if (name.Length == 0)
                continue;

            values[name] = value;
        }

        return new ArgumentSet(values);
    }
}
