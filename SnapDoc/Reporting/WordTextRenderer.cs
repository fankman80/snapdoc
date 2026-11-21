#nullable enable
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

/// <summary>
/// Ersetzt Platzhalter an ihrer exakten Position im Absatz – egal ob mitten im Satz,
/// ob auf mehrere Word-Runs verteilt oder ob das Ergebnis Text, ein Bild oder ein Content Control ist.
/// Text und Formatierung vor und nach dem Platzhalter bleiben unverändert.
/// </summary>
public sealed class WordTextRenderer(ExportSession session, PlaceholderRegistry registry, TemplateParser parser)
{
    public void RenderRoot(OpenXmlElement root, RenderScope scope, CancellationToken ct = default)
    {
        foreach (var paragraph in root.Descendants<W.Paragraph>().ToList())
        {
            ct.ThrowIfCancellationRequested();
            RenderParagraph(paragraph, scope);
        }
    }

    public void RenderParagraph(
        W.Paragraph paragraph,
        RenderScope scope)
    {
        if (paragraph.Parent == null)
            return;

        string originalText = ParagraphText.GetText(paragraph);

        if (!originalText.Contains("${", StringComparison.Ordinal))
            return;

        IReadOnlyList<PlaceholderToken> tokens =
            parser.FindTokens(originalText);

        if (tokens.Count == 0)
            return;

        // Jeder Run enthält danach höchstens ein Inhaltselement.
        SplitMultiContentRuns(paragraph);

        var blocks = new List<OpenXmlElement>();
        string? cellFill = null;

        // Von rechts nach links. Änderungen rechts verändern die Positionen
        // der links liegenden Platzhalter nicht.
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (paragraph.Parent == null)
                break;

            PlaceholderToken token = tokens[i];

            // Wichtig: Nach jedem Ersatz den aktuellen OpenXML-Baum neu lesen.
            IReadOnlyList<ParagraphText.Segment> currentSegments =
                ParagraphText.GetSegments(paragraph);

            ParagraphText.Segment? hostSegment =
                currentSegments.FirstOrDefault(segment =>
                    segment.Start <= token.Start
                    && segment.End > token.Start);

            W.RunProperties? runProperties = null;

            if (hostSegment?.Text.Parent is W.Run hostRun)
                runProperties = hostRun.RunProperties;

            PlaceholderResult? result = Resolve(
                token,
                scope,
                paragraph,
                runProperties,
                originalText);

            if (result == null)
                continue;

            List<OpenXmlElement> elements = BuildElements(
                result,
                token,
                runProperties);

            ReplaceRange(
                currentSegments,
                token.Start,
                token.End,
                elements);

            if (result.Blocks.Count > 0)
                blocks.InsertRange(0, result.Blocks);

            cellFill ??= result.CellFillHex;
        }

        if (cellFill != null && paragraph.Parent != null)
            ApplyCellFill(paragraph, cellFill);

