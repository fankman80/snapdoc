using bsm24.Services;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OpenXmlPowerTools;
using SkiaSharp;
using D = DocumentFormat.OpenXml.Wordprocessing;

namespace bsm24;

public partial class ExportReport
{
    public static async Task DocX(string templateDoc, string savePath)
    {
        var placeholders = new Dictionary<string, string>
        {
            {"${client_name}", GlobalJson.Data.client_name},
            {"${object_address}", GlobalJson.Data.object_address},
            {"${working_title}", GlobalJson.Data.working_title},
            {"${object_name}", GlobalJson.Data.object_name},
            {"${creation_date}", GlobalJson.Data.creation_date},
            {"${project_manager}", GlobalJson.Data.project_manager},
            {"${plan_indexes}", "${plan_indexes}"}, //bereinige splitted runs
            {"${plan_images}", "${plan_images}"} //bereinige splitted runs
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
            foreach (var placeholder in placeholders)
                if (placeholder.Value != "")
                    TextReplacer.SearchAndReplace(wordDoc, placeholder.Key, placeholder.Value, true);

            // Insert Pins in Doc-Table
            MainDocumentPart? mainPart = wordDoc.MainDocumentPart;

            if (mainPart != null)
            {
                D.Table? table = mainPart?.Document?.Body?.Elements<D.Table>().FirstOrDefault();

                if (table != null)
                {
                    int i = 1;
                    foreach (var plan in GlobalJson.Data.plans)
                    {
                        if (GlobalJson.Data.plans[plan.Key].pins != null)
                        {
                            foreach (var pin in GlobalJson.Data.plans[plan.Key].pins)
                            {
                                D.TableRow newRow = new();

                                // Cell 1
                                D.TableCell newCell1 = new(new D.Paragraph(new D.Run(new D.Text(i.ToString()))));

                                // Cell 2
                                D.TableCell newCell2 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.plans[plan.Key].name))));

                                // Cell 3
                                D.TableCell newCell3 = new();
                                if (GlobalJson.Data.plans[plan.Key].pins != null)
                                {
                                    D.Paragraph paragraph = new();
                                    if (SettingsService.Instance.IsImageExport.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        // add Pictures
                                        foreach (var img in GlobalJson.Data.plans[plan.Key].pins[pin.Key].images)
                                        {
                                            var imgName = GlobalJson.Data.plans[plan.Key].pins[pin.Key].images[img.Key].file;
                                            var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.imagePath, imgName);
                                            var overlayFile = Path.GetFileNameWithoutExtension(imgName) + ".png";
                                            var overlayDrawingPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.imageOverlayPath, overlayFile);
                                            var overlayImage = new List<(string, SKPoint, string, SKPoint)>();
                                            if (File.Exists(overlayDrawingPath))
                                                overlayImage.Add((overlayDrawingPath, new SKPoint(0, 0), "", new SKPoint(0, 0)));
                                            var _img = await XmlImage.GenerateImage(mainPart,
                                                                                    new FileResult(imgPath),
                                                                                    Double.Parse(SettingsService.Instance.ImageExportScale),
                                                                                    widthMilimeters: Int32.Parse(SettingsService.Instance.ImageExportSize),
                                                                                    imageQuality: Int32.Parse(SettingsService.Instance.ImageExportQuality),
                                                                                    overlayImages: overlayImage);
                                            paragraph.Append(new D.Run(_img));
                                        }
                                    }

                                    if (SettingsService.Instance.IsPosImageExport.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        // add Part of Plan Image
                                        var planName = GlobalJson.Data.plans[plan.Key].file;
                                        var planPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.planPath, planName);
                                        var pinPos = GlobalJson.Data.plans[plan.Key].pins[pin.Key].pos;
                                        var pinImage = GlobalJson.Data.plans[plan.Key].pins[pin.Key].pinIcon;
                                    
                                        // Pin-Icon ein/ausblenden
                                        var pinList = new List<(string, SKPoint, string, SKPoint)>();
                                        if (SettingsService.Instance.IsPinIconExport.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            pinList =
                                            [
                                                (pinImage,
                                                new SKPoint(0.5f, 0.5f),
                                                "",
                                                new SKPoint((float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].anchor.X,
                                                (float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].anchor.Y))
                                            ];
                                        }
                                        else
                                            pinList = null;
                                    
                                        var _imgPlan = await XmlImage.GenerateImage(mainPart,
                                                                                    new FileResult(planPath),
                                                                                    Double.Parse(SettingsService.Instance.PosImageExportScale),
                                                                                    new SKPoint((float)pinPos.X,
                                                                                    (float)pinPos.Y),
                                                                                    new SKSize(Int32.Parse(SettingsService.Instance.PosImageExportSize), Int32.Parse(SettingsService.Instance.PosImageExportSize)),
                                                                                    widthMilimeters: Int32.Parse(SettingsService.Instance.ImageExportSize),
                                                                                    imageQuality: Int32.Parse(SettingsService.Instance.ImageExportQuality),
                                                                                    overlayImages: pinList);
                                        paragraph.Append(new D.Run(_imgPlan));
                                    }
                                    newCell3.Append(paragraph);
                                }

