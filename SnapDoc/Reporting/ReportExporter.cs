#nullable enable
using SnapDoc.Models;
using SnapDoc.Services;

namespace SnapDoc.Reporting;

/// <summary>
/// Steuert nur den Ablauf: Daten → Assets → Vorlage öffnen → validieren → rendern → speichern.
/// Die eigentliche Arbeit machen TemplateParser, TemplateValidator, PlaceholderRegistry und die Word*Renderer.
/// </summary>
public sealed class ReportExporter
{
    /// <summary>Exportiert mit den aktuellen Projektdaten und Einstellungen.</summary>
    public static Task<ReportExportResult> ExportAsync(string templatePath, string outputPath, CancellationToken ct = default) =>
        ReportExporter.ExportAsync(templatePath, outputPath, ReportDataBuilder.Build(GlobalJson.Data, SettingsService.Instance), ct);

    public static async Task<ReportExportResult> ExportAsync(
        string templatePath,
        string outputPath,
        ReportDataContext data,
        CancellationToken ct = default)
    {
        var messages = new List<ReportMessage>();
        ExportAssetCache? assets = null;
        TemplateDocument? template = null;

        try
        {
            assets = new ExportAssetCache(data.Options);
            await assets.PrepareAsync(data, ct);

            template = TemplateDocument.Open(templatePath);

            var session = new ExportSession(template, data, assets, templatePath, outputPath);
            var images = new WordImageRenderer(session);
            var plans = new WordPlanRenderer(session, images);
            var registry = DefaultPlaceholders.Create(session, images, plans);
            var parser = new TemplateParser();

            // Vorlage prüfen, bevor etwas verändert wird
            var validation = new TemplateValidator(registry, parser).Validate(template);
            messages.AddRange(validation.Messages);
            if (validation.HasErrors)
                return new ReportExportResult(false, null, messages);

            ct.ThrowIfCancellationRequested();
            session.InitializePinStore();

            var textRenderer = new WordTextRenderer(session, registry, parser);
            var tableRenderer = new WordTableRenderer(session, registry, parser, textRenderer);
            var scope = RenderScope.ForProject(data);

            // Body, Kopf- und Fusszeilen: zuerst Tabellenzeilen vervielfältigen, dann restliche Platzhalter ersetzen
            foreach (var root in template.Roots.ToList())
            {
                tableRenderer.RenderTables(root, scope, ct);
                textRenderer.RenderRoot(root, scope, ct);
            }

            messages.AddRange(session.Messages);

            template.SaveTo(outputPath);
            return new ReportExportResult(true, outputPath, messages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            messages.Add(new ReportMessage(ReportMessageSeverity.Error, $"Export fehlgeschlagen: {ex.Message}"));
            return new ReportExportResult(false, null, messages, ex);
        }
        finally
        {
            template?.Dispose();
            assets?.Dispose();
        }
    }
}
