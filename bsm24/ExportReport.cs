using bsm24.Services;
using Codeuctivity.OpenXmlPowerTools;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkiaSharp;
using System.Globalization;
using D = DocumentFormat.OpenXml.Wordprocessing;

namespace bsm24;

public partial class ExportReport
{
    public static async Task DocX(string templateDoc, string savePath)
    {
        var placeholders = new Dictionary<string, string>
        {
            {"${client_name}", GlobalJson.Data.Client_name},
            {"${object_address}", GlobalJson.Data.Object_address},
            {"${working_title}", GlobalJson.Data.Working_title},
            {"${object_name}", GlobalJson.Data.Object_name},
            {"${creation_date}", GlobalJson.Data.Creation_date.Date.ToString("D")},
            {"${project_manager}", GlobalJson.Data.Project_manager},
            {"${plan_indexes}", "${plan_indexes}"}, //bereinige splitted runs
            {"${plan_images}", "${plan_images}"}, //bereinige splitted runs
            {"${title_image}", "${title_image}"} //bereinige splitted runs
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
                    foreach (var plan in GlobalJson.Data.Plans)
                    {
                        if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                        {
                            foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                            {
                                D.TableRow newRow = new();

                                // Cell 1
                                D.TableCell newCell1 = new(new D.Paragraph(new D.Run(new D.Text(i.ToString()))));

                                // Cell 2
                                D.TableCell newCell2 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Name))));

                                // Cell 3
                                D.TableCell newCell3 = new();
                                if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                {
                                    D.Paragraph paragraph = new();
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
                                                paragraph.Append(new D.Run(_img));
                                            }
                                        }
                                    }

                                    if (SettingsService.Instance.IsPosImageExport)
                                    {
                                        // add Part of Plan Image
                                        var planName = GlobalJson.Data.Plans[plan.Key].File;
                                        var planPath = Path.Combine(FileSystem.AppDataDirectory, GlobalJson.Data.PlanPath, planName);
                                        var pinPos = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos;
                                        var pinImage = GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon;

                                        // Pin-Icon ein/ausblenden
                                        var pinList = new List<(string, SKPoint, string, SKPoint)>();
                                        if (SettingsService.Instance.IsPinIconExport)
                                        {
                                            pinList =
                                            [
                                                (pinImage,
                                                new SKPoint(0.5f, 0.5f),
                                                "",
                                                new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.X,
                                                (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.Y))
                                            ];
                                        }
                                        else
                                        {
                                            pinList = null;
                                        }

                                        var _imgPlan = await XmlImage.GenerateImage(mainPart,
                                                                                    new FileResult(planPath),
                                                                                    SettingsService.Instance.PosImageExportScale,
                                                                                    new SKPoint((float)pinPos.X,
                                                                                    (float)pinPos.Y),
                                                                                    new SKSize(SettingsService.Instance.PosImageExportSize, SettingsService.Instance.PosImageExportSize),
                                                                                    widthMilimeters: SettingsService.Instance.ImageExportSize,
                                                                                    imageQuality: SettingsService.Instance.ImageExportQuality,
                                                                                    overlayImages: pinList);
                                        paragraph.Append(new D.Run(_imgPlan));
                                    }
                                    newCell3.Append(paragraph);
                                }

                                // Cell 4
                                D.TableCell newCell4 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].InfoTxt))));

                                // Cell 5
                                D.TableCell newCell5 = new(new D.Paragraph(new D.Run(new D.Text(GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinTxt))));

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
                                            var pinList = new List<(string, SKPoint, string, SKPoint)>();
                                            if (GlobalJson.Data.Plans[plan.Key].Pins != null)
                                            {
                                                foreach (var pin in GlobalJson.Data.Plans[plan.Key].Pins)
                                                {
                                                    pinList.Add((GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].PinIcon,
                                                                new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos.X, (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Pos.Y),
                                                                "    Pos. " + i.ToString(),
                                                                new SKPoint((float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.X, (float)GlobalJson.Data.Plans[plan.Key].Pins[pin.Key].Anchor.Y)));
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
}
