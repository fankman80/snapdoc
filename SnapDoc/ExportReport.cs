#nullable disable
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using SnapDoc.Models;
using SnapDoc.Services;
using System.Text;
using System.Text.RegularExpressions;
using DDW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DW = DocumentFormat.OpenXml.Wordprocessing;
using OXML = DocumentFormat.OpenXml;
using Path = System.IO.Path;

namespace SnapDoc;

public partial class ExportReport
{
    [GeneratedRegex(@"\$\{plan_images/(\d+)/(\d+)\}")]
    private static partial Regex PlanImagesRegex();

    private static readonly Dictionary<string, string> imageRelationshipIds = [];

    private static string storeItemId;

    public static async Task DocX(string templateDoc, string savePath)
    {
        imageRelationshipIds.Clear();

        Dictionary<string, string> placeholders_single = new()
        {
            {"${client_name}", GlobalJson.Data.Client_name},
            {"${object_address}", GlobalJson.Data.Object_address.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "{linebreak}")},
            {"${working_title}", GlobalJson.Data.Working_title},
            {"${project_nr}", GlobalJson.Data.Project_nr},
            {"${object_name}", GlobalJson.Data.Object_name},
            {"${creation_date}", GlobalJson.Data.Creation_date.Date.ToString("D")},
            {"${project_manager}", GlobalJson.Data.Project_manager},
        };
        Dictionary<string, string> placeholders_lists = new()
        {
            {"${plan_indexes}", "${plan_indexes}"},         //bereinige splitted runs
            {"${plan_images/", "${plan_images/"},           //bereinige splitted runs
            {"${title_image}", "${title_image}"},           //bereinige splitted runs
        };
        Dictionary<string, string> placeholders_table = new()
        {
            {"${pin_nr}", "${pin_nr}"},                     //bereinige splitted runs
            {"${pin_planName}", "${pin_planName}"},         //bereinige splitted runs
            {"${pin_posImage}", "${pin_posImage}"},         //bereinige splitted runs
            {"${pin_fotoList}", "${pin_fotoList}"},         //bereinige splitted runs
            {"${pin_name}", "${pin_name}"},                 //bereinige splitted runs
            {"${pin_desc}", "${pin_desc}"},                 //bereinige splitted runs
            {"${pin_location}", "${pin_location}"},         //bereinige splitted runs
            {"${pin_priority}", "${pin_priority}"},         //bereinige splitted runs
            {"${pin_geolocWGS84}", "${pin_geolocWGS84}"},   //bereinige splitted runs
            {"${pin_geolocCH1903}", "${pin_geolocCH1903}"}, //bereinige splitted runs
        };