                                // Cell 4
                                D.TableCell newCell4 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.plans[plan.Key].pins[pin.Key].infoTxt))));

                                // Cell 5
                                D.TableCell newCell5 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.plans[plan.Key].pins[pin.Key].pinTxt))));

                                // Füge die Zellen zur Zeile hinzu
                                newRow.Append(newCell1);
                                newRow.Append(newCell2);
                                newRow.Append(newCell3);
                                newRow.Append(newCell4);
                                newRow.Append(newCell5);
                                newRow.Append(new D.TableCell(new D.Paragraph(new D.Run(new D.Text("")))));
                                newRow.Append(new D.TableCell(new D.Paragraph(new D.Run(new D.Text("")))));
                                newRow.Append(new D.TableCell(new D.Paragraph(new D.Run(new D.Text("")))));

                                // Füge die neue Zeile der Tabelle hinzu
                                table.Append(newRow);
                                i += 1;
                            }
                        }
                    }
                }

                // Insert Plans
                if (SettingsService.Instance.IsPlanExport.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                {
                    // create Plan Index
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
                                    foreach (var plan in GlobalJson.Data.plans)
                                    {
                                        run.Append(new D.Text("- " + GlobalJson.Data.plans[plan.Key].name));
                                        run.Append(new D.Break());
                                    }
                                }
                            }
                        }
                    }

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
                                    foreach (var plan in GlobalJson.Data.plans)
                                    {
                                        var imgName = GlobalJson.Data.plans[plan.Key].file;
                                        var imgPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.planPath, imgName);

                                        // generate Pin-Image-List
                                        var pinList = new List<(string, SKPoint, string, SKPoint)>();
                                        if (GlobalJson.Data.plans[plan.Key].pins != null)
                                        {
                                            foreach (var pin in GlobalJson.Data.plans[plan.Key].pins)
                                            {
                                                pinList.Add((GlobalJson.Data.plans[plan.Key].pins[pin.Key].pinIcon,
                                                            new SKPoint((float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].pos.X, (float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].pos.Y),
                                                            "    Pos. " + i.ToString(),
                                                            new SKPoint((float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].anchor.X, (float)GlobalJson.Data.plans[plan.Key].pins[pin.Key].anchor.Y)));
                                                i += 1;
                                            }
                                        }
                                        else
                                        {
                                            pinList = null;
                                        }
                                        var _img = await XmlImage.GenerateImage(mainPart,
                                                                                new FileResult(imgPath),
                                                                                0.5,
                                                                                heightMilimeters: 140,
                                                                                imageQuality: Int32.Parse(SettingsService.Instance.ImageExportQuality),
                                                                                overlayImages: pinList);

                                        var runProperties = new D.RunProperties(); // definiere Schriftgrösse
                                        var fontSize = new D.FontSize() { Val = "32" }; // 16pt Schriftgröße
                                        runProperties.Append(fontSize);
                                        run.PrependChild(runProperties); // weise Schrift-Property zu

                                        run.Append(new D.Text(GlobalJson.Data.plans[plan.Key].name));
                                        run.Append(new D.Break());
                                        run.Append(_img);
                                        if (i > GlobalJson.Data.plans.Count) run.Append(new Break() { Type = BreakValues.Page });  // letzter Seitenumbruch nicht einfügen
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
}
