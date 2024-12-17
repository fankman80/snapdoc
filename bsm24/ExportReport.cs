using bsm24.Services;
using Codeuctivity.OpenXmlPowerTools;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using D = DocumentFormat.OpenXml.Wordprocessing;

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
                    TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (var placeholder in placeholders_lists)
                if (placeholder.Value != "")
                    TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);
            foreach (var placeholder in placeholders_table)
                if (placeholder.Value != "")
                    TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);

            MainDocumentPart? mainPart = wordDoc.MainDocumentPart;

            // suche Tabelle mit Namen "PinTable"
            string tableTitle = "Pin_Table";
            var table = mainPart?.Document?.Body?.Elements<D.Table>()
            .FirstOrDefault(t =>
            {
                // Überprüfen, ob die Tabelle Eigenschaften hat
                var tableProperties = t.GetFirstChild<D.TableProperties>();
                if (tableProperties != null)
                {
                    // Überprüfen, ob die Tabelle einen Titel (TableCaption) hat
                    var tableCaption = tableProperties.GetFirstChild<D.TableCaption>();
                    if (tableCaption != null && tableCaption.Val == tableTitle)
                        return true; // Tabelle mit gesuchtem Titel gefunden
                }
                return false;
            });

            // Insert Pins in Doc-Table
            if (mainPart != null)
            {
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
                                    var firstRow = table.Elements<D.TableRow>().FirstOrDefault();
                                    if (firstRow != null)
                                        columnCount = firstRow.Elements<D.TableCell>().Count();
                
                                    D.TableRow newRow = new();
                                    for (int column = 0; column < columnCount; column++)
                                    {
                                        var _columnPlaceholders = columnList.FindAll(item => item.Item1 == column);
                                        D.TableCell newTableCell = new();

                                        if (_columnPlaceholders.Count == 0)
                                        {
                                            // Falls keine Platzhalter vorhanden sind, füge einen leeren Paragraph hinzu
                                            D.Paragraph emptyParagraph = new();
                                            emptyParagraph.Append(new D.Run(new D.Text("")));
                                            newTableCell.Append(emptyParagraph);
                                        }
                                        else
                                        {
                                            foreach (var _placeholder in _columnPlaceholders)
                                            {
                                                D.Paragraph newParagraph = new();
                                                switch (_placeholder.Item2)
                                                {
                                                    case "${pin_nr}":
                                                        newParagraph.Append(new D.Run(new D.Text(i.ToString())));
                                                        break;

                                                    case "${pin_planName}":
                                                        newParagraph.Append(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Name)));
                                                        break;

                                                    case "${pin_posImage}":
                                                        if (SettingsService.Instance.IsPosImageExport)
                                                        {
                                                            // add Part of Plan Image
                                                            var planName = GlobalJson.Data.Plans[plan.Key].File;
                                                            var planPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, planName);
                                                            var pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                                            var pinImage = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon;

                                                            // Pin-Icon ein/ausblenden
                                                            var pinList = new List<(string, SKPoint, string, SKPoint, SKColor)>();
                                                            if (SettingsService.Instance.IsPinIconExport)
                                                            {
                                                                pinList = [(pinImage,
                                                                        new SKPoint(0.5f, 0.5f),
                                                                        "",
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

                                                            newParagraph.Append(new D.Run(_imgPlan));
                                                        }
                                                        break;

                                                    case "${pin_fotoList}":
                                                        D.Run newRun = new();
                                                        if (SettingsService.Instance.IsImageExport)
                                                        {
                                                            // add Pictures
                                                            foreach (var img in GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos)
                                                            {
                                                                if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].IsChecked)
                                                                {
                                                                    var imgName = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Fotos[img.Key].File;
                                                                    var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, imgName);
                                                                    var overlayFile = Path.GetFileNameWithoutExtension(imgName) + ".png";
                                                                    var overlayDrawingPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImageOverlayPath, overlayFile);
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
                                                        newParagraph.Append(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinName)));
                                                        break;

                                                    case "${pin_desc}":
                                                        newParagraph.Append(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinDesc)));
                                                        break;

                                                    case "${pin_location}":
                                                        newParagraph.Append(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinLocation)));
                                                        break;

                                                    default:
                                                        newParagraph.Append(new D.Run(new D.Text("")));
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
                    foreach (var paragraph in mainPart.Document.Body.Elements<D.Paragraph>())
                    {
                        var run = paragraph.Elements<D.Run>().FirstOrDefault(r => r.InnerText.Contains("${title_image"));
                        if (run != null)
                        {
                            foreach (var text in run.Elements<D.Text>())
                            {
                                if (text.Text.Contains("${title_image"))
                                {
                                    text.Text = ""; // Lösche den Platzhaltertext
                                    var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.ImagePath, GlobalJson.Data.TitleImage);
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
                        foreach (var paragraph in mainPart.Document.Body.Elements<D.Paragraph>())
                        {
                            var run = paragraph.Elements<D.Run>().FirstOrDefault(r => r.InnerText.Contains("${plan_indexes}"));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<D.Text>())
                                {
                                    if (text.Text.Contains("${plan_indexes}"))
                                    {
                                        text.Text = ""; // Lösche den Platzhaltertext
                                        foreach (var plan in GlobalJson.Data.Plans)
                                        {
                                            run.Append(new D.Text("- " + GlobalJson.Data.Plans[plan.Key].Name));
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
                        foreach (var paragraph in mainPart.Document.Body.Elements<D.Paragraph>())
                        {
                            var run = paragraph.Elements<D.Run>().FirstOrDefault(r => r.InnerText.Contains("${plan_images}"));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<D.Text>())
                                {
                                    if (text.Text.Contains("${plan_images}"))
                                    {
                                        int i = 1;
                                        text.Text = ""; // Lösche den Platzhaltertext
                                        foreach (var plan in GlobalJson.Data.Plans)
                                        {
                                            var imgName = GlobalJson.Data.Plans[plan.Key].File;
                                            var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, imgName);

                                            // generate Pin-Image-List
                                            var pinList = new List<(string, SKPoint, string, SKPoint, SKColor)>();
                                            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                            {
                                                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                                                {
                                                    if (GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].AllowExport)
                                                    {
                                                        pinList.Add((GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon,
                                                                    new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos.X, (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos.Y),
                                                                    SettingsService.Instance.PlanLabelPrefix + i.ToString(),
                                                                    new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.X, (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.Y),
                                                                    GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinColor));
                                                        i += 1;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                pinList = null;
                                            }
                                            var _img = await XmlImage.GenerateImage(mainPart,
                                                                                    new FileResult(imgPath),
                                                                                    0.5,
                                                                                    heightMilimeters: SettingsService.Instance.PlanExportSize,
                                                                                    imageQuality: SettingsService.Instance.ImageExportQuality,
                                                                                    overlayImages: pinList);

                                            var runProperties = new D.RunProperties(); // definiere Schriftgrösse
                                            var fontSize = new D.FontSize() { Val = "32" }; // 16pt Schriftgröße
                                            runProperties.Append(fontSize);
                                            run.PrependChild(runProperties); // weise Schrift-Property zu
                                            run.Append(new D.Text(GlobalJson.Data.Plans[plan.Key].Name));
                                            run.Append(new Break() { Type = BreakValues.TextWrapping });
                                            run.Append(_img);
                                            if (i < GlobalJson.Data.Plans.Count - 1) run.Append(new Break() { Type = BreakValues.Page });  // letzter Seitenumbruch nicht einfügen
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

    private static List<(int, string)> SearchTableColumns(D.Table table, Dictionary<string, string> placeholders_table)
    {
        List<(int, string)> columnList = [];

        foreach (var row in table.Elements<D.TableRow>())
        {
            int columnIndex = 0; // Spaltenzähler
            foreach (var cell in row.Elements<D.TableCell>())
            {
                foreach (var paragraph in cell.Elements<D.Paragraph>())
                {
                    foreach (var placeholder in placeholders_table)
                    {
                        if (paragraph.InnerText.Contains(placeholder.Key))
                        {
                            columnList.Add((columnIndex, placeholder.Key));

                            // Platzhalter aus dem Paragraphen entfernen
                            var run = paragraph.Elements<D.Run>().FirstOrDefault(r => r.InnerText.Contains(placeholder.Key));
                            if (run != null)
                            {
                                foreach (var text in run.Elements<D.Text>())
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
}