        // create a list with all icons, each icon only one times
        List<string> uniquePinIcons = GetUniquePinIcons(GlobalJson.Data);
        foreach (string icon in uniquePinIcons)
            if (icon.Contains("custompin_", StringComparison.OrdinalIgnoreCase)) //check if icon is a custompin
                CopyImageToDirectory(Settings.CacheDirectory, Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath), icon);
            else if (icon.Contains("customicons", StringComparison.OrdinalIgnoreCase)) //check if icon is a customicon
                CopyImageToDirectory(Path.Combine(Settings.CacheDirectory, "customicons"), "customicons", Path.GetFileName(icon));
            else
                await MauiResourceLoader.CopyAppPackageFileAsync(Settings.CacheDirectory, icon);

        using MemoryStream memoryStream = new();
        using (Stream fileStream = new FileStream(templateDoc, FileMode.Open, FileAccess.Read))
        {
            fileStream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0; // Sicherstellen, dass der Stream auf den Anfang gesetzt ist

        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(memoryStream, true))
        {
            // Platzhalter durch die entsprechenden Werte ersetzen
            foreach (KeyValuePair<string, string> placeholder in placeholders_single)
                if (!string.IsNullOrEmpty(placeholder.Value))
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (KeyValuePair<string, string> placeholder in placeholders_lists)
                if (!string.IsNullOrEmpty(placeholder.Value))
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (KeyValuePair<string, string> placeholder in placeholders_table)
                if (!string.IsNullOrEmpty(placeholder.Value))
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);

            MainDocumentPart mainPart = wordDoc.MainDocumentPart;

            // CustomXmlPart erzeugen
            var customXmlPart = mainPart.CustomXmlParts
                                        .FirstOrDefault()
                             ?? mainPart.AddCustomXmlPart(CustomXmlPartType.CustomXml);

            // Positionsdaten für alle Pins schreiben
            var xml = "<positions>";
            int i = 1;
            foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
            {
                if (GlobalJson.Data.Plans[plan.Key].Pins != null && GlobalJson.Data.Plans[plan.Key].AllowExport)
                {
                    foreach (KeyValuePair<string, Pin> pin in GlobalJson.Data.Plans[plan.Key].Pins)
                    {
                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsAllowExport)
                        {
                            xml += $"<pos id='{i}'>{i}</pos>";
                            i += 1;
                        }
                    }
                }
            }
            xml += "</positions>";

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
                customXmlPart.FeedData(ms);

            // Properties mit StoreItemId erzeugen
            var propPart = customXmlPart.CustomXmlPropertiesPart
                           ?? customXmlPart.AddNewPart<CustomXmlPropertiesPart>();

            if (string.IsNullOrEmpty(storeItemId))
            {
                storeItemId = "{" + Guid.NewGuid().ToString().ToUpper() + "}";
            }

            propPart.DataStoreItem ??= new DataStoreItem { ItemId = storeItemId };


            // suche Tabelle mit Namen "Pin_Table"
            string tableTitle = "Pin_Table";
            Table table = mainPart?.Document?.Body?.Elements<Table>()
            .FirstOrDefault(t =>
            {
                // Überprüfen, ob die Tabelle Eigenschaften hat
                TableProperties tableProperties = t.GetFirstChild<TableProperties>();
                if (tableProperties != null)
                {
                    // Überprüfen, ob die Tabelle einen Titel (TableCaption) hat
                    TableCaption tableCaption = tableProperties.GetFirstChild<TableCaption>();
                    if (tableCaption != null && tableCaption.Val == tableTitle)
                        return true;
                }
                return false;
            });

            if (mainPart != null)
            {
                if (table != null)
                {
                    List<(int, int, string)> columnList = SearchTableColumns(table, placeholders_table); // Suche SpaltenNummern
                    i = 1;
                    foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
                    {
                        if (GlobalJson.Data.Plans[plan.Key].Pins != null && GlobalJson.Data.Plans[plan.Key].AllowExport)
                        {
                            foreach (KeyValuePair<string, Pin> pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            {
                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsAllowExport)
                                {
                                    // Anzahl Spalten ermitteln
                                    int columnCount = table.Elements<TableRow>().FirstOrDefault()?.Elements<TableCell>().Count() ?? 0;

                                    TableRow newRow = new();

                                    for (int column = 0; column < columnCount; column++)
                                    {
                                        var _columnPlaceholders = columnList.FindAll(item => item.Item1 == column);
                                        TableCell newTableCell = new();
                                        Paragraph newParagraph = new();

                                        if (_columnPlaceholders.Count > 0)
                                        {
                                            foreach ((int, int, string) ph in _columnPlaceholders)
                                            {
                                                // Hilfsfunktion: Text + Break anhängen
                                                void AddText(string text)
                                                {
                                                    if (!string.IsNullOrEmpty(text))
                                                    {
                                                        newParagraph.Append(new Run(new Text(text)));
                                                        newParagraph.Append(new Run(new Break()));   // Zeilenumbruch nur nach Text
                                                    }
                                                }

                                                switch (ph.Item3)
                                                {
                                                    case "${pin_nr}":
                                                        {
                                                            string tag = $"Pos_{i}";
                                                            string xpath = $"/positions/pos[@id='{i}']";
                                                            newParagraph.Append(new Run(CreateBoundSDTRun(tag, xpath, i.ToString())));
                                                            newParagraph.Append(new Run(new Break()));
                                                            break;
                                                        }

                                                    case "${pin_planName}":
                                                        AddText(GlobalJson.Data.Plans[plan.Key].Name);
                                                        break;

                                                    case "${pin_posImage}":
                                                        if (SettingsService.Instance.IsPosImageExport &&
                                                            !GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsCustomPin)
                                                        {
                                                            // Bild des Plans + Pin einfügen
                                                            string planName = GlobalJson.Data.Plans[plan.Key].File;
                                                            string planPath = Path.Combine(Settings.DataDirectory,
                                                                                           GlobalJson.Data.ProjectPath,
                                                                                           GlobalJson.Data.PlanPath,
                                                                                           planName);

                                                            Point pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                            string pinImage = Path.Combine(Settings.CacheDirectory, GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon);
                                                            Point pinAnchor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor;
                                                            float pinRotation = (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinRotation;
                                                            Size pinSize = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Size;
                                                            var planSize = GlobalJson.Data.Plans[plan.Key].ImageSize;
                                                            var cropFactor = new Point((1 / planSize.Width * 300) / 2,
                                                                                       (1 / planSize.Height * 300) / 2);
                                                            var crop = new OXML.Drawing.SourceRectangle
                                                            {
                                                                Left = (int)((pinPos.X - cropFactor.X) * 100000),
                                                                Top = (int)((pinPos.Y - cropFactor.Y) * 100000),
                                                                Right = (int)((1 - pinPos.X - cropFactor.X) * 100000),
                                                                Bottom = (int)((1 - pinPos.Y - cropFactor.Y) * 100000),
                                                            };

                                                            var exportSize = new SizeF
                                                            {
                                                                Width = SettingsService.Instance.PinPosExportSize,
                                                                Height = SettingsService.Instance.PinPosExportSize
                                                            };

                                                            var scaledPinSize = new Size
                                                            {
                                                                Width = pinSize.Width * exportSize.Width /
                                                                         SettingsService.Instance.PinPosCropExportSize *
                                                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale,
                                                                Height = pinSize.Height * exportSize.Height /
                                                                         SettingsService.Instance.PinPosCropExportSize *
                                                                         GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale
                                                            };

                                                            PointF posOnPlan = PivotRecalc(new Point(0.5, 0.5), pinRotation, pinAnchor, scaledPinSize, exportSize);

                                                            newParagraph.Append(new Run(GetImageElement(mainPart, planPath, exportSize,
                                                                                                        new Point(0, 0), 0, "anchor", crop)));
                                                            newParagraph.Append(new Run(GetImageElement(mainPart, pinImage, scaledPinSize,
                                                                                                        posOnPlan, pinRotation, "anchor")));
                                                            newParagraph.Append(new Run(new Break()));
                                                        }
                                                        break;

                                                    case "${pin_fotoList}":
                                                        if (SettingsService.Instance.IsImageExport)
                                                        {
                                                            foreach (var img in GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos)
                                                            {
                                                                if (!img.Value.AllowExport) continue;

                                                                string imgPath = Path.Combine(Settings.DataDirectory,
                                                                                              GlobalJson.Data.ProjectPath,
                                                                                              GlobalJson.Data.ImagePath,
                                                                                              img.Value.File);

                                                                if (!SettingsService.Instance.IsFotoOverlayExport &&
                                                                    img.Value.HasOverlay)
                                                                {
                                                                    imgPath = Path.Combine(Settings.DataDirectory,
                                                                                           GlobalJson.Data.ProjectPath,
                                                                                           GlobalJson.Data.ImagePath,
                                                                                           "originals",
                                                                                           img.Value.File);
                                                                }

                                                                var factor = img.Value.ImageSize.Width / img.Value.ImageSize.Height;
                                                                var scaledSize = new Size
                                                                {
                                                                    Width = SettingsService.Instance.ImageExportSize,
                                                                    Height = SettingsService.Instance.ImageExportSize / factor
                                                                };

                                                                if (SettingsService.Instance.IsFotoCompressed)
                                                                {
                                                                    var newPath = Path.Combine(Settings.CacheDirectory,
                                                                                               Path.GetFileName(img.Value.File));
                                                                    Helper.BitmapResizer(imgPath, newPath,
                                                                                         SettingsService.Instance.FotoCompressValue / 100f);
                                                                    imgPath = newPath;
                                                                }

                                                                newParagraph.Append(new Run(GetImageElement(mainPart, imgPath,
                                                                                                            scaledSize,
                                                                                                            new Point(0, 0), 0, "inline")));
                                                            }
                                                        }
                                                        break;

                                                    case "${pin_name}":
                                                        AddText(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName);
                                                        break;

                                                    case "${pin_desc}":
                                                        AddText(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key]
                                                                .PinDesc.Replace("\r\n", "\n")
                                                                .Replace("\r", "\n"));
                                                        break;

                                                    case "${pin_location}":
                                                        AddText(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation);
                                                        break;

                                                    case "${pin_priority}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinPriority != 0)
                                                        {
                                                            string fillColor = SettingsService.Instance.PriorityItems[
                                                                GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinPriority].Color;
                                                            newTableCell.TableCellProperties =
                                                                new TableCellProperties(new Shading { Fill = fillColor.Replace("#", "") });
                                                        }
                                                        break;

                                                    case "${pin_geolocWGS84}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                                                            AddText(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.ToString());
                                                        break;

                                                    case "${pin_geolocCH1903}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                                                            AddText(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.CH1903.ToString());
                                                        break;
                                                }
                                            }
                                        }

                                        newTableCell.Append(newParagraph);
                                        newRow.Append(newTableCell);
                                    }

                                    table.Append(newRow);
                                    i++;
                                }
                            }
                        }
                    }
                }

                // Add Title Image
                if (mainPart?.Document?.Body != null)
                {
                    foreach (Paragraph paragraph in mainPart.Document.Body.Elements<Paragraph>())
                    {
                        Run run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains("${title_image"));
                        if (run != null)
                        {
                            foreach (Text text in run.Elements<Text>())
                            {
                                if (text.Text.Contains("${title_image"))
                                {
                                    text.Text = ""; // Lösche den Platzhaltertext
                                    string imgPath = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                                    if (File.Exists(imgPath))
                                    {
                                        var factor = GlobalJson.Data.TitleImageSize.Width / GlobalJson.Data.TitleImageSize.Height;
                                        var scaledSize = new Size
                                        {
                                            Width = SettingsService.Instance.TitleExportSize * factor,
                                            Height = SettingsService.Instance.TitleExportSize
                                        };

                                        Drawing _img = GetImageElement(mainPart, imgPath, scaledSize, new Point(0, 0), 0, "inline");

                                        run.Append(_img);
                                    }
                                }
                            }
                        }
                    }
                }

                // Insert Plans
                if (SettingsService.Instance.IsPlanExport)
                {
                    // create Plan Index
                    if (mainPart?.Document?.Body != null)
                    {
                        foreach (Paragraph paragraph in mainPart.Document.Body.Elements<Paragraph>())
                        {
                            Run run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains("${plan_indexes}"));
                            if (run != null)
                            {
                                foreach (Text text in run.Elements<Text>())
                                {
                                    if (text.Text.Contains("${plan_indexes}"))
                                    {
                                        text.Remove(); // Lösche den Platzhaltertext
                                        foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
                                        {
                                            if (GlobalJson.Data.Plans[plan.Key].AllowExport)
                                            {
                                                run.Append(new Text("- " + GlobalJson.Data.Plans[plan.Key].Name));
                                                if (!string.IsNullOrWhiteSpace(GlobalJson.Data.Plans[plan.Key].Description))
                                                {
                                                    run.Append(new Text(" (" + GlobalJson.Data.Plans[plan.Key].Description + ")")
                                                    { Space = SpaceProcessingModeValues.Preserve });
                                                }
                                                run.Append(new Break() { Type = BreakValues.TextWrapping });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (mainPart?.Document?.Body != null)
                    {
                        // add Plan Images
                        foreach (Paragraph paragraph in mainPart.Document.Body.Elements<Paragraph>())
                        {
                            if (paragraph.InnerText.Contains("${plan_images/"))
                            {
                                SizeF planMaxSize = ExtractDimensions(paragraph.InnerText.ToString());
                                string replaceText = "${plan_images/" + planMaxSize.Width.ToString() + "/" + planMaxSize.Height.ToString() + "}";
                                
                                // lese Textformatierung des Platzhalters ein
                                string fontSizeVal = "28";
                                Run firstRun = paragraph.Elements<Run>().FirstOrDefault();
                                var existingFontSize = firstRun?.RunProperties?.FontSize;
                                if (existingFontSize != null)
                                    fontSizeVal = existingFontSize.Val;

                                // lösche den Platzhalter
                                paragraph.RemoveAllChildren();
                                paragraph.Append(new Run());

                                int planCounter = GlobalJson.Data.Plans.Count;
                                i = 1;
                                foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
                                {
                                    if (GlobalJson.Data.Plans[plan.Key].AllowExport)
                                    {
                                        // Erster Paragraph für den Plan-Namen
                                        Paragraph textParagraph = new();
                                        Run run = new();
                                        RunProperties runProperties = new(); // definiere Schriftgrösse
                                        DW.FontSize fontSize = new() { Val = fontSizeVal }; // 16pt Schriftgröße
                                        runProperties.Append(fontSize);
                                        run.PrependChild(runProperties);
                                        run.Append(new Text(GlobalJson.Data.Plans[plan.Key].Name));
                                        textParagraph.Append(run);
                                        paragraph.Append(textParagraph);

                                        // Zweiter Paragraph für das Plan-Image und Pins
                                        Paragraph imageAndPinsParagraph = new();
                                        run = new Run();
                                        string planImage = Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[plan.Key].File);
                                        Size planSize = GlobalJson.Data.Plans[plan.Key].ImageSize;
                                        SizeF scaledPlanSize = ScaleToFit(planSize, planMaxSize);
                                        SizeF scaleFactor = new(scaledPlanSize.Width / (float)planSize.Width, scaledPlanSize.Height / (float)planSize.Height);
                                        run.Append(GetImageElement(mainPart, planImage, scaledPlanSize, new Point(0, 0), 0, "anchor"));

                                        // Setze Pins
                                        if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                        {
                                            foreach (KeyValuePair<string, Pin> pin in GlobalJson.Data.Plans[plan.Key].Pins)
                                            {
                                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsAllowExport)
                                                {
                                                    string pinImage = Path.Combine(Settings.CacheDirectory, GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon);
                                                    PointF pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                    PointF pinAnchor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor;
                                                    SizeF pinSize = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Size;
                                                    SKColor pinColor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinColor;
                                                    float pinRotation = (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinRotation;
                                                    var scaledPinSize = new SizeF
                                                    {
                                                        Width = (float)(pinSize.Width * scaledPlanSize.Width / planSize.Width * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale),
                                                        Height = (float)(pinSize.Height * scaledPlanSize.Height / planSize.Height * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale)
                                                    };

                                                    PointF posOnPlan = PivotRecalc(pinPos, pinRotation, pinAnchor, scaledPinSize, scaledPlanSize);

                                                    run.Append(GetImageElement(
                                                        mainPart,
                                                        pinImage,
                                                        scaledPinSize,
                                                        posOnPlan,
                                                        pinRotation,
                                                        "anchor"
                                                    ));
                                                    run.Append(CreateTextBoxWithShape(SettingsService.Instance.PinLabelPrefix,
                                                                                      i,
                                                                                      storeItemId,
                                                                                      $"/positions/pos[@id='{i}']",
                                                                                      new Point(posOnPlan.X + scaledPinSize.Width + 1, posOnPlan.Y - scaledPinSize.Height),
                                                                                      SettingsService.Instance.PinLabelFontSize,
                                                                                      pinColor.ToString()[3..]));
                                                    i++;
                                                }
                                            }
                                        }
                                        // Füge das Plan-Image und die Pins zum Paragraph hinzu
                                        imageAndPinsParagraph.Append(run);
                                        paragraph.Append(imageAndPinsParagraph);

                                        // Erstelle einen neuen Paragraph mit einem Seitenumbruch (ausser nach dem letzen Plan)
                                        if (planCounter > 1)
                                        {
                                            Paragraph pageBreakParagraph = new(new Run(new Break() { Type = BreakValues.Page }));
                                            paragraph.Append(pageBreakParagraph);
                                        }
                                        planCounter--;
                                    }
                                }
                            }
                        }
                    }
                }

                // Ersetze alle {linebreak} im Dokument mit einem Zeilenumbruch
                ReplacePlaceholdersWithLineBreaks(wordDoc.MainDocumentPart, "{linebreak}");

                wordDoc.Save(); // Änderungen im MemoryStream speichern
            }
        }

        // Den bearbeiteten MemoryStream an den gewünschten Speicherort speichern
        using FileStream outputFileStream = new(savePath, FileMode.Create, FileAccess.Write);
        memoryStream.Position = 0; // Zurück zum Anfang des MemoryStreams, bevor du ihn schreibst
        memoryStream.CopyTo(outputFileStream);

        // beende alle Streams
        memoryStream.Close();
        memoryStream.Dispose();
        outputFileStream.Close();
        outputFileStream.Dispose();

        // lösche den Bild-Cache
        if (Directory.Exists(Settings.CacheDirectory))
            Directory.Delete(Settings.CacheDirectory, true);
    }

    private static SdtRun CreateBoundSDTRun(string tag, string xpath, string initialValue)
    {
        return new SdtRun(
            new SdtProperties(
                new SdtAlias { Val = tag },
                new Tag { Val = tag },
                new DataBinding { StoreItemId = storeItemId, XPath = xpath, PrefixMappings = "" }
            ),
            new SdtContentRun(
                new Run(new Text(initialValue) { Space = SpaceProcessingModeValues.Preserve })
            )
        );
    }

    private static Drawing GetImageElement(MainDocumentPart mainPart, string imgPath, SizeF size, Point pos, float rotationAngle, string wrap, OXML.Drawing.SourceRectangle crop = null)
    {
        crop ??= new OXML.Drawing.SourceRectangle();

        // Prüfen, ob das Bild bereits hinzugefügt wurde
        if (!imageRelationshipIds.TryGetValue(imgPath, out string relationshipId))
        {
            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
            using (FileStream stream = new(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                imagePart.FeedData(stream);
            }
            relationshipId = mainPart.GetIdOfPart(imagePart);
            imageRelationshipIds[imgPath] = relationshipId;
        }

        if (wrap == "inline")
            return GetInlinePicture(relationshipId, size, rotationAngle, crop);
        else
            return GetAnchorPicture(relationshipId, size, pos, rotationAngle, crop);
    }

    private static PointF PivotRecalc(PointF pos, float angle, PointF anchor, SizeF scaledPinSize, SizeF scaledPlanSize)
    {
        float W = scaledPinSize.Width;
        float H = scaledPinSize.Height;

        // Pivot im Bild
        float px = anchor.X * W;
        float py = anchor.Y * H;

        // Mitte des Bildes
        float cx = W / 2;
        float cy = H / 2;

        // Offset Pivot → Mitte
        float dx = px - cx;
        float dy = py - cy;

        // Rotierter Offset
        float rad = angle * (float)Math.PI / 180;
        float dxRot = dx * MathF.Cos(rad) - dy * MathF.Sin(rad);
        float dyRot = dx * MathF.Sin(rad) + dy * MathF.Cos(rad);

        // Final Position
        float finalX = pos.X * scaledPlanSize.Width - cx - dxRot + cx - (W / 2);
        float finalY = pos.Y * scaledPlanSize.Height - cy - dyRot + cy - (H / 2);

        return new PointF(finalX, finalY);
    }

    private static Drawing GetInlinePicture(String imagePartId, SizeF size, float rotationAngle, OXML.Drawing.SourceRectangle crop)
    {
        Drawing drawing = new();
        DDW.Inline inline = new()
        {
            DistanceFromTop = (OXML.UInt32Value)0U,
            DistanceFromBottom = (OXML.UInt32Value)0U,
            DistanceFromLeft = (OXML.UInt32Value)0U,
            DistanceFromRight = (OXML.UInt32Value)0U,
            Extent = new DDW.Extent
            {
                Cx = MillimetersToEMU(size.Width),
                Cy = MillimetersToEMU(size.Height)
            },
            EffectExtent = new DDW.EffectExtent
            {
                LeftEdge = 0L,
                TopEdge = 0L,
                RightEdge = 0L,
                BottomEdge = 0L
            },
            DocProperties = new DDW.DocProperties
            {
                Id = 1U,
                Name = "Picture"
            },
            NonVisualGraphicFrameDrawingProperties = new DDW.NonVisualGraphicFrameDrawingProperties()
        };

        OXML.Drawing.Graphic graphic = new();
        OXML.Drawing.GraphicData graphicData = new() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        OXML.Drawing.Pictures.Picture picture = new();

        OXML.Drawing.Pictures.NonVisualPictureProperties nvPicProps = new();
        OXML.Drawing.Pictures.NonVisualDrawingProperties nvDrawProps = new()
        {
            Id = 0U,
            Name = "Picture"
        };
        OXML.Drawing.Pictures.NonVisualPictureDrawingProperties nonVisualPicDrawingProps = new();
        nvPicProps.Append(nvDrawProps);
        nvPicProps.Append(nonVisualPicDrawingProps);

        OXML.Drawing.Pictures.BlipFill blipFill = new();

        OXML.Drawing.Blip blip = new()
        {
            Embed = imagePartId,
            CompressionState = OXML.Drawing.BlipCompressionValues.Print
        };

        OXML.Drawing.SourceRectangle srcRect = new()
        {
            Left = crop.Left,
            Top = crop.Top,
            Right = crop.Right,
            Bottom = crop.Bottom
        };
        blipFill.Append(blip);
        blipFill.Append(srcRect);

        OXML.Drawing.Stretch stretch = new();
        OXML.Drawing.FillRectangle fillRect = new();
        stretch.Append(fillRect);
        blipFill.Append(stretch);

        OXML.Drawing.Pictures.ShapeProperties shapeProps = new();
        OXML.Drawing.Transform2D transform2D = new();
        OXML.Drawing.Offset offset = new()
        {
            X = 0L,
            Y = 0L
        };
        OXML.Drawing.Extents extents = new()
        {
            Cx = MillimetersToEMU(size.Width),
            Cy = MillimetersToEMU(size.Height)
        };

        transform2D.Rotation = (OXML.Int32Value)(rotationAngle * 60000); // Rotation in 1/60000 Grad
        transform2D.Append(offset);
        transform2D.Append(extents);

        OXML.Drawing.PresetGeometry presetGeometry = new()
        {
            Preset = OXML.Drawing.ShapeTypeValues.Rectangle
        };
        OXML.Drawing.AdjustValueList adjustValueList = new();
        presetGeometry.Append(adjustValueList);

        shapeProps.Append(transform2D);
        shapeProps.Append(presetGeometry);

        picture.Append(nvPicProps);
        picture.Append(blipFill);
        picture.Append(shapeProps);

        graphicData.Append(picture);
        graphic.Append(graphicData);
        inline.Append(graphic);

        drawing.Append(inline);

        return drawing;
    }

    private static Drawing GetAnchorPicture(String imagePartId, SizeF size, Point pos, float rotationAngle, OXML.Drawing.SourceRectangle crop)
    {
        Drawing _drawing = new();
        DDW.Anchor _anchor = new()
        {
            DistanceFromTop = (OXML.UInt32Value)0U,
            DistanceFromBottom = (OXML.UInt32Value)0U,
            DistanceFromLeft = (OXML.UInt32Value)0U,
            DistanceFromRight = (OXML.UInt32Value)0U,
            SimplePos = false,
            RelativeHeight = (OXML.UInt32Value)0U,
            BehindDoc = true,
            Locked = false,
            LayoutInCell = true,
            AllowOverlap = true,
            EditId = "44CEF5E4",
            AnchorId = "44803ED1"
        };
        DDW.SimplePosition _spos = new()
        {
            X = MillimetersToEMU(pos.X),
            Y = MillimetersToEMU(pos.Y)
        };

        DDW.HorizontalPosition _hp = new()
        {
            RelativeFrom = DDW.HorizontalRelativePositionValues.Column
        };
        DDW.PositionOffset _hPO = new()
        {
            Text = MillimetersToEMU(pos.X).ToString()
        };
        _hp.Append(_hPO);

        DDW.VerticalPosition _vp = new()
        {
            RelativeFrom = DDW.VerticalRelativePositionValues.Paragraph
        };
        DDW.PositionOffset _vPO = new()
        {
            Text = MillimetersToEMU(pos.Y).ToString()
        };
        _vp.Append(_vPO);

        DDW.Extent _e = new()
        {
            Cx = MillimetersToEMU(size.Width),
            Cy = MillimetersToEMU(size.Height)
        };

        DDW.EffectExtent _ee = new()
        {
            LeftEdge = 0L,
            TopEdge = 0L,
            RightEdge = 0L,
            BottomEdge = 0L
        };

        DDW.WrapSquare _wp = new()
        {
            WrapText = DDW.WrapTextValues.BothSides
        };

        DDW.WrapPolygon _wpp = new()
        {
            Edited = false
        };

        DDW.StartPoint _sp = new()
        {
            X = 0L,
            Y = 0L
        };

        DDW.LineTo _l1 = new() { X = 0L, Y = 0L };
        DDW.LineTo _l2 = new() { X = 0L, Y = 0L };
        DDW.LineTo _l3 = new() { X = 0L, Y = 0L };
        DDW.LineTo _l4 = new() { X = 0L, Y = 0L };

        _wpp.Append(_sp);
        _wpp.Append(_l1);
        _wpp.Append(_l2);
        _wpp.Append(_l3);
        _wpp.Append(_l4);

        _wp.Append(_wpp);

        DDW.DocProperties _dp = new()
        {
            Id = 1U,
            Name = "Picture"
        };

        OXML.Drawing.Graphic _g = new();
        OXML.Drawing.GraphicData _gd = new() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };
        OXML.Drawing.Pictures.Picture _pic = new();

        OXML.Drawing.Pictures.NonVisualPictureProperties _nvpp = new();
        OXML.Drawing.Pictures.NonVisualDrawingProperties _nvdp = new()
        {
            Id = 0,
            Name = "Picture"
        };
        OXML.Drawing.Pictures.NonVisualPictureDrawingProperties _nvpdp = new();
        _nvpp.Append(_nvdp);
        _nvpp.Append(_nvpdp);


        OXML.Drawing.Pictures.BlipFill _bf = new();

        OXML.Drawing.Blip _b = new()
        {
            Embed = imagePartId,
            CompressionState = OXML.Drawing.BlipCompressionValues.Print
        };
        _bf.Append(_b);

        OXML.Drawing.SourceRectangle srcRect = new()
        {
            Left = crop.Left,
            Top = crop.Top,
            Right = crop.Right,
            Bottom = crop.Bottom
        };
        _bf.Append(srcRect);

        OXML.Drawing.Stretch _str = new();
        OXML.Drawing.FillRectangle _fr = new();

        _str.Append(_fr);
        _bf.Append(_str);

        OXML.Drawing.Pictures.ShapeProperties _shp = new();
        OXML.Drawing.Transform2D _t2d = new();
        OXML.Drawing.Offset _os = new()
        {
            X = 0L,
            Y = 0L
        };
        OXML.Drawing.Extents _ex = new()
        {
            Cx = MillimetersToEMU(size.Width),
            Cy = MillimetersToEMU(size.Height)
        };

        _t2d.Rotation = (OXML.Int32Value)(rotationAngle * 60000); // (in 1/60000 Grad, daher multiplizieren)
        _t2d.Append(_os);
        _t2d.Append(_ex);

        OXML.Drawing.PresetGeometry _preGeo = new()
        {
            Preset = OXML.Drawing.ShapeTypeValues.Rectangle
        };
        OXML.Drawing.AdjustValueList _adl = new();

        _preGeo.Append(_adl);

        _shp.Append(_t2d);
        _shp.Append(_preGeo);

        _pic.Append(_nvpp);
        _pic.Append(_bf);
        _pic.Append(_shp);

        _gd.Append(_pic);

        _g.Append(_gd);

        _anchor.Append(_spos);
        _anchor.Append(_hp);
        _anchor.Append(_vp);
        _anchor.Append(_e);
        _anchor.Append(_ee);
        _anchor.Append(_wp);
        _anchor.Append(_dp);
        _anchor.Append(_g);

        _drawing.Append(_anchor);

        return _drawing;
    }

    private static long MillimetersToEMU(double millimeters)
    {
        return Convert.ToInt64(millimeters * 36000);
    }

    private static List<string> GetUniquePinIcons(JsonDataModel jsonDataModel)
    {
        HashSet<string> uniquePinIcons = [];
        if (jsonDataModel.Plans != null)
        {
            foreach (Plan plan in jsonDataModel.Plans.Values)
            {
                if (plan.Pins != null)
                {
                    foreach (Pin pin in plan.Pins.Values)
                    {
                        if (!string.IsNullOrEmpty(pin.PinIcon))
                            uniquePinIcons.Add(pin.PinIcon);
                    }
                }
            }
        }
        return [.. uniquePinIcons];
    }

    private static List<(int columnIndex, int positionInText, string placeholderKey)> SearchTableColumns(Table table, Dictionary<string, string> placeholders_table)
    {
        List<(int, int, string)> columnList = [];
        foreach (TableRow row in table.Elements<TableRow>())
        {
            int columnIndex = 0; // Spaltenzähler
            foreach (TableCell cell in row.Elements<TableCell>())
            {
                foreach (Paragraph paragraph in cell.Elements<Paragraph>())
                {
                    string paragraphText = paragraph.InnerText; // Gesamter Text des Paragraphen
                    foreach (KeyValuePair<string, string> placeholder in placeholders_table)
                    {
                        int position = paragraphText.IndexOf(placeholder.Key);
                        if (position != -1) // Platzhalter gefunden
                        {
                            columnList.Add((columnIndex, position, placeholder.Key));
                            Run run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains(placeholder.Key));
                            if (run != null)
                            {
                                foreach (Text text in run.Elements<Text>())
                                {
                                    if (text.Text.Contains(placeholder.Key))
                                        text.Text = text.Text.Replace(placeholder.Key, ""); // Platzhalter entfernen
                                }
                            }
                        }
                    }
                }
                columnIndex++; // Spaltenzähler erhöhen
            }
        }

        // Sortiere die Liste nach Spalte und Position im Text
        columnList.Sort((x, y) =>
        {
            int result = x.Item1.CompareTo(y.Item1); // Zuerst nach Spalte (Item1) sortieren
            if (result == 0)
                result = x.Item2.CompareTo(y.Item2); // Wenn Spalte gleich, nach Position im Text (Item2) sortieren
            return result;
        });

        return columnList;
    }

    private static Picture CreateTextBoxWithShape(
        string preText,
        int posNr,
        string storeItemId,
        string xpath,
        Point coordinateMM,
        double fontSizePt,
        string fontColorHex)
    {
        double xPt = coordinateMM.X * 2.83465;
        double yPt = coordinateMM.Y * 2.83465;

        Picture picture = new();

        // VML-Shape
        Shape shape = new()
        {
            Id = "TextBoxShape",
            Style = $"position:absolute;margin-left:{xPt}pt;margin-top:{yPt}pt;" +
                    "mso-fit-shape-to-text:t;mso-wrap-style:none;",
            Stroked = TrueFalseValue.FromBoolean(true),
            Filled = TrueFalseValue.FromBoolean(true)
        };

        shape.Append(
            new Fill { Color = "#FFFFFF", On = TrueFalseValue.FromBoolean(true) },
            new Stroke { Color = fontColorHex, Weight = "1pt", On = TrueFalseValue.FromBoolean(true) }
        );

        // TextBox innerhalb der Shape
        TextBox textBox = new()
        {
            Style = "mso-fit-shape-to-text:t;mso-wrap-style:none;",
            Inset = "0pt,0pt,0pt,0pt"
        };
        TextBoxContent textBoxContent = new();

        // Paragraph
        DW.Paragraph paragraph = new();
        ParagraphProperties paragraphProperties = new();

        // Rahmen + Hintergrundfarbe
        Shading paragraphShading = new()
        {
            Fill = "FFFFFF",
            Val = ShadingPatternValues.Clear
        };
        ParagraphBorders paragraphBorders = new()
        {
            TopBorder = new TopBorder { Val = BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },
            BottomBorder = new BottomBorder { Val = BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },
            LeftBorder = new LeftBorder { Val = BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },
            RightBorder = new RightBorder { Val = BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U }
        };
        paragraphProperties.Append(paragraphShading);
        paragraphProperties.Append(paragraphBorders);
        paragraph.Append(paragraphProperties);

        // Präfixtext
        if (!string.IsNullOrEmpty(preText))
        {
            paragraph.Append(
                new DW.Run(
                    new DW.RunProperties(
                        new DW.RunFonts { Ascii = "Arial", HighAnsi = "Arial", EastAsia = "Arial" },
                        new DW.FontSize { Val = (fontSizePt * 2).ToString() },
                        new DW.Color { Val = fontColorHex.TrimStart('#') }
                    ),
                    new DW.Text(preText) { Space = SpaceProcessingModeValues.Preserve }
                )
            );
        }

        // SDT
        var sdtRun = new DW.SdtRun(
            new DW.SdtProperties(
                new DW.SdtAlias { Val = $"Pos_{posNr}" },
                new DW.Tag { Val = $"Pos_{posNr}" },
                new DW.DataBinding { StoreItemId = storeItemId, XPath = xpath, PrefixMappings = "" },
                new DW.RunProperties(
                    new DW.RunFonts { Ascii = "Arial", HighAnsi = "Arial", EastAsia = "Arial" },
                    new DW.FontSize { Val = (fontSizePt * 2).ToString() },
                    new DW.Color { Val = fontColorHex.TrimStart('#') }
                )
            ),
            new DW.SdtContentRun(
                new DW.Run(
                    new DW.RunProperties(
                        new DW.RunFonts { Ascii = "Arial", HighAnsi = "Arial", EastAsia = "Arial" },
                        new DW.FontSize { Val = (fontSizePt * 2).ToString() },
                        new DW.Color { Val = fontColorHex.TrimStart('#') }
                    ),
                    new DW.Text(posNr.ToString()) { Space = SpaceProcessingModeValues.Preserve }
                )
            )
        );
        paragraph.Append(sdtRun);

        textBoxContent.Append(paragraph);
        textBox.Append(textBoxContent);
        shape.Append(textBox);
        picture.Append(shape);

        return picture;
    }

    private static SizeF ScaleToFit(Size originalSize, SizeF maxTargetSize)
    {
        double widthScale = maxTargetSize.Width != 0
            ? (double)maxTargetSize.Width / originalSize.Width
            : double.PositiveInfinity;

        double heightScale = maxTargetSize.Height != 0
            ? (double)maxTargetSize.Height / originalSize.Height
            : double.PositiveInfinity;

        double scale = Math.Min(widthScale, heightScale);

        int newWidth = (int)(originalSize.Width * scale);
        int newHeight = (int)(originalSize.Height * scale);

        return new SizeF(newWidth, newHeight);
    }

    private static SizeF ExtractDimensions(string input)
    {
        Regex regex = PlanImagesRegex();
        Match match = regex.Match(input);

        if (match.Success)
        {
            int width = int.Parse(match.Groups[1].Value);
            int height = int.Parse(match.Groups[2].Value);
            return new SizeF(width, height);
        }
        return new SizeF(250, 140);
    }

    public static void CopyImageToDirectory(string destinationPath, string path, string icon)
    {
        string destinationFilePath = Path.Combine(destinationPath, icon);
        string sourceFilePath = Path.Combine(Settings.DataDirectory, path, icon);

        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
            Directory.CreateDirectory(Path.Combine(destinationPath, "customicons"));
        }

        File.Copy(sourceFilePath, destinationFilePath, true);
    }

    public static void ReplacePlaceholdersWithLineBreaks(MainDocumentPart mainPart, string placeholder)
    {
        string regexPattern = Regex.Escape(placeholder); // Sonderzeichen escapen

        foreach (var paragraph in mainPart.Document.Descendants<Paragraph>())
        {
            foreach (var run in paragraph.Elements<Run>())
            {
                var textElement = run.GetFirstChild<Text>();
                if (textElement != null && textElement.Text.Contains(placeholder))
                {
                    var parts = Regex.Split(textElement.Text, regexPattern);
                    run.RemoveAllChildren<Text>();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        run.AppendChild(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
                        if (i < parts.Length - 1)
                        {
                            run.AppendChild(new Break());
                        }
                    }
                }
            }
        }
    }
}
