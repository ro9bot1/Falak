using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace Falak.Engines;

// Creates a standards-based ODF 1.2 spreadsheet using only locally available
// .NET libraries. Values/text are exported; spreadsheet-specific styling and
// formulas are not guaranteed to transfer precisely.
internal static class OdsLocalEngine
{
    public static bool Supports(string id,IReadOnlyList<string> inputs) =>
        id == "convert.ods" && inputs.Count > 0 && inputs.All(p =>
            Path.GetExtension(p).ToLowerInvariant() is ".xlsx" or ".csv" or ".tsv");
    public static Task<JobResult> Run(JobRequest request,IProgress<double>? progress,CancellationToken ct)
    {
        var outputs=new List<string>();int completed=0;
        foreach(string input in request.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            string dest=OutputPaths.NextTo(input,".ods");
            try
            {
                var rows=new List<string[]>();
                if(Path.GetExtension(input).Equals(".xlsx",StringComparison.OrdinalIgnoreCase))
                {
                    using var book=new XLWorkbook(input);
                    var sheet=book.Worksheet(1);
                    int lastRow=Math.Min(sheet.LastRowUsed()?.RowNumber() ?? 0,10000);
                    int lastCol=Math.Min(sheet.LastColumnUsed()?.ColumnNumber() ?? 0,100);
                    for(int r=1;r<=lastRow;r++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var row=new string[lastCol];
                        for(int c=1;c<=lastCol;c++) row[c-1]=sheet.Cell(r,c).GetFormattedString();
                        rows.Add(row);
                    }
                }
                else
                {
                    char separator=Path.GetExtension(input).Equals(".tsv",StringComparison.OrdinalIgnoreCase)?'\t':',';
                    foreach(string line in File.ReadLines(input,Encoding.UTF8).Take(10000))
                        rows.Add(line.Split(separator).Take(100).ToArray());
                }
                Save(dest,rows);
                if(new FileInfo(dest).Length==0) throw new UserFacingException("ODS output is empty.");
                outputs.Add(dest);
                progress?.Report((double)++completed/request.Inputs.Count);
            }
            catch{if(File.Exists(dest))File.Delete(dest);throw;}
        }
        return Task.FromResult(new JobResult(outputs,null,"ODS spreadsheet exported locally"));
    }
    private static void Save(string output,IReadOnlyList<string[]> rows)
    {
        XNamespace office="urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        XNamespace table="urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        XNamespace text="urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var content=new XDocument(new XDeclaration("1.0","UTF-8",null),
            new XElement(office+"document-content",
                new XAttribute(XNamespace.Xmlns+"office",office),
                new XAttribute(XNamespace.Xmlns+"table",table),
                new XAttribute(XNamespace.Xmlns+"text",text),
                new XAttribute(office+"version","1.2"),
                new XElement(office+"body",new XElement(office+"spreadsheet",
                    new XElement(table+"table",new XAttribute(table+"name","Falak data"),
                        rows.Select(row=>new XElement(table+"table-row",
                            row.Select(cell=>new XElement(table+"table-cell",
                                new XAttribute(office+"value-type","string"),
                                new XElement(text+"p",cell ?? ""))))))))));
        using var file=File.Create(output);
        using var zip=new ZipArchive(file,ZipArchiveMode.Create);
        void Entry(string path,string body,CompressionLevel level=CompressionLevel.Optimal)
        {
            var e=zip.CreateEntry(path,level);
            using var writer=new StreamWriter(e.Open(),new UTF8Encoding(false));
            writer.Write(body);
        }
        Entry("mimetype","application/vnd.oasis.opendocument.spreadsheet",CompressionLevel.NoCompression);
        Entry("content.xml",content.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
        Entry("styles.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\"><office:styles/></office:document-styles>");
        Entry("META-INF/manifest.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.spreadsheet\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/><manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>");
    }
}
