using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Falak.Engines;

// Offline, text-only rendering. This intentionally does not claim layout fidelity
// for DOCX/EPUB. It never changes input files or connects to a server.
internal static class FalakSimpleExport
{
    private static IReadOnlyList<string> Pages(string source)
    {
        var lines = new List<string>();
        foreach (string raw in source.Replace("\r", "").Split('\n'))
        {
            if (raw.Length == 0) { lines.Add(""); continue; }
            for (int start = 0; start < raw.Length; start += 72)
                lines.Add(raw.Substring(start, Math.Min(72, raw.Length - start)));
        }
        if (lines.Count == 0) lines.Add(" ");
        return lines.Chunk(36).Select(chunk => string.Join("\n", chunk)).ToArray();
    }

    private static BitmapSource RenderPage(string content, int index)
    {
        const int width = 1200, height = 1600;
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var title = new FormattedText("Falak v1.2 · Text export", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 27, Brushes.DarkSlateGray, 1.0);
            ctx.DrawText(title, new Point(68, 50));
            var body = new FormattedText(content, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 22, Brushes.Black, 1.0)
            { MaxTextWidth = width - 140, MaxTextHeight = height - 180 };
            ctx.DrawText(body, new Point(68, 120));
            ctx.DrawText(new FormattedText($"Page {index}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 17, Brushes.Gray, 1.0), new Point(70, height - 55));
        }
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual);
        image.Freeze();
        return image;
    }

    private static byte[] Jpeg(BitmapSource source)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 86 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static List<string> Images(string source, string input, string extension)
    {
        var outputs = new List<string>();
        try
        {
            int page = 0;
            foreach (string content in Pages(source))
            {
                string output = OutputPaths.NextTo(input, extension, "page " + (++page).ToString("00"));
                var encoder = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                    ? (BitmapEncoder)new JpegBitmapEncoder { QualityLevel = 86 }
                    : new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(RenderPage(content,page)));
                using (var file = File.Create(output)) encoder.Save(file);
                outputs.Add(output);
            }
            return outputs;
        }
        catch
        {
            foreach (string path in outputs) { try { File.Delete(path); } catch { } }
            throw;
        }
    }

    public static void Pdf(string source, string output)
    {
        using var document = new PdfDocument();
        int pageNo = 0;
        foreach (string content in Pages(source))
        {
            byte[] picture = Jpeg(RenderPage(content, ++pageNo));
            using var ms = new MemoryStream(picture);
            using var image = XImage.FromStream(ms);
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(595); page.Height = XUnit.FromPoint(792);
            using var drawing = XGraphics.FromPdfPage(page);
            drawing.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }
        document.Save(output);
    }
}
