using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Falak.Engines;

// Text-based exports are explicitly marked; they do not reproduce PDF layout.
internal static class PdfExtraEngine
{
    public static bool Supports(string action) => action is "convert.md" or "convert.docx" or "convert.xlsx" or "convert.epub" or "tool.pdf-metadata" or "tool.pdf-organize";
    public static JobResult Run(JobRequest job, IProgress<double>? progress, CancellationToken ct)
    {
        var outputs=new List<string>();
        int n=0;
        foreach(var input in job.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            string id=job.ActionId;
            string target=id.StartsWith("convert.", StringComparison.Ordinal)?"."+id[8..]:".pdf";
            string output=OutputPaths.NextTo(input,target,id=="tool.pdf-organize"?"organized":null);
            if(id=="tool.pdf-metadata")
            {
                output=OutputPaths.NextTo(input,".json","metadata");
                using var pdf=PdfSharp.Pdf.IO.PdfReader.Open(input,PdfDocumentOpenMode.Import);
                var info=pdf.Info;
                var json=JsonSerializer.Serialize(new {info.Title,info.Subject,info.Author,info.Creator,info.Keywords, PageCount=pdf.PageCount},new JsonSerializerOptions{WriteIndented=true});
                File.WriteAllText(output,json,Encoding.UTF8);
            }
            else if(id=="tool.pdf-organize")
            {
                using var pdf=PdfSharp.Pdf.IO.PdfReader.Open(input,PdfDocumentOpenMode.Import);
                string? spec=job.Options;
                if (string.IsNullOrWhiteSpace(spec)) throw new UserFacingException("Select PDF pages (e.g. 3,1,2) or reverse.");
                int[] pages=spec.Trim().Equals("reverse",StringComparison.OrdinalIgnoreCase)
                    ? Enumerable.Range(1,pdf.PageCount).Reverse().ToArray()
                    : spec.Split(',').Select(x=>int.Parse(x.Trim())).ToArray();
                if(pages.Length==0||pages.Any(x=>x<1||x>pdf.PageCount)) throw new UserFacingException("Invalid page number.");
                using var targetPdf=new PdfSharp.Pdf.PdfDocument();
                foreach(var page in pages){ct.ThrowIfCancellationRequested();targetPdf.AddPage(pdf.Pages[page-1]);}
                targetPdf.Save(output);
            }
            else
            {
                List<string> lines=new();
                using(var pdf=UglyToad.PdfPig.PdfDocument.Open(input))
                    foreach(var page in pdf.GetPages()) {ct.ThrowIfCancellationRequested();lines.Add(ContentOrderTextExtractor.GetText(page));}
                if(id=="convert.md") File.WriteAllText(output,string.Join("\n\n---\n\n",lines),Encoding.UTF8);
                else if(id=="convert.docx") WordDocument(output,lines);
                else if(id=="convert.xlsx")
                {
                    using var wb=new XLWorkbook();var ws=wb.Worksheets.Add("Extracted Text");
                    int r=1;int pageNo=0;foreach(string line in lines){pageNo++;foreach(string piece in line.Split('\n')){foreach(int offset in Enumerable.Range(0,Math.Max(1,(piece.Length+30000-1)/30000))){ws.Cell(r,1).Value="Page "+pageNo;ws.Cell(r,2).Value=piece.Substring(Math.Min(piece.Length,offset*30000),Math.Min(30000,Math.Max(0,piece.Length-offset*30000)));r++;}}}
                    ws.Column(1).Width=14;ws.Column(2).Width=90;wb.SaveAs(output);
                }
                else if(id=="convert.epub") Ebook(output,Path.GetFileNameWithoutExtension(input),lines);
                else throw new UserFacingException("Unsupported PDF text export.");
            }
            outputs.Add(output);progress?.Report((double)++n/job.Inputs.Count);
        }
        return new JobResult(outputs,null,"تم إنشاء نسخة نصية");
    }
    private static void WordDocument(string output,IEnumerable<string> pages)
    {
        using var doc=WordprocessingDocument.Create(output,WordprocessingDocumentType.Document);
        var main=doc.AddMainDocumentPart();main.Document=new Word.Document(new Word.Body());
        // Preserve PDF page boundaries while maintaining editable Word text.
        // This is not a pixel-exact layout reconstruction: PDF has no native
        // paragraph/table model to reproduce automatically.
        bool first=true;
        foreach(string page in pages)
        {
            if(!first)
                main.Document.Body!.Append(new Word.Paragraph(new Word.Run(new Word.Break { Type=Word.BreakValues.Page })));
            first=false;
            foreach(string line in page.Split('\n'))
                main.Document.Body!.Append(new Word.Paragraph(new Word.Run(new Word.Text(line){Space=SpaceProcessingModeValues.Preserve})));
        }
        main.Document.Save();
    }
    private static void Ebook(string output,string title,IEnumerable<string> pages)
    {
        using var zip=ZipFile.Open(output,ZipArchiveMode.Create);
        void Add(string path,string content,CompressionLevel level=CompressionLevel.Optimal){var entry=zip.CreateEntry(path,level);using var wr=new StreamWriter(entry.Open(),new UTF8Encoding(false));wr.Write(content);}
        Add("mimetype","application/epub+zip",CompressionLevel.NoCompression);
        Add("META-INF/container.xml","<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
        var items=pages.ToArray();
        string manifest=string.Join("",Enumerable.Range(0,items.Length).Select(i=>$"<item id=\"p{i}\" href=\"p{i}.xhtml\" media-type=\"application/xhtml+xml\"/>") );
        string spine=string.Join("",Enumerable.Range(0,items.Length).Select(i=>$"<itemref idref=\"p{i}\"/>"));
        string uuid=Guid.NewGuid().ToString();
        manifest += "<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>";
        Add("OEBPS/nav.xhtml","<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"><head><title>Contents</title></head><body><nav epub:type=\"toc\" id=\"toc\"><ol>" + string.Concat(Enumerable.Range(0,items.Length).Select(i => $"<li><a href=\"p{i}.xhtml\">Page {i+1}</a></li>")) + "</ol></nav></body></html>");
        Add("OEBPS/content.opf",$"<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"id\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"id\">urn:uuid:{uuid}</dc:identifier><dc:title>{System.Security.SecurityElement.Escape(title)}</dc:title><dc:language>und</dc:language></metadata><manifest>{manifest}</manifest><spine>{spine}</spine></package>");
        for(int i=0;i<items.Length;i++) Add($"OEBPS/p{i}.xhtml",$"<html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Page {i+1}</title></head><body><p>{System.Security.SecurityElement.Escape(items[i]).Replace("\n","<br/>")}</p></body></html>");
    }
}
