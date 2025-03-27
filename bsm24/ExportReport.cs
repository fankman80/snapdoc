#nullable disable

using bsm24.Models;
using bsm24.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Vml;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using System.Text.RegularExpressions;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using OXML = DocumentFormat.OpenXml;

namespace bsm24;

public partial class ExportReport
{
    [GeneratedRegex(@"\$\{plan_images/(\d+)/(\d+)\}")]
    private static partial Regex PlanImagesRegex();

    private static readonly Dictionary<string, string> imageRelationshipIds = [];

    public static async Task DocX(string templateDoc, string savePath)
    {
        Dictionary<string, string> placeholders_single = new()
        {
            {"${client_name}", GlobalJson.Data.Client_name},
            {"${object_address}", GlobalJson.Data.Object_address},
            {"${working_title}", GlobalJson.Data.Working_title},
            {"${object_name}", GlobalJson.Data.Object_name},
            {"${creation_date}", GlobalJson.Data.Creation_date.Date.ToString("D")},
            {"${project_manager}", GlobalJson.Data.Project_manager},
        };
        Dictionary<string, string> placeholders_lists = new()
        {
            {"${plan_indexes}", "${plan_indexes}"}, //bereinige splitted runs
            {"${plan_images/", "${plan_images/"},   //bereinige splitted runs  
            {"${title_image}", "${title_image}"},   //bereinige splitted runs
        };
        Dictionary<string, string> placeholders_table = new()
        {
            {"${pin_nr}", "${pin_nr}"},             //bereinige splitted runs
            {"${pin_planName}", "${pin_planName}"}, //bereinige splitted runs
            {"${pin_posImage}", "${pin_posImage}"}, //bereinige splitted runs
            {"${pin_fotoList}", "${pin_fotoList}"}, //bereinige splitted runs
            {"${pin_name}", "${pin_name}"},         //bereinige splitted runs
            {"${pin_desc}", "${pin_desc}"},         //bereinige splitted runs
            {"${pin_location}", "${pin_location}"}, //bereinige splitted runs
            {"${pin_priority}", "${pin_priority}"}, //bereinige splitted runs
            {"${pin_geolocWGS84}", "${pin_geolocWGS84}"}, //bereinige splitted runs
            {"${pin_geolocCH1903}", "${pin_geolocCH1903}"}, //bereinige splitted runs
        };

        List<string> uniquePinIcons = GetUniquePinIcons(GlobalJson.Data);
        foreach (string icon in uniquePinIcons)
            if (icon.Contains("custompin_", StringComparison.OrdinalIgnoreCase)) //check if icon is a custompin
                CopyImageToDirectory(Settings.CacheDirectory, System.IO.Path.Combine(GlobalJson.Data.ProjectPath, GlobalJson.Data.CustomPinsPath), icon);
            else if (icon.Contains("customicons", StringComparison.OrdinalIgnoreCase)) //check if icon is a customicon
                CopyImageToDirectory(System.IO.Path.Combine(Settings.CacheDirectory, "customicons"), "customicons", System.IO.Path.GetFileName(icon));
            else
                await CopyImageToDirectoryAsync(Settings.CacheDirectory, icon);

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
                if (placeholder.Value != "")
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (KeyValuePair<string, string> placeholder in placeholders_lists)
                if (placeholder.Value != "")
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (KeyValuePair<string, string> placeholder in placeholders_table)
                if (placeholder.Value != "")
                    Codeuctivity.OpenXmlPowerTools.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);

