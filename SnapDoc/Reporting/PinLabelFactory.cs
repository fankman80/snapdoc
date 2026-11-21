#nullable enable
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;
using V = DocumentFormat.OpenXml.Vml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SnapDoc.Reporting;

/// <summary>
/// Schreibt die Pin-Nummern in einen CustomXml-Part. Content Controls (Tabelle und Plan-Beschriftungen)
/// sind daran gebunden – wer in Word eine Nummer ändert, ändert sie überall.
/// </summary>
public static class PinNumberStore
{
    /// <returns>Die StoreItemId, an die alle Data-Bindings gehängt werden.</returns>
    public static string Write(MainDocumentPart main, ReportDataContext data)
    {
        // Nur einen eigenen "positions"-Part wiederverwenden, fremde CustomXml-Parts der Vorlage nie überschreiben.
        CustomXmlPart? part = null;
        foreach (var candidate in main.CustomXmlParts)
        {
            try
            {
                using var s = candidate.GetStream();
                if (XDocument.Load(s).Root?.Name.LocalName == "positions")
                {
                    part = candidate;
                    break;
                }
            }
            catch (System.Xml.XmlException) { /* kein XML, ignorieren */ }
        }
        part ??= main.AddCustomXmlPart(CustomXmlPartType.CustomXml);

        var xml = new XDocument(new XElement("positions",
            data.AllPins.Select(p => new XElement("pos", new XAttribute("id", p.Number), p.Number))));

        using (var ms = new MemoryStream())
        {
            xml.Save(ms);
            ms.Position = 0;
            part.FeedData(ms);
        }

        var props = part.CustomXmlPropertiesPart ?? part.AddNewPart<CustomXmlPropertiesPart>();
        var item = props.DataStoreItem ??= new DataStoreItem();
        if (string.IsNullOrEmpty(item.ItemId?.Value))
            item.ItemId = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";

        return item.ItemId!.Value!;
    }

    public static string XPath(int number) => $"/positions/pos[@id='{number}']";
}

/// <summary>Erzeugt die an die Pin-Nummer gebundenen Word-Elemente.</summary>
public static class PinLabelFactory
{
    /// <summary>Inline-Content-Control, z. B. für ${pin_nr} in der Tabelle.</summary>
    public static W.SdtRun CreateBoundNumber(string storeItemId, int number, W.RunProperties? runProperties)
    {
        string tag = $"Pos_{number}";

        var inner = new W.Run();
        if (runProperties != null)
            inner.RunProperties = (W.RunProperties)runProperties.CloneNode(true);
        inner.Append(new W.Text(number.ToString()) { Space = SpaceProcessingModeValues.Preserve });

        return new W.SdtRun(
            new W.SdtProperties(
                new W.SdtAlias { Val = tag },
                new W.Tag { Val = tag },
                new W.DataBinding { StoreItemId = storeItemId, XPath = PinNumberStore.XPath(number), PrefixMappings = "" }),
            new W.SdtContentRun(inner));
    }

    /// <summary>Beschriftungsfeld (VML-Textbox) neben dem Pin auf dem Planbild.</summary>
    public static W.Picture CreatePlanLabel(
        string prefix,
        int number,
        string storeItemId,
        int shapeId,
        PointF positionMm,
        double fontSizePt,
        string colorHex)
    {
        double xPt = positionMm.X * 2.83465;
        double yPt = positionMm.Y * 2.83465;
        string xText = xPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        string yText = yPt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        string color = colorHex.TrimStart('#');

        W.RunProperties NewRunProps() => new(
            new W.RunFonts { Ascii = "Arial", HighAnsi = "Arial", EastAsia = "Arial" },
            new W.FontSize { Val = (fontSizePt * 2).ToString(System.Globalization.CultureInfo.InvariantCulture) },
            new W.Color { Val = color });

        var shape = new V.Shape
        {
            Id = $"PinLabel_{shapeId}",
            Style = $"position:absolute;margin-left:{xText}pt;margin-top:{yText}pt;" +
                    "mso-position-horizontal:absolute;mso-position-horizontal-relative:margin;" +
                    "mso-position-vertical:absolute;mso-position-vertical-relative:paragraph;" +
                    "mso-fit-shape-to-text:t;mso-wrap-style:none;",
            Stroked = TrueFalseValue.FromBoolean(true),
            Filled = TrueFalseValue.FromBoolean(true)
        };
        shape.Append(
            new V.Fill { Color = "#FFFFFF", On = TrueFalseValue.FromBoolean(true) },
            new V.Stroke { Color = colorHex, Weight = "1pt", On = TrueFalseValue.FromBoolean(true) });

        var paragraphProperties = new W.ParagraphProperties();
        paragraphProperties.Append(new W.Shading { Fill = "FFFFFF", Val = W.ShadingPatternValues.Clear });
        paragraphProperties.Append(new W.ParagraphBorders
        {
            TopBorder = new W.TopBorder { Val = W.BorderValues.Single, Color = color, Size = 6U },
            BottomBorder = new W.BottomBorder { Val = W.BorderValues.Single, Color = color, Size = 6U },
            LeftBorder = new W.LeftBorder { Val = W.BorderValues.Single, Color = color, Size = 6U },
            RightBorder = new W.RightBorder { Val = W.BorderValues.Single, Color = color, Size = 6U }
        });

        var paragraph = new W.Paragraph();
        paragraph.Append(paragraphProperties);

        if (!string.IsNullOrEmpty(prefix))
            paragraph.Append(new W.Run(NewRunProps(), new W.Text(prefix) { Space = SpaceProcessingModeValues.Preserve }));

        string tag = $"Pos_{number}";
        paragraph.Append(new W.SdtRun(
            new W.SdtProperties(
                new W.SdtAlias { Val = tag },
                new W.Tag { Val = tag },
                new W.DataBinding { StoreItemId = storeItemId, XPath = PinNumberStore.XPath(number), PrefixMappings = "" },
                NewRunProps()),
            new W.SdtContentRun(
                new W.Run(NewRunProps(), new W.Text(number.ToString()) { Space = SpaceProcessingModeValues.Preserve }))));

        var content = new W.TextBoxContent();
        content.Append(paragraph);

        var textBox = new V.TextBox
        {
            Style = "mso-fit-shape-to-text:t;mso-wrap-style:none;",
            Inset = "0pt,0pt,0pt,0pt"
        };
        textBox.Append(content);
        shape.Append(textBox);

        var picture = new W.Picture();
        picture.Append(shape);
        return picture;
    }
}
