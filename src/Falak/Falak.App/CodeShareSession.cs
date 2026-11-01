using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.App;

// Explicitly enabled temporary, read-only LAN file transfer. Does not implement
// Google's proprietary Quick Share protocol or expose files to the Internet.
internal sealed class CodeShareSession : IDisposable
{
    private readonly IReadOnlyList<string> _files;
    private readonly CancellationTokenSource _stop = new();
    private readonly TcpListener _listener;
    private readonly string _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
    public string Url { get; }
    public CodeShareSession(IReadOnlyList<string> paths)
    {
        _files = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (_files.Count == 0) throw new IOException("No files selected for Code Share.");
        _listener = new TcpListener(IPAddress.Any,0);
        _listener.Start(4);
        int port=((IPEndPoint)_listener.LocalEndpoint).Port;
        string? ip = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n=>n.OperationalStatus==OperationalStatus.Up &&
                (n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211 || n.NetworkInterfaceType==NetworkInterfaceType.Ethernet))
            .OrderBy(n=>n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211 ? 0 : 1)
            .SelectMany(n=>n.GetIPProperties().UnicastAddresses)
            .Select(a=>a.Address)
            .Where(a=>a.AddressFamily==AddressFamily.InterNetwork && !IPAddress.IsLoopback(a) &&
                !a.ToString().StartsWith("169.254.",StringComparison.Ordinal))
            .Select(a=>a.ToString()).FirstOrDefault();
        if (ip is null) { _listener.Stop(); _stop.Dispose(); throw new IOException("Connect your PC and Android to the same local Wi-Fi network before sharing."); }
        Url=$"http://{ip}:{port}/{_token}/";
        _stop.CancelAfter(TimeSpan.FromMinutes(10));
        _ = Task.Run(Listen);
    }
    private async Task Listen()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                using var client=await _listener.AcceptTcpClientAsync(_stop.Token);
                client.ReceiveTimeout=5000;client.SendTimeout=10000;
                await Handle(client,_stop.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception) { /* Ignore malformed/abandoned network requests. */ }
        }
    }
    private async Task Handle(TcpClient client,CancellationToken ct)
    {
        using var stream=client.GetStream();
        var buf=new byte[8192];
        int count=0;
        while(count<buf.Length)
        {
            int n=await stream.ReadAsync(buf.AsMemory(count,buf.Length-count),ct);
            if(n==0) return;
            count+=n;
            if(count>=4 && Encoding.ASCII.GetString(buf,0,count).Contains("\r\n\r\n",StringComparison.Ordinal)) break;
        }
        string line=Encoding.ASCII.GetString(buf,0,count).Split("\r\n",StringSplitOptions.None)[0];
        string[] parts=line.Split(' ');
        if(parts.Length<3 || parts[0]!="GET") { await Error(stream,405,ct);return; }
        string path=parts[1].Split('?')[0];
        string basePath="/"+_token+"/";
        if(!path.StartsWith(basePath,StringComparison.Ordinal)) { await Error(stream,404,ct);return; }
        var segment=path.Substring(basePath.Length);
        if(segment.Length==0)
        {
            var links=string.Join("",_files.Select((f,i)=>
                $"<li><a href=\"{i}\">{HtmlEncoder.Default.Encode(Path.GetFileName(f))}</a></li>"));
            string html="<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'></head>"+
                "<body style='font:18px sans-serif;padding:24px'><h2>Falak Code Share</h2><p>Tap a file to download it.</p><ul>"+links+"</ul></body></html>";
            byte[] body=Encoding.UTF8.GetBytes(html);
            await Header(stream,200,"text/html; charset=utf-8",body.Length,null,ct);
            await stream.WriteAsync(body,ct);
            return;
        }
        if(!int.TryParse(segment,out int index)||index<0||index>=_files.Count) {await Error(stream,404,ct);return;}
        string filename=_files[index];
        try
        {
            using var source=new FileStream(filename,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
            // RFC 5987 UTF-8 filename (no shell or path interpretation).
            var disposition="attachment; filename*=UTF-8''"+Uri.EscapeDataString(Path.GetFileName(filename));
            await Header(stream,200,"application/octet-stream",source.Length,disposition,ct);
            await source.CopyToAsync(stream,81920,ct);
        }
        catch(IOException) { /* A file may be deleted while sharing; do not disclose its path. */ }
    }
    private static async Task Header(Stream stream,int code,string type,long size,string? disposition,CancellationToken ct)
    {
        string head=$"HTTP/1.1 {code} {(code==200?"OK":"Not Found")}\r\nContent-Type: {type}\r\nContent-Length: {size}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n";
        if(disposition!=null) head+="Content-Disposition: "+disposition+"\r\n";
        head+="\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head),ct);
    }
    private static async Task Error(Stream stream,int code,CancellationToken ct)
    {
        byte[] body=Encoding.UTF8.GetBytes("Unavailable");
        await Header(stream,code,"text/plain",body.Length,null,ct);
        await stream.WriteAsync(body,ct);
    }
    public void Dispose()
    {
        if(!_stop.IsCancellationRequested) _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }
}