        if (blocks.Count > 0 && paragraph.Parent != null)
            InsertBlocksAfter(paragraph, blocks);
    }

    // ------------------------------------------------------------------ Auflösen

    private PlaceholderResult? Resolve(PlaceholderToken token, RenderScope scope, W.Paragraph paragraph, W.RunProperties? runProperties, string paragraphText)
    {
        string location = $"Absatz „{(paragraphText.Length > 40 ? paragraphText[..40] + "…" : paragraphText)}“";

        if (!registry.TryGet(token.Name, out var definition))
        {
            session.Warn(
                $"Unbekannter Platzhalter „{token.Raw}“ – bleibt unverändert.",
                location);

            return null;
        }

        if (definition.Kind == PlaceholderKind.Marker)
            return PlaceholderResult.Empty;

        if (!scope.Satisfies(definition.RequiredScope))
        {
            session.Warn(
                $"„{token.Raw}“ hat an dieser Stelle keine Datengrundlage " +
                "(ausserhalb einer passenden Wiederholungszeile) – " +
                "bleibt unverändert.",
                location);

            return null;
        }

        try
        {
            return definition.Resolve(new PlaceholderCall(token, scope, paragraph, runProperties, session));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            session.Error($"„{token.Raw}“ konnte nicht erzeugt werden: {ex.Message}", location);
            return null;
        }
    }

    private static List<OpenXmlElement> BuildElements(PlaceholderResult result, PlaceholderToken token, W.RunProperties? runProperties)
    {
        if (result.Elements != null)
            return [.. result.Elements];

        string? text = result.Text;

        if (string.IsNullOrEmpty(text))
        {
            text = token.Arguments.Get("fallback");
        }
        else
        {
            text =
                token.Arguments.Get("prefix")
                + text
                + token.Arguments.Get("suffix");
        }

        if (string.IsNullOrEmpty(text))
            return [];

        string[] lines = text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        var run = new W.Run();

        if (runProperties != null)
        {
            run.RunProperties =
                (W.RunProperties)runProperties.CloneNode(true);
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                run.Append(new W.Break());

            run.Append(
                new W.Text(lines[i])
                {
                    Space = SpaceProcessingModeValues.Preserve
                });
        }

        return
        [
            run
        ];
    }

    // ------------------------------------------------------------------ Textbereich ersetzen

    /// <summary>
    /// Ersetzt die Zeichen [start, end) des Absatztextes durch <paramref name="replacement"/>.
    /// Voraussetzung: jeder Run enthält höchstens ein Text-Element (siehe SplitMultiContentRuns).
    /// </summary>
    private static void ReplaceRange(
        IReadOnlyList<ParagraphText.Segment> segments,
        int start,
        int end,
        IReadOnlyList<OpenXmlElement> replacement)
    {
        List<ParagraphText.Segment> hit = [.. segments
            .Where(segment =>
                segment.Start < end
                && segment.End > start)];

        if (hit.Count == 0)
            return;

        ParagraphText.Segment first = hit[0];
        ParagraphText.Segment last = hit[^1];

        if (first.Text.Parent is not W.Run firstRun
            || firstRun.Parent == null)
        {
            return;
        }

        if (last.Text.Parent is not W.Run lastRun
            || lastRun.Parent == null)
        {
            return;
        }

        int prefixLength = start - first.Start;
        int suffixStart = end - last.Start;

        if (prefixLength < 0
            || prefixLength > first.Text.Text.Length
            || suffixStart < 0
            || suffixStart > last.Text.Text.Length)
        {
            return;
        }

        string prefix = first.Text.Text[..prefixLength];
        string suffix = last.Text.Text[suffixStart..];

        W.Run? suffixRun = null;

        if (ReferenceEquals(first, last))
        {
            if (suffix.Length > 0)
            {
                suffixRun = (W.Run)firstRun.CloneNode(true);

                W.Text? suffixText =
                    suffixRun.Elements<W.Text>().FirstOrDefault();

                if (suffixText != null)
                    SetText(suffixText, suffix);
            }
        }
        else
        {
            // Alle vollständig vom Platzhalter belegten Zwischen-Runs entfernen.
            foreach (ParagraphText.Segment middle in
                     hit.Skip(1).Take(hit.Count - 2))
            {
                if (middle.Text.Parent is W.Run middleRun
                    && middleRun.Parent != null)
                {
                    middleRun.Remove();
                }
            }

            if (suffix.Length > 0)
            {
                if (lastRun.Parent != null)
                    SetText(last.Text, suffix);
            }
            else if (lastRun.Parent != null)
            {
                lastRun.Remove();
            }
        }

        // firstRun bleibt bis zum Ende als Einfügeanker im Dokument.
        OpenXmlElement anchor = firstRun;

        foreach (OpenXmlElement element in replacement)
        {
            if (anchor.Parent == null)
                break;

            OpenXmlElement newElement =
                element.Parent == null
                    ? element
                    : element.CloneNode(true);

            anchor.InsertAfterSelf(newElement);
            anchor = newElement;
        }

        if (suffixRun != null && anchor.Parent != null)
        {
            OpenXmlElement newSuffixRun =
                suffixRun.Parent == null
                    ? suffixRun
                    : suffixRun.CloneNode(true);

            anchor.InsertAfterSelf(newSuffixRun);
        }

        // Den ursprünglichen Run erst entfernen, nachdem alle neuen Elemente
        // über diesen Run als Anker eingefügt wurden.
        if (prefix.Length > 0)
        {
            if (firstRun.Parent != null)
                SetText(first.Text, prefix);
        }
        else if (firstRun.Parent != null)
        {
            firstRun.Remove();
        }
    }

    private static void SetText(W.Text text, string value)
    {
        text.Text = value;
        text.Space = SpaceProcessingModeValues.Preserve;
    }

    /// <summary>Zerlegt Runs mit mehreren Inhaltselementen (Text + Tab + Text …) in einzelne Runs mit gleicher Formatierung.</summary>
    private static void SplitMultiContentRuns(W.Paragraph paragraph)
    {
        foreach (var run in paragraph.Descendants<W.Run>()
                     .Where(r => ReferenceEquals(r.Ancestors<W.Paragraph>().FirstOrDefault(), paragraph))
                     .ToList())
        {
            var content = run.ChildElements.Where(c => c is not W.RunProperties).ToList();
            if (content.Count <= 1 || !content.Any(c => c is W.Text))
                continue;

            OpenXmlElement anchor = run;
            foreach (var child in content.Skip(1))
            {
                var clonedChild = child.CloneNode(true);
                child.Remove();
                var newRun = new W.Run();

                if (run.RunProperties != null)
                    newRun.RunProperties = (W.RunProperties)run.RunProperties.CloneNode(true);

                newRun.Append(clonedChild);
                anchor.InsertAfterSelf(newRun);
                anchor = newRun;
            }
        }
    }

    private static void ApplyCellFill(W.Paragraph paragraph, string fillHex)
    {
        var cell = paragraph.Ancestors<W.TableCell>().FirstOrDefault();
        if (cell == null) return;

        cell.TableCellProperties ??= new W.TableCellProperties();
        var shading = cell.TableCellProperties.Shading ??= new W.Shading();
        shading.Fill = fillHex.TrimStart('#');
        shading.Val = W.ShadingPatternValues.Clear;
    }

    private static void InsertBlocksAfter(W.Paragraph paragraph, IReadOnlyList<OpenXmlElement> blocks)
    {
        if (paragraph.Parent == null)
            return;

        OpenXmlElement anchor = paragraph;

        foreach (OpenXmlElement block in blocks)
        {
            if (anchor.Parent == null)
                break;

            OpenXmlElement newBlock =
                block.Parent == null
                    ? block
                    : block.CloneNode(true);

            anchor.InsertAfterSelf(newBlock);
            anchor = newBlock;
        }

        // In einer Tabellenzelle muss mindestens ein Absatz bestehen bleiben.
        bool canRemoveParagraph =
            paragraph.Parent != null
            && paragraph.Parent is not W.TableCell
            && string.IsNullOrWhiteSpace(paragraph.InnerText)
            && !paragraph.Descendants<W.Drawing>().Any();

        if (canRemoveParagraph)
            paragraph.Remove();
    }
}