            MainDocumentPart mainPart = wordDoc.MainDocumentPart;

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
                    int i = 1;
                    foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
                    {
                        if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                        {
                            foreach (KeyValuePair<string, Pin> pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            {
                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport)
                                {
                                    // Anzahl Spalten ermitteln
                                    int columnCount = 0;
                                    TableRow firstRow = table.Elements<TableRow>().FirstOrDefault();
                                    if (firstRow != null)
                                        columnCount = firstRow.Elements<TableCell>().Count();

                                    TableRow newRow = new();
                                    for (int column = 0; column < columnCount; column++)
                                    {
                                        List<(int, int, string)> _columnPlaceholders = columnList.FindAll(item => item.Item1 == column);
                                        TableCell newTableCell = new();

                                        if (_columnPlaceholders.Count == 0)
                                        {
                                            // Falls keine Platzhalter vorhanden sind, füge einen leeren Paragraph hinzu
                                            Paragraph emptyParagraph = new();
                                            emptyParagraph.Append(new Run(new Text("")));
                                            newTableCell.Append(emptyParagraph);
                                        }
                                        else
                                        {
                                            foreach ((int, int, string) _placeholder in _columnPlaceholders)
                                            {
                                                Paragraph newParagraph = new();
                                                String text = "";
                                                switch (_placeholder.Item3)
                                                {
                                                    case "${pin_nr}":
                                                        text = i.ToString();
                                                        break;

                                                    case "${pin_planName}":
                                                        text = GlobalJson.Data.Plans[plan.Key].Name;
                                                        break;

                                                    case "${pin_posImage}":
                                                        if (SettingsService.Instance.IsPosImageExport && !GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].IsCustomPin)
                                                        {
                                                            // add Part of Plan Image
                                                            string planName = GlobalJson.Data.Plans[plan.Key].File;
                                                            string planPath = System.IO.Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, planName);
                                                            Point pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                            string pinImage = System.IO.Path.Combine(Settings.CacheDirectory, GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon);
                                                            Point pinAnchor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor;
                                                            float pinRotation = (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinRotation;
                                                            Size pinSize = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Size;
                                                            var cropPos = new SKPoint((float)pinPos.X, (float)pinPos.Y);
                                                            var planSize = GlobalJson.Data.Plans[plan.Key].ImageSize;
                                                            var cropFactor = new Point((1 / planSize.Width * 300) / 2, (1 / planSize.Height * 300) / 2);
                                                            var crop = new SKRectI
                                                            {
                                                                Left = (int)((cropPos.X - cropFactor.X) * 100000),
                                                                Top = (int)((cropPos.Y - cropFactor.Y) * 100000),
                                                                Right = (int)((1 - cropPos.X - cropFactor.X) * 100000),
                                                                Bottom = (int)((1 - cropPos.Y - cropFactor.Y) * 100000),
                                                            };
                                                            var exportSize = new SizeF
                                                            { 
                                                                Width = SettingsService.Instance.PinPosExportSize,
                                                                Height = SettingsService.Instance.PinPosExportSize
                                                            };
                                                            var scaledPinSize = new Size
                                                            {
                                                                Width = pinSize.Width * exportSize.Width / SettingsService.Instance.PinPosCropExportSize * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale,
                                                                Height = pinSize.Height * exportSize.Height / SettingsService.Instance.PinPosCropExportSize * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale
                                                            };
                                                            var pinPoint = new Point
                                                            {
                                                                X = (exportSize.Width / 2) - (scaledPinSize.Width * pinAnchor.X),
                                                                Y = (exportSize.Height / 2) - (scaledPinSize.Height * pinAnchor.Y)
                                                            };

                                                            Drawing _imgPlan = GetImageElement(mainPart, planPath, exportSize, new Point(0, 0), 0, "anchor", crop);
                                                            Drawing _imgPin = GetImageElement(mainPart, pinImage, scaledPinSize, pinPoint, pinRotation, "anchor");

                                                            newParagraph.Append(new Run(_imgPlan));
                                                            newParagraph.Append(new Run(_imgPin));
                                                        }
                                                        break;

                                                    case "${pin_fotoList}":
                                                        if (SettingsService.Instance.IsImageExport)
                                                        {
                                                            // add Pictures
                                                            foreach (KeyValuePair<string, Foto> img in GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos)
                                                            {
                                                            Run newRun = new();
                                                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].IsChecked)
                                                                {
                                                                    string imgName = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].File;
                                                                    string imgPath = System.IO.Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, imgName);
                                                                    if (!SettingsService.Instance.IsFotoOverlayExport)
                                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].HasOverlay)
                                                                            imgPath = System.IO.Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, "originals", imgName);

                                                                    var factor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].ImageSize.Width / GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].ImageSize.Height;
                                                                    var scaledSize = new Size
                                                                    {
                                                                        Width = SettingsService.Instance.ImageExportSize,
                                                                        Height = SettingsService.Instance.ImageExportSize / factor
                                                                    };
                                                                    
                                                                    Drawing _img = GetImageElement(mainPart, imgPath, scaledSize, new Point(0, 0), 0, "inline");

                                                                    newRun.Append(_img);
                                                                }
                                                            newParagraph.Append(newRun);
                                                            }
                                                        }
                                                        break;

                                                    case "${pin_name}":
                                                        text = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName;
                                                        break;

                                                    case "${pin_desc}":
                                                        text = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc;
                                                        break;

                                                    case "${pin_location}":
                                                        text = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation;
                                                        break;

                                                    case "${pin_priority}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinPriority != 0)
                                                        {
                                                            string fillColor = Settings.PriorityItems[GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinPriority].Color;
                                                            TableCellProperties cellColor = new(new Shading() { Fill = fillColor.Replace("#", "") });
                                                            newTableCell.Append(cellColor);
                                                        }
                                                        newParagraph.Append(new Run(new Text(Settings.PriorityItems[GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinPriority].Key)));
                                                        break;

                                                    case "${pin_geolocWGS84}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                                                            text = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.WGS84.ToString();
                                                        break;

                                                    case "${pin_geolocCH1903}":
                                                        if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation != null)
                                                            text = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].GeoLocation.CH1903.ToString();
                                                        break;

                                                    default:
                                                        text = "";
                                                        break;
                                                }

                                                newParagraph.Append(new Run(new Text(text)));

                                                if (newParagraph.Elements<Run>().Any())
                                                    newTableCell.Append(newParagraph);
                                            }
                                        }
                                        newRow.Append(newTableCell);
                                    }
                                    table.Append(newRow);
                                    i += 1;
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
                                    string imgPath = System.IO.Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
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
                                            run.Append(new Text("- " + GlobalJson.Data.Plans[plan.Key].Name));
                                            run.Append(new Break() { Type = BreakValues.TextWrapping });
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
                                string fontSizeVal = "28";
                                Run firstRun = paragraph.Elements<Run>().FirstOrDefault();
                                var existingFontSize = firstRun?.RunProperties?.FontSize;
                                if (existingFontSize != null)
                                    fontSizeVal = existingFontSize.Val;

                                int i = 1;
                                foreach (KeyValuePair<string, Plan> plan in GlobalJson.Data.Plans)
                                {
                                    // Erster Paragraph für den Plan-Namen
                                    Paragraph textParagraph = new();
                                    RunProperties runProperties = new(); // definiere Schriftgrösse
                                    DocumentFormat.OpenXml.Wordprocessing.FontSize fontSize = new() { Val = fontSizeVal }; // 16pt Schriftgröße
                                    runProperties.Append(fontSize);

                                    Run run = new();
                                    run.PrependChild(runProperties);
                                    run.Append(new Text(GlobalJson.Data.Plans[plan.Key].Name));
                                    textParagraph.Append(run);

                                    // Füge den Paragraph (Plan-Namen) hinzu
                                    paragraph.Append(textParagraph);

                                    // Zweiter Paragraph für das Plan-Image und Pins (ohne manuelles Break)
                                    Paragraph imageAndPinsParagraph = new();
                                    string planImage = System.IO.Path.Combine(Settings.DataDirectory, GlobalJson.Data.ProjectPath, GlobalJson.Data.PlanPath, GlobalJson.Data.Plans[plan.Key].File);
                                    Size planSize = GlobalJson.Data.Plans[plan.Key].ImageSize;
                                    SizeF scaledPlanSize = ScaleToFit(planSize, planMaxSize);
                                    SizeF scaleFactor = new(scaledPlanSize.Width / (float)planSize.Width, scaledPlanSize.Height / (float)planSize.Height);

                                    run = new Run();
                                    run.Append(GetImageElement(mainPart, planImage, scaledPlanSize, new Point(0, 0), 0, "anchor"));

                                    if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                    {
                                        foreach (KeyValuePair<string, Pin> pin in GlobalJson.Data.Plans[plan.Key].Pins)
                                        {
                                            if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport)
                                            {
                                                string pinImage = System.IO.Path.Combine(Settings.CacheDirectory, GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon);
                                                Point pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                Point pinAnchor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor;
                                                Size pinSize = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Size;
                                                SKColor pinColor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinColor;

                                                var scaledPinSize = new SizeF
                                                {
                                                    Width = (float)(pinSize.Width * scaledPlanSize.Width / planSize.Width * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale),
                                                    Height = (float)(pinSize.Height * scaledPlanSize.Height / planSize.Height * GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinScale)
                                                };    
                                                
                                                Point posOnPlan = new((pinPos.X * scaledPlanSize.Width) - (pinAnchor.X * scaledPinSize.Width),
                                                                      (pinPos.Y * scaledPlanSize.Height) - (pinAnchor.Y * scaledPinSize.Height));
                                                float rotationAngle = (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinRotation;

                                                run.Append(GetImageElement(mainPart, pinImage, new SizeF(scaledPinSize.Width, scaledPinSize.Height), posOnPlan, rotationAngle, "anchor"));
                                                run.Append(CreateTextBoxWithShape(SettingsService.Instance.PlanLabelPrefix + i.ToString(),
                                                                                  new Point(posOnPlan.X + scaledPinSize.Width + 1, posOnPlan.Y - scaledPinSize.Height), // offset 1mm to right
                                                                                  SettingsService.Instance.PlanLabelFontSize,
                                                                                  pinColor.ToString()[3..]));
                                                i += 1;
                                            }
                                        }
                                    }

                                    // Füge das Plan-Image und die Pins zum Paragraph hinzu
                                    imageAndPinsParagraph.Append(run);
                                    paragraph.Append(imageAndPinsParagraph);

                                    // Erstelle einen neuen Paragraph mit einem Seitenumbruch
                                    Paragraph pageBreakParagraph = new(new Run(new Break() { Type = BreakValues.Page }));
                                    paragraph.Append(pageBreakParagraph);
                                }
                            }
                        }
                    }
                }
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

    private static Drawing GetImageElement(MainDocumentPart mainPart, string imgPath, SizeF size, Point pos, float rotationAngle, string wrap, SKRectI? crop = null)
    {
        crop ??= new SKRectI(0, 0, 0, 0);

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
            return GetInlinePicture(relationshipId, size, rotationAngle, crop.Value);
        else
            return GetAnchorPicture(relationshipId, size, pos, rotationAngle, crop.Value);
    }

    private static Drawing GetInlinePicture(String imagePartId, SizeF size, float rotationAngle, SKRectI crop)
    {
        Drawing drawing = new();
        DW.Inline inline = new()
        {
            DistanceFromTop = (OXML.UInt32Value)0U,
            DistanceFromBottom = (OXML.UInt32Value)0U,
            DistanceFromLeft = (OXML.UInt32Value)0U,
            DistanceFromRight = (OXML.UInt32Value)0U,
            Extent = new DW.Extent
            {
                Cx = MillimetersToEMU(size.Width),
                Cy = MillimetersToEMU(size.Height)
            },
            EffectExtent = new DW.EffectExtent
            {
                LeftEdge = 0L,
                TopEdge = 0L,
                RightEdge = 0L,
                BottomEdge = 0L
            },
            DocProperties = new DW.DocProperties
            {
                Id = 1U,
                Name = "Picture"
            },
            NonVisualGraphicFrameDrawingProperties = new DW.NonVisualGraphicFrameDrawingProperties()
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

    private static Drawing GetAnchorPicture(String imagePartId, SizeF size, Point pos, float rotationAngle, SKRectI crop)
    {
        Drawing _drawing = new();
        DW.Anchor _anchor = new()
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
        DW.SimplePosition _spos = new()
        {
            X = MillimetersToEMU(pos.X),
            Y = MillimetersToEMU(pos.Y)
        };

        DW.HorizontalPosition _hp = new()
        {
            RelativeFrom = DW.HorizontalRelativePositionValues.Column
        };
        DW.PositionOffset _hPO = new()
        {
            Text = MillimetersToEMU(pos.X).ToString()
        };
        _hp.Append(_hPO);

        DW.VerticalPosition _vp = new()
        {
            RelativeFrom = DW.VerticalRelativePositionValues.Paragraph
        };
        DW.PositionOffset _vPO = new()
        {
            Text = MillimetersToEMU(pos.Y).ToString()
        };
        _vp.Append(_vPO);

        DW.Extent _e = new()
        {
            Cx = MillimetersToEMU(size.Width),
            Cy = MillimetersToEMU(size.Height)
        };

        DW.EffectExtent _ee = new()
        {
            LeftEdge = 0L,
            TopEdge = 0L,
            RightEdge = 0L,
            BottomEdge = 0L
        };

        DW.WrapSquare _wp = new()
        {
            WrapText = DW.WrapTextValues.BothSides
        };

        DW.WrapPolygon _wpp = new()
        {
            Edited = false
        };

        DW.StartPoint _sp = new()
        {
            X = 0L,
            Y = 0L
        };

        DW.LineTo _l1 = new() { X = 0L, Y = 0L };
        DW.LineTo _l2 = new() { X = 0L, Y = 0L };
        DW.LineTo _l3 = new() { X = 0L, Y = 0L };
        DW.LineTo _l4 = new() { X = 0L, Y = 0L };

        _wpp.Append(_sp);
        _wpp.Append(_l1);
        _wpp.Append(_l2);
        _wpp.Append(_l3);
        _wpp.Append(_l4);

        _wp.Append(_wpp);

        DW.DocProperties _dp = new()
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

    private static Picture CreateTextBoxWithShape(string text, Point coordinateMM, double fontSizePt, string fontColorHex)
    {
        double xCoordinatePt = coordinateMM.X * 2.83465;
        double yCoordinatePt = coordinateMM.Y * 2.83465;
        double textWidthPt = GetTextWidthInPoints(text, "Arial", fontSizePt, 96) * 2;

        Picture picture1 = new();

        Shape shape1 = new()
        {
            Id = "TextBoxShape",
            Style = $"position:absolute;margin-left:{xCoordinatePt}pt;margin-top:{yCoordinatePt}pt;width:{textWidthPt}pt;mso-fit-shape-to-text:t;mso-wrap-style:square;",
        };

        Fill fill1 = new() { Color = "#FFFFFF" };
        Stroke stroke1 = new() { Color = fontColorHex, Weight = "1pt" };

        TextBox textBox1 = new()
        {
            Style = "mso-fit-shape-to-text:t;mso-wrap-style:square;",
            Inset = "0pt, 0pt, 0pt, 0pt"
        };
        TextBoxContent textBoxContent1 = new();

        Paragraph paragraph2 = new();
        ParagraphProperties paragraphProperties = new();

        Shading paragraphShading = new()
        {
            Fill = "FFFFFF",
            Val = ShadingPatternValues.Clear
        };

        ParagraphBorders paragraphBorders = new()
        {
            TopBorder = new OXML.Wordprocessing.TopBorder() { Val = OXML.Wordprocessing.BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },  // Obere Linie
            BottomBorder = new OXML.Wordprocessing.BottomBorder() { Val = OXML.Wordprocessing.BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },  // Untere Linie
            LeftBorder = new OXML.Wordprocessing.LeftBorder() { Val = OXML.Wordprocessing.BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U },  // Linke Linie
            RightBorder = new OXML.Wordprocessing.RightBorder() { Val = OXML.Wordprocessing.BorderValues.Single, Color = fontColorHex.Replace("#", ""), Size = 6U }  // Rechte Linie
        };

        // Füge die Hintergrundfarbe und die Rahmen zu den Absatzeigenschaften hinzu
        paragraphProperties.Append(paragraphShading);  // Hintergrundfarbe des Absatzes
        paragraphProperties.Append(paragraphBorders);  // Rahmen um den Absatz

        // Definiere den Text und seine Eigenschaften (Größe und Farbe)
        Run run2 = new();
        RunProperties runProperties = new()
        {
            FontSize = new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = (fontSizePt * 2).ToString() },  // Schriftgröße
            Color = new DocumentFormat.OpenXml.Wordprocessing.Color() { Val = fontColorHex.Replace("#", "") },  // Schriftfarbe
            RunFonts = new DocumentFormat.OpenXml.Wordprocessing.RunFonts() { Ascii = "Arial" }  // Schriftart auf Arial setzen
        };

        // Füge den Textinhalt hinzu
        DocumentFormat.OpenXml.Wordprocessing.Text text2 = new() { Text = text };

        // Setze die Formatierungen auf den Text und füge ihn hinzu
        run2.Append(runProperties);
        run2.Append(text2);
        paragraph2.Append(paragraphProperties);  // Füge die Absatz-Eigenschaften hinzu
        paragraph2.Append(run2);
        textBoxContent1.Append(paragraph2);

        // Füge den Textinhalt zur TextBox hinzu
        textBox1.Append(textBoxContent1);
        shape1.Append(textBox1);
        shape1.Append(fill1);  // Hintergrundfarbe der Form
        shape1.Append(stroke1);  // Rand der Form
        picture1.Append(shape1);

        return picture1;
    }

    private static double GetTextWidthInPoints(string text, string fontName, double fontSizePt, double dpi)
    {
        using SKFont font = new(SKTypeface.FromFamilyName(fontName), (float)fontSizePt);
        float textWidthInPixels = font.MeasureText(text + " ");
        double textWidthInPoints = textWidthInPixels * 72 / dpi;
        return textWidthInPoints;
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
        string destinationFilePath = System.IO.Path.Combine(destinationPath, icon);
        string sourceFilePath = System.IO.Path.Combine(Settings.DataDirectory, path, icon);

        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
            Directory.CreateDirectory(System.IO.Path.Combine(destinationPath, "customicons"));
        }

        File.Copy(sourceFilePath, destinationFilePath, true);
    }

    public static async Task CopyImageToDirectoryAsync(string destinationPath, string icon)
    {
        string destinationFilePath = System.IO.Path.Combine(destinationPath, icon);

        if (!Directory.Exists(destinationPath))
            Directory.CreateDirectory(destinationPath);

        Stream stream = null;

        try
        {
            // Überprüfen, ob der Stream geöffnet werden kann
            if (System.IO.Path.IsPathRooted(icon) && File.Exists(icon))
            {
                stream = File.OpenRead(icon);
            }
#if ANDROID
            if (stream == null)
            {
                var context = Android.App.Application.Context;
                var resources = context.Resources;

                var resourceId = resources.GetIdentifier(System.IO.Path.GetFileNameWithoutExtension(icon), "drawable", context.PackageName);
                if (resourceId > 0)
                {
                    var imageUri = new Android.Net.Uri.Builder()
                        .Scheme(Android.Content.ContentResolver.SchemeAndroidResource)
                        .Authority(resources.GetResourcePackageName(resourceId))
                        .AppendPath(resources.GetResourceTypeName(resourceId))
                        .AppendPath(resources.GetResourceEntryName(resourceId))
                        .Build();

                    stream = context.ContentResolver.OpenInputStream(imageUri);
                }
            }
#elif WINDOWS
            if (stream == null)
            {
                try
                {
                    Windows.Storage.StorageFile sf = await Windows.Storage.StorageFile.GetFileFromPathAsync(icon);
                    if (sf is not null)
                    {
                        stream = await sf.OpenStreamForReadAsync();
                    }
                }
                catch
                {
                }

                if (stream == null && AppInfo.PackagingModel == AppPackagingModel.Packaged)
                {
                    Uri uri = new("ms-appx:///" + icon);
                    Windows.Storage.StorageFile sf = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri);
                    stream = await sf.OpenStreamForReadAsync();
                }
                else if (stream == null)
                {
                    string root = AppContext.BaseDirectory;
                    icon = System.IO.Path.Combine(root, icon);
                    if (File.Exists(icon))
                        stream = File.OpenRead(icon);
                }
            }
#elif IOS || MACCATALYST
            if (stream == null)
            {
                var root = Foundation.NSBundle.MainBundle.BundlePath;
#if MACCATALYST || MACOS
                root = Path.Combine(root, "Contents", "Resources");
#endif
                icon = Path.Combine(root, icon);
                if (File.Exists(icon))
                    stream = File.OpenRead(icon);
            }
#endif
            if (stream == null)
                throw new FileNotFoundException($"The file '{icon}' could not be found.");

            // Kopiere den Stream in das Zielverzeichnis
            using FileStream fileStream = new(destinationFilePath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);

            stream.Close();
            stream.Dispose();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error while copying image to directory: {ex.Message}");
        }
    }
}
