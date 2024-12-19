using bsm24.Models;
using bsm24.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using C = Codeuctivity.OpenXmlPowerTools;
using SkiaSharp;
using OXML = DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Vml.Office;
using DocumentFormat.OpenXml.Vml.Wordprocessing;
using DocumentFormat.OpenXml.Vml;
using DocumentFormat.OpenXml;

namespace bsm24;

public partial class ExportReport
{
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
            {"${plan_images}", "${plan_images}"},   //bereinige splitted runs  
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
        };

        // Eine Kopie der Vorlage im MemoryStream öffnen, um das Original nicht zu verändern
        using MemoryStream memoryStream = new();

        // Das Vorlagendokument in den MemoryStream kopieren
        using (Stream fileStream = await FileSystem.OpenAppPackageFileAsync(templateDoc))
        {
            fileStream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0; // Sicherstellen, dass der Stream auf den Anfang gesetzt ist

        // Das Dokument aus dem MemoryStream öffnen
        using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(memoryStream, true))
        {
            // Platzhalter durch die entsprechenden Werte ersetzen
            foreach (var placeholder in placeholders_single)
                if (placeholder.Value != "")
                    C.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (var placeholder in placeholders_lists)
                if (placeholder.Value != "")
                    C.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (var placeholder in placeholders_table)
                if (placeholder.Value != "")
                    C.TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);

            MainDocumentPart? mainPart = wordDoc.MainDocumentPart;

            // suche Tabelle mit Namen "PinTable"
            string tableTitle = "Pin_Table";
            var table = mainPart?.Document?.Body?.Elements<Table>()
            .FirstOrDefault(t =>
            {
                // Überprüfen, ob die Tabelle Eigenschaften hat
                var tableProperties = t.GetFirstChild<TableProperties>();
                if (tableProperties != null)
                {
                    // Überprüfen, ob die Tabelle einen Titel (TableCaption) hat
                    var tableCaption = tableProperties.GetFirstChild<TableCaption>();
                    if (tableCaption != null && tableCaption.Val == tableTitle)
                        return true; // Tabelle mit gesuchtem Titel gefunden
                }
                return false;
            });

            if (mainPart != null)
            {
                // Insert Pins in Doc-Table
                if (table != null)
                {
                    List<(int, string)> columnList = SearchTableColumns(table, placeholders_table); // Suche SpaltenNummern
                    int i = 1;
                    foreach (var plan in GlobalJson.Data.Plans)
                    {
                        if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                        {
                            foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            {
                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport)
                                {
                                    // Anzahl Spalten ermitteln
                                    int columnCount = 0;
                                    var firstRow = table.Elements<TableRow>().FirstOrDefault();
                                    if (firstRow != null)
                                        columnCount = firstRow.Elements<TableCell>().Count();

                                    TableRow newRow = new();
                                    for (int column = 0; column < columnCount; column++)
                                    {
                                        var _columnPlaceholders = columnList.FindAll(item => item.Item1 == column);
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
                                            foreach (var _placeholder in _columnPlaceholders)
                                            {
                                                Paragraph newParagraph = new();
                                                switch (_placeholder.Item2)
                                                {
                                                    case "${pin_nr}":
                                                        newParagraph.Append(new Run(new Text(i.ToString())));
                                                        break;

                                                    case "${pin_planName}":
                                                        newParagraph.Append(new Run(new Text(GlobalJson.Data.Plans[plan.Key].Name)));
                                                        break;

                                                    case "${pin_posImage}":
                                                        if (SettingsService.Instance.IsPosImageExport)
                                                        {
                                                            // add Part of Plan Image
                                                            var planName = GlobalJson.Data.Plans[plan.Key].File;
                                                            var planPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, planName);
                                                            var pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                            var pinImage = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon;

                                                            // Pin-Icon ein/ausblenden
                                                            var pinList = new List<(string, SKPoint, SKPoint, SKColor)>();
                                                            if (SettingsService.Instance.IsPinIconExport)
                                                            {
                                                                pinList = [(pinImage,
                                                                        new SKPoint(0.5f, 0.5f),
                                                                        new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.X,
                                                                                    (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.Y),
                                                                                    GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinColor)];
                                                            }
                                                            else
                                                                pinList = null;

                                                            var _imgPlan = await XmlImage.GenerateImage(mainPart,
                                                                                                        new FileResult(planPath),
                                                                                                        SettingsService.Instance.PosImageExportScale,
                                                                                                        new SKPoint((float)pinPos.X,
                                                                                                        (float)pinPos.Y),
                                                                                                        new SKSize(SettingsService.Instance.PinPosCropExportSize, SettingsService.Instance.PinPosCropExportSize),
                                                                                                        widthMilimeters: SettingsService.Instance.PinPosExportSize,
                                                                                                        imageQuality: SettingsService.Instance.ImageExportQuality,
                                                                                                        overlayImages: pinList);

                                                            newParagraph.Append(new Run(_imgPlan));
                                                        }
                                                        break;

                                                    case "${pin_fotoList}":
                                                        Run newRun = new();
                                                        if (SettingsService.Instance.IsImageExport)
                                                        {
                                                            // add Pictures
                                                            foreach (var img in GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos)
                                                            {
                                                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].IsChecked)
                                                                {
                                                                    var imgName = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].File;
                                                                    var imgPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, imgName);
                                                                    var overlayFile = System.IO.Path.GetFileNameWithoutExtension(imgName) + ".png";
                                                                    var overlayDrawingPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImageOverlayPath, overlayFile);
                                                                    var _img = await XmlImage.GenerateImage(mainPart,
                                                                                                            new FileResult(imgPath),
                                                                                                            SettingsService.Instance.ImageExportScale,
                                                                                                            widthMilimeters: SettingsService.Instance.ImageExportSize,
                                                                                                            imageQuality: SettingsService.Instance.ImageExportQuality);
                                                                    newRun.Append(_img);
                                                                }
                                                            }
                                                        }
                                                        newParagraph.Append(newRun);
                                                        break;

                                                    case "${pin_name}":
                                                        newParagraph.Append(new Run(new Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName)));
                                                        break;

                                                    case "${pin_desc}":
                                                        newParagraph.Append(new Run(new Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc)));
                                                        break;

                                                    case "${pin_location}":
                                                        newParagraph.Append(new Run(new Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation)));
                                                        break;

                                                    default:
                                                        newParagraph.Append(new Run(new Text("")));
                                                        break;
                                                }
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
                    foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                    {
                        var run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains("${title_image"));
                        if (run != null)
                        {
                            foreach (var text in run.Elements<Text>())
                            {
                                if (text.Text.Contains("${title_image"))
                                {
                                    text.Text = ""; // Lösche den Platzhaltertext
                                    var imgPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
                                    if (File.Exists(imgPath))
                                    {
                                        var _img = await XmlImage.GenerateImage(mainPart,
                                                                                new FileResult(imgPath),
                                                                                0.5,
                                                                                heightMilimeters: SettingsService.Instance.TitleExportSize,
                                                                                imageQuality: SettingsService.Instance.ImageExportQuality);
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
                        foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                        {
                            var run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains("${plan_indexes}"));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<Text>())
                                {
                                    if (text.Text.Contains("${plan_indexes}"))
                                    {
                                        text.Remove(); // Lösche den Platzhaltertext
                                        foreach (var plan in GlobalJson.Data.Plans)
                                        {
                                            run.Append(new Text("- " + GlobalJson.Data.Plans[plan.Key].Name));
                                            run.Append(new Break() { Type = BreakValues.TextWrapping });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Extrahiere die einzigartigen PinIcons
                    //List<string> uniquePinIcons = GetUniquePinIcons(GlobalJson.Data);

                    var cacheDir = System.IO.Path.Combine(FileSystem.AppDataDirectory, "pincache");
                    //if (!Directory.Exists(cacheDir))
                    //    Directory.CreateDirectory(cacheDir);

                    //foreach (var icon in uniquePinIcons)
                    //{
                        //File.Move(icon, Path.Combine(cacheDir, icon));
                    //}


                    if (mainPart?.Document?.Body != null)
                    {
                        // add Plan Images
                        foreach (var paragraph in mainPart.Document.Body.Elements<Paragraph>())
                        {
                            var run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains("${plan_images}"));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<Text>())
                                {
                                    if (text.Text.Contains("${plan_images}"))
                                    {
                                        int i = 1;
                                        text.Remove(); // Lösche den Platzhaltertext

                                        foreach (var plan in GlobalJson.Data.Plans)
                                        {
                                            var runProperties = new RunProperties(); // definiere Schriftgrösse
                                            var fontSize = new DocumentFormat.OpenXml.Wordprocessing.FontSize() { Val = "32" }; // 16pt Schriftgröße
                                            var imgName = GlobalJson.Data.Plans[plan.Key].File;
                                            var planImage = System.IO.Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, imgName);
                                            var planSize = GlobalJson.Data.Plans[plan.Key].ImageSize;
                                            var scaledPlanSize = ScaleToFit(planSize, new Size(250, 140));
                                            runProperties.Append(fontSize);
                                            run.PrependChild(runProperties);
                                            run.Append(new Text(GlobalJson.Data.Plans[plan.Key].Name));

                                            run.Append(GetImageElement(mainPart, planImage, scaledPlanSize, new Point(0, 0)));

                                            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                            {
                                                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                                                {
                                                    if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport)
                                                    {
                                                        string pinImage = System.IO.Path.Combine(cacheDir, GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon);
                                                        var pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                        var pinAnchor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor;
                                                        var pinSize = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Size;
                                                        var pinColor = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinColor;
                                                        var scaledPinSize = ScaleToFit(pinSize, new Size(SettingsService.Instance.PinSize, SettingsService.Instance.PinSize));
                                                        var posOnPlan = new Point((pinPos.X * scaledPlanSize.Width) - (pinAnchor.X * scaledPinSize.Width),
                                                                                  (pinPos.Y * scaledPlabSize.Height) - (pinAnchor.Y * scaledPinSize.Height));

                                                        
                                                        run.Append(GetImageElement(mainPart, pinImage, new Size(scaledPinSize.Width, scaledPinSize.Height), posOnPlan));

                                                        run.Append(CreateTextBoxWithShape(SettingsService.Instance.PlanLabelPrefix + i.ToString(),
                                                                                          new Point(posOnPlan.X + (pinSize.Width / scaledPinSize.Width), posOnPlan.Y - pinSize.Height / scaledPinSize.Height),
                                                                                          SettingsService.Instance.PlanLabelFontSize,
                                                                                          pinColor.ToString()[3..]));
                                                        i += 1;
                                                    }
                                                }
                                            }

                                            // Erstelle einen neuen Paragraph und Run für den nächsten Plan auf der neuen Seite
                                            var pageBreakParagraph = new Paragraph(new Run(new Break() { Type = BreakValues.Page }));
                                            mainPart.Document.Body.AppendChild(pageBreakParagraph);

                                            // Füge einen neuen Paragraph und Run für den nächsten Plan hinzu
                                            var nextPlanParagraph = new Paragraph();
                                            var nextPlanRun = new Run();
                                            mainPart.Document.Body.AppendChild(nextPlanParagraph);
                                            nextPlanParagraph.Append(nextPlanRun);

                                            // Setze den `run` auf den neu erstellten `nextPlanRun`, um den nächsten Plan einzufügen
                                            run = nextPlanRun;
                                        }
                                    }
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
    }

    private static Drawing GetImageElement(MainDocumentPart mainPart, string imgPath, Size size, Point pos)
    {
        ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
        using (FileStream stream = new(imgPath, FileMode.Open))
        {
            imagePart.FeedData(stream);
        }
        Drawing element = GetAnchorPicture(mainPart.GetIdOfPart(imagePart), size, pos);

        return element;
    }

    private static Drawing GetAnchorPicture(String imagePartId, Size size, Point pos, string name="")
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

        DW.WrapTight _wp = new()
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
            Name = name
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

    public static long MillimetersToEMU(double millimeters)
    {
        return Convert.ToInt64(millimeters * 36000);
    }

    public static List<string> GetUniquePinIcons(JsonDataModel jsonDataModel)
    {
        // HashSet für einzigartige PinIcons
        HashSet<string> uniquePinIcons = [];

        // Durchlaufe alle Plans im JsonDataModel
        foreach (var plan in jsonDataModel.Plans.Values)
        {
            // Durchlaufe alle Pins im Plan
            foreach (var pin in plan.Pins.Values)
            {
                if (!string.IsNullOrEmpty(pin.PinIcon))
                    uniquePinIcons.Add(pin.PinIcon);
            }
        }

        // Rückgabe der eindeutigen PinIcons als Liste
        return [.. uniquePinIcons];
    }

    private static List<(int, string)> SearchTableColumns(Table table, Dictionary<string, string> placeholders_table)
    {
        List<(int, string)> columnList = [];

        foreach (var row in table.Elements<TableRow>())
        {
            int columnIndex = 0; // Spaltenzähler
            foreach (var cell in row.Elements<TableCell>())
            {
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    foreach (var placeholder in placeholders_table)
                    {
                        if (paragraph.InnerText.Contains(placeholder.Key))
                        {
                            columnList.Add((columnIndex, placeholder.Key));

                            // Platzhalter aus dem Paragraphen entfernen
                            var run = paragraph.Elements<Run>().FirstOrDefault(r => r.InnerText.Contains(placeholder.Key));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<Text>())
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
        return columnList;
    }

    private static Picture CreateTextBoxWithShape(string text, Point coordinateMM, double fontSizePt, string fontColorHex)
    {
        double xCoordinatePt = coordinateMM.X * 3.7795; // * 2.83465  f=1.333333 ?;
        double yCoordinatePt = coordinateMM.Y * 3.7795; // * 2.83465  f=1.333333 ?;
        double textWidthPt = GetTextWidthInPoints(text, "Arial", fontSizePt, 96) * 2;

        // Erstelle die Shape mit Positionierung
        Picture picture1 = new();

        Shape shape1 = new()
        {
            Id = "TextBoxShape",
            Style = $"position:absolute;margin-left:{xCoordinatePt}pt;margin-top:{yCoordinatePt}pt;width:{textWidthPt}pt;mso-fit-shape-to-text:t;mso-wrap-style:square;",
        };

        Fill fill1 = new() { Color = "#FFFFFF" };
        Stroke stroke1 = new() { Color = fontColorHex, Weight = "1pt" };

        // Erstelle die TextBox
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
        float textWidthInPixels = font.MeasureText(text);
        double textWidthInPoints = textWidthInPixels * 72 / dpi;
        return textWidthInPoints;
    }

private Size ScaleToFit(Size originalSize, Size maxTargetSize)
{
    // Berechne die Skalierungsfaktoren für die Breite und Höhe
    double widthScale = (double)maxTargetWidth / originalWidth;
    double heightScale = (double)maxTargetHeight / originalHeight;

    // Wähle den kleineren Skalierungsfaktor, um sicherzustellen, dass das Bild nicht größer als die Zielabmessungen wird
    double scale = Math.Min(widthScale, heightScale);

    // Berechne die neuen Abmessungen
    int newWidth = (int)(originalWidth * scale);
    int newHeight = (int)(originalHeight * scale);

    // Rückgabe der skalierten Abmessungen
    return (newWidth, newHeight);
}
    
    
}
