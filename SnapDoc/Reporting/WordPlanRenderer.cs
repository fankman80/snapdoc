#nullable enable
using DocumentFormat.OpenXml;
using Microsoft.Maui.Graphics;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

/// <summary>
/// Planverzeichnis (${plan_indexes}) und Planbilder mit Pins und nummerierten Beschriftungen (${plan_images}).
/// </summary>
public sealed class WordPlanRenderer(ExportSession session, WordImageRenderer images)
{
    private ExportOptions Options => session.Options;

    /// <summary>"- Plan (Beschreibung)" pro exportiertem Plan, ohne Web-Karten.</summary>
    public PlaceholderResult PlanIndexes(PlaceholderCall call)
    {
        if (!Options.ExportPlans)
            return PlaceholderResult.Empty;

        var lines = call.Data.Plans
            .Where(p => !p.IsWebMap)
            .Select(p => string.IsNullOrWhiteSpace(p.Description) ? $"- {p.Name}" : $"- {p.Name} ({p.Description})");

        return PlaceholderResult.FromText(string.Join("\n", lines));
    }

    /// <summary>
    /// Pro Plan: Planname, Planbild mit Pins, Seitenumbruch (ausser nach dem letzten).
    /// Die Absätze werden nach dem Platzhalter-Absatz eingefügt.
    /// </summary>
    public PlaceholderResult PlanImages(PlaceholderCall call)
    {
        if (!Options.ExportPlans)
            return PlaceholderResult.Empty;

        var anchorParagraph = call.Paragraph;
        SizeF exportSize = WordImageRenderer.GetSize(call, 250, 160);

        var plans = call.Data.Plans.Where(p => !p.IsWebMap).ToList();
        var blocks = new List<OpenXmlElement>();

        for (int index = 0; index < plans.Count; index++)
        {
            var plan = plans[index];

            // Planname mit Formatierung des Platzhalters
            var nameRun = call.NewRun();
            nameRun.Append(new W.Text(plan.Name));
            var namePara = new W.Paragraph(nameRun);
            if (anchorParagraph.ParagraphProperties != null)
                namePara.PrependChild((W.ParagraphProperties)anchorParagraph.ParagraphProperties.CloneNode(true));
            blocks.Add(namePara);

            // Planbild + Pins + Beschriftungen in einem Run
            var imageRun = new W.Run();
            string planPath = session.Assets.ResolvePlanImage(plan);

            if (!session.Assets.Exists(planPath))
            {
                session.Warn($"Planbild nicht gefunden: {plan.File}", call.Token.Raw);
            }
            else
            {
                SizeF scaledPlan = ReportGeometry.ScaleToFit(plan.ImageSize, exportSize);
                imageRun.Append(images.CreateDrawing(anchorParagraph, planPath, scaledPlan, new PointF(0, 0), 0,
                    anchored: true, crop: null, grayscale: plan.IsGrayscale));

                foreach (var pin in plan.Pins)
                    AppendPin(imageRun, anchorParagraph, plan, pin, scaledPlan);
            }

            blocks.Add(new W.Paragraph(imageRun));

            if (index < plans.Count - 1)
                blocks.Add(new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })));
        }

        return PlaceholderResult.FromBlocks(blocks);
    }

    private void AppendPin(W.Run imageRun, OpenXmlElement host, ExportPlan plan, ExportPin pin, SizeF scaledPlan)
    {
        SizeF scaledPinSize;
        if (pin.IsLockAutoScale || pin.IsCustomPin)
        {
            scaledPinSize = new SizeF(
                pin.Size.Width * scaledPlan.Width / plan.ImageSize.Width * pin.PinScale,
                pin.Size.Height * scaledPlan.Height / plan.ImageSize.Height * pin.PinScale);
        }
        else
        {
            scaledPinSize = ReportGeometry.ScaleToFit(pin.Size, new SizeF(Options.PinExportSize, Options.PinExportSize));
        }

        PointF posOnPlan = ReportGeometry.PivotRecalc(pin.Position, pin.Rotation, pin.Anchor, scaledPinSize, scaledPlan);

        string iconPath = session.Assets.ResolvePinIcon(pin);
        if (session.Assets.Exists(iconPath))
            imageRun.Append(images.CreateDrawing(host, iconPath, scaledPinSize, posOnPlan, pin.Rotation, anchored: true));

        imageRun.Append(PinLabelFactory.CreatePlanLabel(
            Options.PinLabelPrefix,
            pin.Number,
            session.StoreItemId,
            session.NextShapeId(),
            new PointF(
                posOnPlan.X + scaledPinSize.Width + 1,
                posOnPlan.Y - (float)(Options.PinLabelFontSize / 2)),
            Options.PinLabelFontSize,
            pin.ColorHex));
    }
}
