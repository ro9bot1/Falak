using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace Falak.Engines;

// Text export, not a visual/page-layout reproduction.
internal static class EpubTextEngine
{
    public static bool Supports(string id,IReadOnlyList<string> inputs) => inputs.All(p=>Path.GetExtension(p).Equals(".epub",StringComparison.OrdinalIgnoreCase))
        && (id is "convert.txt" or "convert.md" or "convert.html" or "convert.docx" or "convert.pdf");
    public static async Task<JobResult> Run(JobRequest job,IProgress<double>? progress,CancellationToken ct)
    {
        var outputs=new List<string>();int done=0;
        foreach(string input in job.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            List<string> pages=new();
            using (var zip=ZipFile.OpenRead(input))
            {
                if(zip.Entries.Count>2000 || zip.Entries.Sum(e=>e.Length)>150_000_000L) throw new UserFacingException("EPUB exceeds safe processing size.");
                foreach (var entry in zip.Entries.Where(e=>e.FullName.EndsWith(".xhtml",StringComparison.OrdinalIgnoreCase)|| e.FullName.EndsWith(".html",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName))
                {
                    ct.ThrowIfCancellationRequested();
                    if(entry.Length>20_000_000) throw new UserFacingException("EPUB chapter exceeds the safe processing limit.");
                    using var stream=entry.Open();using var reader=new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
                    string html=await reader.ReadToEndAsync(ct);
                    html=Regex.Replace(html,@"<(script|style)\b[^>]*>.*?</\1>","",RegexOptions.IgnoreCase|RegexOptions.Singleline,TimeSpan.FromSeconds(2));
                    html=Regex.Replace(html,@"</(p|h[1-6]|div|li|br|section)>|<br\s*/?>","\n",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
                    string text=Regex.Replace(html,@"<[^>]+>"," ",RegexOptions.Singleline,TimeSpan.FromSeconds(2));
                    pages.Add(WebUtility.HtmlDecode(text));
                }
            }
            if(pages.Count==0)throw new UserFacingException("The EPUB did not contain readable XHTML chapters.");
            string target="."+job.ActionId[8..];
            string output=OutputPaths.NextTo(input,target);
            string content=string.Join("\n\n",pages);
            if(target is ".txt" or ".md") File.WriteAllText(output,content,Encoding.UTF8);
            else if(target==".html")File.WriteAllText(output,"<!doctype html><html><head><meta charset=\"utf-8\"></head><body><pre>"+WebUtility.HtmlEncode(content)+"</pre></body></html>",Encoding.UTF8);
            else if(target==".pdf")
            {
                await Sta.Run(() => { FalakSimpleExport.Pdf(content, output); return true; },ct);
            }
            else if(target==".docx")
            {
                using var doc=WordprocessingDocument.Create(output,WordprocessingDocumentType.Document);
                var main=doc.AddMainDocumentPart();main.Document=new Word.Document(new Word.Body());
                foreach(string line in content.Split('\n'))
                    main.Document.Body!.Append(new Word.Paragraph(new Word.Run(new Word.Text(line){Space=SpaceProcessingModeValues.Preserve})));
                main.Document.Save();
            }
            else
            {
                string folder=Path.Combine(Path.GetTempPath(),"FalakEpub_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
                try {
                    string intermediate=Path.Combine(folder,"book.html");
                    File.WriteAllText(intermediate,"<!doctype html><html><head><meta charset=\"utf-8\"></head><body><pre>"+WebUtility.HtmlEncode(content)+"</pre></body></html>");
                    string exe=LocalToolEngine.Find("soffice.exe")!;
                    await LocalToolEngine.Invoke(exe,new[]{"-env:UserInstallation=file:///"+Path.Combine(folder,"profile").Replace('\\','/'),"--headless","--convert-to",target.TrimStart('.'),"--outdir",folder,intermediate},ct);
                    string produced=Path.Combine(folder,"book"+target);
                    if(!File.Exists(produced))throw new UserFacingException("LibreOffice could not export the selected format.");
                    File.Move(produced,output);
                } finally {try{Directory.Delete(folder,true);}catch{}}
            }
            outputs.Add(output);progress?.Report((double)++done/job.Inputs.Count);
        }
        return new JobResult(outputs,null,"تم تحويل نص الكتاب");
    }
}
