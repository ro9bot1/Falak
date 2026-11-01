using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Falak.Engines;

// Offline PDF->PPTX: for white pages, erase the ORIGINAL rasterized glyphs
// from the slide's bitmap background *before* adding editable text boxes.
// This prevents ghost/duplicate words when users move a PowerPoint text box.
// Other pages remain fidelity-first raster images instead of overlapping text.
// Scanned PDFs without an extractable text layer also remain image-only.
internal static class PdfSlidesEngine
{
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Pres = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";

    public static bool Supports(string id, IReadOnlyList<string> inputs) =>
        id == "convert.pptx" && inputs.Count > 0 &&
        inputs.All(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase));

    public static async Task<JobResult> Run(JobRequest req, IProgress<double>? progress, CancellationToken ct)
    {
        var outputs = new List<string>();
        int complete = 0;
        foreach (string input in req.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            string folder = Path.Combine(Path.GetTempPath(), "FalakPdfSlides_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                // PdfEngine creates images beside its input; use a PRIVATE copy in a temp folder.
                string copy = Path.Combine(folder, "source.pdf");
                File.Copy(input, copy);
                JobResult images = await PdfEngine.ToImages(new[] { copy }, ".png", null, ct);
                // ToImages returns pages in numeric page order; lexicographic sorting corrupts 10+ page PDFs.
                var pages = images.Outputs.Where(File.Exists).ToList();
                if (pages.Count == 0) throw new UserFacingException("PDF rasterizer produced no pages.");
                var editableWords = new List<EditablePage>();
                using (var document = UglyToad.PdfPig.PdfDocument.Open(input))
                {
                    foreach (var page in document.GetPages())
                    {
                        var layer = new EditablePage(page.Width,page.Height);
                        foreach (var word in page.GetWords())
                        {
                            if (word.Text.Length > 0 && word.BoundingBox.Width > 0 && word.BoundingBox.Height > 0)
                                layer.Words.Add(new EditableWord(word.Text, word.BoundingBox.Left, word.BoundingBox.Bottom,
                                    word.BoundingBox.Width, word.BoundingBox.Height,
                                    (word.FontName ?? "").Contains("Bold", StringComparison.OrdinalIgnoreCase),
                                    (word.FontName ?? "").Contains("Italic", StringComparison.OrdinalIgnoreCase) ||
                                    (word.FontName ?? "").Contains("Oblique", StringComparison.OrdinalIgnoreCase)));
                            if (layer.Words.Count == 4000) break; // page performance guard
                        }
                        editableWords.Add(layer);
                    }
                }
                string output = OutputPaths.NextTo(input, ".pptx");
                try
                {
                    OutputPaths.WriteAtomically(output, path => Build(path, pages, editableWords, ct));
                    // Validate after closing the ZIP and reject incomplete results.
                    using (var zip = ZipFile.OpenRead(output))
                        if (zip.GetEntry("ppt/presentation.xml") == null ||
                            zip.Entries.Count(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)) != pages.Count)
                            throw new UserFacingException("Generated PPTX failed slide-count validation.");
                    outputs.Add(output);
                }
                catch { if (File.Exists(output)) File.Delete(output); throw; }
            }
            finally
            {
                try { Directory.Delete(folder, true); } catch (IOException) { }
            }
            progress?.Report((double)++complete / req.Inputs.Count);
        }
        return new JobResult(outputs, null, "PDF converted: white-page background text erased and replaced by editable lines to avoid duplicate ghost text. Complex/colored or scanned pages may remain image-only; fonts and complex layout can vary.");
    }

    private sealed record EditableWord(string Text,double X,double Bottom,double Width,double Height, bool Bold, bool Italic);
    private sealed class EditablePage(double width,double height)
    {
        public double Width { get; } = width;
        public double Height { get; } = height;
        public List<EditableWord> Words { get; } = new();
    }

    private static void Build(string destination, IReadOnlyList<string> images, IReadOnlyList<EditablePage> words, CancellationToken ct)
    {
        using Stream template = typeof(PdfSlidesEngine).Assembly.GetManifestResourceStream("Falak.PdfSlideTemplate.pptx")
            ?? throw new UserFacingException("The local PDF-to-PPTX template is missing.");
        using var src = new ZipArchive(template, ZipArchiveMode.Read, leaveOpen: false);
        using var dst = ZipFile.Open(destination, ZipArchiveMode.Create);
        byte[] slideXml = Read(src, "ppt/slides/slide1.xml");
        string slideRel = System.Text.Encoding.UTF8.GetString(Read(src, "ppt/slides/_rels/slide1.xml.rels"));

        foreach (var entry in src.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/')) continue;
            byte[] bytes;
            switch (entry.FullName)
            {
                case "ppt/slides/slide1.xml": bytes = SlideXml(slideXml, images[0], words.Count>0?words[0]:null); break;
                case "ppt/media/image1.png": bytes = CleanBackground(images[0], words.Count > 0 ? words[0] : null); break;
                case "ppt/presentation.xml":
                {
                    var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(Read(src, entry.FullName)));
                    var ids = doc.Root!.Element(Pres + "sldIdLst") ?? throw new InvalidDataException("Template missing slide IDs");
                    for (int i = 2; i <= images.Count; i++)
                        ids.Add(new XElement(Pres + "sldId", new XAttribute("id", 255 + i), new XAttribute(OfficeRel + "id", "rId" + (6 + i))));
                    bytes = XmlBytes(doc); break;
                }
                case "ppt/_rels/presentation.xml.rels":
                {
                    var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(Read(src, entry.FullName)));
                    for (int i = 2; i <= images.Count; i++)
                        doc.Root!.Add(new XElement(PkgRel + "Relationship",
                            new XAttribute("Id", "rId" + (6 + i)),
                            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide"),
                            new XAttribute("Target", "slides/slide" + i + ".xml")));
                    bytes = XmlBytes(doc); break;
                }
                case "[Content_Types].xml":
                {
                    var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(Read(src, entry.FullName)));
                    for (int i = 2; i <= images.Count; i++)
                        doc.Root!.Add(new XElement(Types + "Override",
                            new XAttribute("PartName", "/ppt/slides/slide" + i + ".xml"),
                            new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.presentationml.slide+xml")));
                    bytes = XmlBytes(doc); break;
                }
                default: bytes = Read(src, entry.FullName); break;
            }
            Write(dst, entry.FullName, bytes);
        }
        for (int i = 2; i <= images.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            Write(dst, "ppt/slides/slide" + i + ".xml", SlideXml(slideXml, images[i-1], i-1<words.Count?words[i-1]:null));
            Write(dst, "ppt/slides/_rels/slide" + i + ".xml.rels",
                System.Text.Encoding.UTF8.GetBytes(slideRel.Replace("../media/image1.png", "../media/image" + i + ".png", StringComparison.Ordinal)));
            Write(dst, "ppt/media/image" + i + ".png", CleanBackground(images[i-1], i-1 < words.Count ? words[i-1] : null));
        }
    }

    // Preserve each PDF page's aspect ratio inside the existing portrait template.
    // PPTX points are 1/72 inch; 12700 EMU = 1 point.
    private sealed record EditableLine(string Text, double X, double Bottom, double Width, double Height, bool Rtl, bool Bold, bool Italic);

    private sealed class LineGroup
    {
        public readonly List<EditableWord> Words = new();
        public double Center;
        public double Height;
        public void Add(EditableWord w)
        {
            Words.Add(w);
            // Robust line center: avoid shifting the line when a tall glyph/superscript appears.
            var center = w.Bottom + w.Height / 2;
            Center = Words.Count == 1 ? center : Center + (center - Center) / Words.Count;
            Height = Math.Max(Height, w.Height);
        }
    }

    // PDFPig GetWords gives independent word rectangles, not layout paragraphs.
    // Group words by baseline and split distant columns before adding spaces.
    private static List<EditableLine> ReconstructLines(EditablePage page)
    {
        var groups = new List<LineGroup>();
        foreach (var word in page.Words.OrderByDescending(w => w.Bottom + w.Height / 2).ThenBy(w => w.X))
        {
            double mid = word.Bottom + word.Height / 2;
            LineGroup? best = null;
            double distance = double.MaxValue;
            foreach (var group in groups)
            {
                var d = Math.Abs(group.Center - mid);
                if (d <= Math.Max(1.5, Math.Min(group.Height, word.Height) * 0.31) && d < distance)
                {
                    best = group;
                    distance = d;
                }
            }
            if (best == null) { best = new LineGroup(); groups.Add(best); }
            best.Add(word);
        }

        var result = new List<EditableLine>();
        foreach (var group in groups.OrderByDescending(g => g.Center))
        {
            var ordered = group.Words.OrderBy(w => w.X).ToArray();
            if (ordered.Length == 0) continue;
            double usualHeight = ordered.Select(w => w.Height).OrderBy(h => h).ElementAt(ordered.Length / 2);
            // Keep two-column material and side annotations out of the same text box.
            double maxGap = Math.Max(26, usualHeight * 3.6);
            var segment = new List<EditableWord>();
            double right = double.NegativeInfinity;
            foreach (var word in ordered)
            {
                if (segment.Count > 0 && word.X - right > maxGap)
                {
                    AddSegment(segment);
                    segment = new List<EditableWord>();
                }
                segment.Add(word);
                right = Math.Max(right, word.X + word.Width);
            }
            AddSegment(segment);
        }
        return result;

        void AddSegment(List<EditableWord> segment)
        {
            if (segment.Count == 0) return;
            double x = segment.Min(w => w.X);
            double far = segment.Max(w => w.X + w.Width);
            double bottom = segment.Min(w => w.Bottom);
            double top = segment.Max(w => w.Bottom + w.Height);
            bool rtl = segment.Sum(w => w.Text.Count(c => c >= '\u0600' && c <= '\u08FF')) >
                       segment.Sum(w => w.Text.Count(c => c >= 'A' && c <= 'z'));
            IEnumerable<EditableWord> readingOrder = rtl ? segment.OrderByDescending(w => w.X) : segment;
            string contents = string.Join(" ", readingOrder.Select(w => w.Text.Trim()).Where(t => t.Length > 0));
            if (contents.Length == 0) return;
            result.Add(new EditableLine(contents, x, bottom, Math.Max(1, far - x), Math.Max(1, top - bottom), rtl,
                segment.Count(w => w.Bold) > segment.Count / 2,
                segment.Count(w => w.Italic) > segment.Count / 2));
        }
    }

    private static byte[] SlideXml(byte[] original, string pngFile, EditablePage? layer)
    {
        byte[] png = File.ReadAllBytes(pngFile);
        if (png.Length < 24) throw new InvalidDataException("PNG page was truncated.");
        uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
        uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        if (width == 0 || height == 0) throw new InvalidDataException("Invalid PDF page dimensions.");
        const long slideW = 6858000, slideH = 9144000;
        double ratio = Math.Min((double)slideW / width, (double)slideH / height);
        long scaledW = (long)Math.Round(width * ratio), scaledH = (long)Math.Round(height * ratio);
        var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(original));
        var picture = doc.Descendants(Pres + "pic").FirstOrDefault()
            ?? throw new InvalidDataException("Template contains no slide picture.");
        var xfrm = picture.Descendants(Drawing + "xfrm").FirstOrDefault()
            ?? throw new InvalidDataException("Template missing picture transform.");
        var off = xfrm.Element(Drawing + "off")!;
        var ext = xfrm.Element(Drawing + "ext")!;
        off.SetAttributeValue("x", (slideW - scaledW) / 2);
        off.SetAttributeValue("y", (slideH - scaledH) / 2);
        ext.SetAttributeValue("cx", scaledW);
        ext.SetAttributeValue("cy", scaledH);

        if (layer != null && layer.Width > 0 && layer.Height > 0 && HasWhitePageBackground(pngFile))
        {
            var tree = doc.Descendants(Pres + "spTree").FirstOrDefault();
            if (tree != null)
            {
                double xScale = (double)scaledW / layer.Width;
                double yScale = (double)scaledH / layer.Height;
                double xOffset = (slideW - scaledW) / 2d;
                double yOffset = (slideH - scaledH) / 2d;
                // Background glyphs have already been erased in CleanBackground.
                // Transparent editable text boxes do not mask table lines or figures.
                int id = 100;
                foreach (var line in ReconstructLines(layer))
                {
                    // Slight breathing room for font ascenders, descenders and editing.
                    double pad = Math.Max(1.2, line.Height * 0.18);
                    long x = (long)Math.Round(xOffset + (line.X - pad) * xScale);
                    long y = (long)Math.Round(yOffset + (layer.Height - line.Bottom - line.Height - pad) * yScale);
                    long w = (long)Math.Round((line.Width + pad * 2) * xScale);
                    long h = (long)Math.Round((line.Height + 2 * pad) * yScale);
                    x = Math.Max(0, x);
                    y = Math.Max(0, y);
                    if (x >= slideW || y >= slideH) continue;
                    w = Math.Min(Math.Max(w, 30000), slideW - x);
                    h = Math.Min(Math.Max(h, 30000), slideH - y);

                    // Glyph boxes are typically shorter than font em-height. Use the
                    // text's actual PDF height, not an arbitrary per-word font size.
                    double glyphHeightPoints = line.Height * yScale / 12700d;
                    double fontPt = Math.Clamp(glyphHeightPoints * 1.32, 7.0, 54.0);
                    // Constrain long lines when a PDF has spacing/justification.
                    double estimatedEm = ApproximateTextWidthEm(line.Text);
                    double boxPt = w / 12700d;
                    if (estimatedEm > 0) fontPt = Math.Min(fontPt, boxPt / estimatedEm * 1.03);
                    int fontSize = (int)Math.Clamp(Math.Round(fontPt * 100), 600, 5400);

                    var fill = new XElement(Drawing + "noFill");
                    var shape = new XElement(Pres + "sp",
                        new XElement(Pres + "nvSpPr",
                            new XElement(Pres + "cNvPr", new XAttribute("id", id++), new XAttribute("name", "Editable PDF line")),
                            new XElement(Pres + "cNvSpPr", new XAttribute("txBox", "1")), new XElement(Pres + "nvPr")),
                        new XElement(Pres + "spPr",
                            new XElement(Drawing + "xfrm",
                                new XElement(Drawing + "off", new XAttribute("x", x), new XAttribute("y", y)),
                                new XElement(Drawing + "ext", new XAttribute("cx", w), new XAttribute("cy", h))),
                            new XElement(Drawing + "prstGeom", new XAttribute("prst", "rect"), new XElement(Drawing + "avLst")),
                            fill,
                            new XElement(Drawing + "ln", new XElement(Drawing + "noFill"))),
                        new XElement(Pres + "txBody",
                            new XElement(Drawing + "bodyPr", new XAttribute("wrap", "none"),
                                new XAttribute("anchor", "t"), new XAttribute("lIns", 0),
                                new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0),
                                new XElement(Drawing + "normAutofit")),
                            new XElement(Drawing + "lstStyle"),
                            new XElement(Drawing + "p",
                                new XElement(Drawing + "pPr", new XAttribute("algn", line.Rtl ? "r" : "l"),
                                    new XAttribute("rtl", line.Rtl ? "1" : "0")),
                                new XElement(Drawing + "r",
                                    new XElement(Drawing + "rPr", new XAttribute("lang", line.Rtl ? "ar-SA" : "en-US"),
                                        new XAttribute("sz", fontSize),
                                        new XAttribute("b", line.Bold ? "1" : "0"),
                                        new XAttribute("i", line.Italic ? "1" : "0"),
                                        new XElement(Drawing + "solidFill", new XElement(Drawing + "srgbClr", new XAttribute("val", "161616"))),
                                        new XElement(Drawing + "latin", new XAttribute("typeface", "Arial"))),
                                    new XElement(Drawing + "t", line.Text)),
                                new XElement(Drawing + "endParaRPr", new XAttribute("lang", line.Rtl ? "ar-SA" : "en-US")))));
                    tree.Add(shape);
                }
            }
        }
        return XmlBytes(doc);
    }

    // Conservative approximation for single-line text; PowerPoint text remains editable.
    // Font advance varies, so estimate is only used to prevent overflowing lines.
    private static double ApproximateTextWidthEm(string value)
    {
        double width = 0;
        foreach (char ch in value)
        {
            width += char.IsWhiteSpace(ch) ? 0.30 :
                "ilIjt.,:;!|()[]'`".Contains(ch) ? 0.26 :
                "MW@%#QO".Contains(ch) ? 0.79 :
                ch >= '\u0600' && ch <= '\u08FF' ? 0.56 : 0.49;
        }
        return Math.Max(0.5, width);
    }

    // Clear full editable TEXT LINES from the original raster layer. Earlier versions
    // erased tight per-word bounding boxes, which left apostrophes, punctuation,
    // antialiasing fringes and duplicate glyphs outside the boxes. We use the union
    // of the complete line plus generous ascender / descender overscan.
    // The source PDF and other source files are never modified.
    private static byte[] CleanBackground(string imagePath, EditablePage? page)
    {
        if (page is null || page.Words.Count == 0 || page.Width <= 0 || page.Height <= 0 ||
            !HasWhitePageBackground(imagePath)) return File.ReadAllBytes(imagePath);
        using var input = File.OpenRead(imagePath);
        var frame = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
        int stride = checked(width * 4);
        var pixels = new byte[checked(stride * height)];
        bitmap.CopyPixels(pixels, stride, 0);
        var sourcePixels = (byte[])pixels.Clone();
        double sx = width / page.Width, sy = height / page.Height;
        var areas = new List<(int X0, int Y0, int X1, int Y1)>();
        foreach (var line in ReconstructLines(page))
        {
            // Overscan is proportional to text height. It also catches rotated
            // punctuation and font metrics that lie outside a PDF word rectangle.
            double overX = Math.Max(3.0, line.Height * 0.35) * sx;
            double overY = Math.Max(2.5, line.Height * 0.60) * sy;
            int x0 = Math.Clamp((int)Math.Floor(line.X * sx - overX), 0, width);
            int x1 = Math.Clamp((int)Math.Ceiling((line.X + line.Width) * sx + overX), 0, width);
            int y0 = Math.Clamp((int)Math.Floor((page.Height - line.Bottom - line.Height) * sy - overY), 0, height);
            int y1 = Math.Clamp((int)Math.Ceiling((page.Height - line.Bottom) * sy + overY), 0, height);
            if (x1 > x0 && y1 > y0) areas.Add((x0, y0, x1, y1));
        }
        foreach (var area in areas)
            for (int y = area.Y0; y < area.Y1; y++)
            {
                int begin = y * stride + area.X0 * 4;
                int end = y * stride + area.X1 * 4;
                for (int i = begin; i < end; i += 4)
                {
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 255;
                }
            }
        // Preserve drawn long, straight borders and form answer lines when they
        // pass through a text-mask area. Text glyph runs do not meet the minimum
        // long horizontal / vertical geometry thresholds below.
        RestoreLongRules(sourcePixels, pixels, width, height, stride);
        var cleaned = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(cleaned));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static void RestoreLongRules(byte[] original, byte[] cleaned, int width, int height, int stride)
    {
        static bool Dark(byte[] buffer, int index) =>
            buffer[index] < 95 && buffer[index + 1] < 95 && buffer[index + 2] < 95;
        // A long stroke is black for many adjacent pixels. This avoids restoring
        // arbitrary glyph dots and punctuation into the background.
        int minHorizontal = Math.Max(100, width / 5);
        for (int y = 0; y < height; y++)
        {
            int x = 0;
            while (x < width)
            {
                int begin = x;
                while (x < width && Dark(original, y * stride + x * 4)) x++;
                if (x - begin >= minHorizontal)
                    for (int k = begin; k < x; k++)
                    {
                        int i = y * stride + k * 4;
                        cleaned[i] = original[i]; cleaned[i + 1] = original[i + 1];
                        cleaned[i + 2] = original[i + 2]; cleaned[i + 3] = original[i + 3];
                    }
                if (begin == x) x++;
            }
        }
        int minVertical = Math.Max(100, height / 7);
        for (int x = 0; x < width; x++)
        {
            int y = 0;
            while (y < height)
            {
                int begin = y;
                while (y < height && Dark(original, y * stride + x * 4)) y++;
                if (y - begin >= minVertical)
                    for (int k = begin; k < y; k++)
                    {
                        int i = k * stride + x * 4;
                        cleaned[i] = original[i]; cleaned[i + 1] = original[i + 1];
                        cleaned[i + 2] = original[i + 2]; cleaned[i + 3] = original[i + 3];
                    }
                if (begin == y) y++;
            }
        }
    }

    // A white overlay is appropriate only for predominantly white PDF pages.
    // Detect from the rendered page, not from assumptions about the PDF template.
    private static bool HasWhitePageBackground(string imagePath)
    {
        try
        {
            using var input = File.OpenRead(imagePath);
            var frame = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int w = bitmap.PixelWidth, h = bitmap.PixelHeight;
            if (w < 4 || h < 4) return false;
            var points = new (int X, int Y)[]
            {
                ((int)(w*0.02), (int)(h*0.02)), ((int)(w*0.98), (int)(h*0.02)),
                ((int)(w*0.02), (int)(h*0.98)), ((int)(w*0.98), (int)(h*0.98))
            };
            int white = 0;
            var buffer = new byte[4];
            foreach (var point in points)
            {
                int x = Math.Clamp(point.X, 0, w - 1), y = Math.Clamp(point.Y, 0, h - 1);
                bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
                if (buffer[0] > 247 && buffer[1] > 247 && buffer[2] > 247) white++;
            }
            return white >= 3;
        }
        catch (Exception)
        {
            // Color sampling is optional; never fail the entire conversion on image decoding.
            return false;
        }
    }

    private static byte[] XmlBytes(XDocument doc)
    {
        using var stream = new MemoryStream();
        doc.Save(stream, SaveOptions.DisableFormatting);
        return stream.ToArray();
    }
    private static byte[] Read(ZipArchive archive, string name)
    {
        using var input = archive.GetEntry(name)?.Open() ?? throw new InvalidDataException("Missing template part: " + name);
        using var buffer = new MemoryStream(); input.CopyTo(buffer); return buffer.ToArray();
    }
    private static void Write(ZipArchive archive, string name, byte[] bytes)
    {
        using var output = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        output.Write(bytes, 0, bytes.Length);
    }
}
