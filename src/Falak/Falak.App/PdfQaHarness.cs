using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Linq;
using Falak.Engines;

namespace Falak.App;

// Executes Falak's actual PDF->PPTX engine on a COPY of a supplied fixture.
// Package validity/editable text is automated; visual acceptance remains manual.
internal static class PdfQaHarness
{
    public static void Run(string fixturePdf, string reportPath)
    {
        var window = new Window { Title = "Falak PDF->PPTX QA", Width = 480, Height = 170,
            Content = new TextBlock { Text = "Converting a private test copy of the PDF and checking the resulting PPTX...",
                Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap } };
        Application.Current.MainWindow = window;
        window.Show();
        window.Dispatcher.BeginInvoke(new Action(async () =>
        {
            var notes = new List<string>();
            string? folder = null;
            bool pass = false;
            try
            {
                if (!File.Exists(fixturePdf)) throw new FileNotFoundException("Test PDF not found", fixturePdf);
                folder = Path.Combine(Path.GetTempPath(), "FalakPdfQA_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(folder);
                var copy = Path.Combine(folder, "input.pdf");
                File.Copy(fixturePdf, copy);
                var outputs = await new FileEngines().RunAsync(new JobRequest("convert.pptx", new [] {copy}), null, CancellationToken.None);
                var pptx = outputs.Outputs.Single();
                using var zip = ZipFile.OpenRead(pptx);
                var slides = zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) &&
                    e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToArray();
                XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                int editableShapes = 0;
                int characters = 0;
                foreach (var slide in slides)
                {
                    using var stream = slide.Open();
                    var xml = XDocument.Load(stream);
                    foreach (var shape in xml.Descendants(p + "sp"))
                    {
                        var text = string.Concat(shape.Descendants(a + "t").Select(t => t.Value));
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        editableShapes++;
                        characters += text.Length;
                    }
                }
                notes.Add($"Generated slides: {slides.Length}");
                notes.Add($"Editable text shapes: {editableShapes}");
                notes.Add($"Editable text characters: {characters}");
                pass = slides.Length > 0 && editableShapes > 0 && characters > 10;
                var outputPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
                    "Falak_PDF_QA_Output.pptx");
                File.Copy(pptx, outputPath, true);
                notes.Add("Candidate PPTX for manual visual review: " + outputPath);
                notes.Add(pass ? "PASS: package structure and native text objects" : "FAIL: missing native editable text");
                notes.Add("NOT VERIFIED: text placement and visual match to source (inspect in PowerPoint).");
            }
            catch (Exception e)
            {
                notes.Add("FAIL: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (folder != null) try { Directory.Delete(folder, true); } catch { }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                File.WriteAllLines(reportPath, notes);
                MessageBox.Show(string.Join("\n", notes), "Falak PDF QA", MessageBoxButton.OK,
                    pass ? MessageBoxImage.Information : MessageBoxImage.Error);
                Application.Current.Shutdown(pass ? 0 : 1);
            }
        }), DispatcherPriority.Background);
    }
}
