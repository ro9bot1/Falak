using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Falak.Core;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Falak.Engines;

// Locally converts DOCX text into new outputs. Not a layout-accurate renderer.
internal static class NativeDocxTextEngine
{
    public static bool Supports(string id, IReadOnlyList<string> inputs)
    {
        return inputs.Count > 0 && inputs.All(p =>
            Path.GetExtension(p).Equals(".docx",StringComparison.OrdinalIgnoreCase)) &&
            (id is "convert.txt" or "convert.html" or "convert.md" or "convert.epub" or "convert.jpg" or "convert.png");
    }
    public static async Task<JobResult> Run(JobRequest job, IProgress<double>? progress, CancellationToken ct)
    {
        var outputs = new List<string>(); int done = 0;
        foreach (var input in job.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(input)) throw new UserFacingException("DOCX input not found");
            var paragraphs = new List<string>();
            using (var document=WordprocessingDocument.Open(input,false))
            {
                var body=document.MainDocumentPart?.Document?.Body
                    ?? throw new UserFacingException("DOCX document has no text body.");
                foreach (var paragraph in body.Descendants<Word.Paragraph>())
                {
                    ct.ThrowIfCancellationRequested();
                    paragraphs.Add(string.Concat(paragraph.Descendants<Word.Text>().Select(x=>x.Text ?? "")));
                }
            }
            string ext="."+job.ActionId[8..];
            string output=OutputPaths.NextTo(input,ext);
            string content=string.Join("\n",paragraphs);
            try
            {
                if (ext is ".jpg" or ".png")
                {
                    outputs.AddRange(await Sta.Run(() => FalakSimpleExport.Images(content,input,ext),ct));
                    progress?.Report((double)++done/job.Inputs.Count);
                    continue;
                }
                if(ext==".txt" || ext==".md") File.WriteAllText(output,content,new UTF8Encoding(false));
                else if(ext==".html") File.WriteAllText(output,
                    "<!doctype html><html><head><meta charset=\"utf-8\"></head><body>" +
                    string.Concat(paragraphs.Select(s=>"<p>"+WebUtility.HtmlEncode(s)+"</p>"))+
                    "</body></html>",new UTF8Encoding(false));
                else if(ext==".epub") WriteEpub(output,Path.GetFileNameWithoutExtension(input),paragraphs);
                if(!File.Exists(output) || new FileInfo(output).Length==0)
                    throw new UserFacingException("No output was generated.");
                outputs.Add(output);
            }
            catch { if(File.Exists(output))File.Delete(output);throw; }
            progress?.Report((double)++done/job.Inputs.Count);
        }
        return new JobResult(outputs,null,"تم تصدير نص المستند محليًا");
    }
    private static void WriteEpub(string output,string title,IReadOnlyList<string> paragraphs)
    {
        using var archive=ZipFile.Open(output,ZipArchiveMode.Create);
        void Add(string path,string content,CompressionLevel level=CompressionLevel.Optimal)
        {
            var entry=archive.CreateEntry(path,level);
            using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write(content);
        }
        Add("mimetype","application/epub+zip",CompressionLevel.NoCompression);
        Add("META-INF/container.xml","<?xml version=\"1.0\"?><container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles></container>");
        Add("OEBPS/content.opf",$"<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\" version=\"3.0\" unique-identifier=\"bookid\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:identifier id=\"bookid\">urn:uuid:{Guid.NewGuid()}</dc:identifier><dc:title>{SecurityElement.Escape(title)}</dc:title><dc:language>und</dc:language></metadata><manifest><item id=\"chapter\" href=\"chapter.xhtml\" media-type=\"application/xhtml+xml\"/><item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/></manifest><spine><itemref idref=\"chapter\"/></spine></package>");
        Add("OEBPS/nav.xhtml","<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\"><head><title>Contents</title></head><body><nav epub:type=\"toc\" id=\"toc\"><ol><li><a href=\"chapter.xhtml\">Content</a></li></ol></nav></body></html>");
        Add("OEBPS/chapter.xhtml","<?xml version=\"1.0\" encoding=\"utf-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title>Document</title></head><body>"+string.Concat(paragraphs.Select(p=>"<p>"+SecurityElement.Escape(p)+"</p>"))+"</body></html>");
    }
}
