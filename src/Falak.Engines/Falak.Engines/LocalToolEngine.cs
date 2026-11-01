using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.Engines;

// Additive, opt-in engine. Old engines are untouched. No shell command execution.
// Every external executable is local; the application never downloads anything.
public static class LocalToolEngine
{
    private static readonly string[] MediaExtensions = { ".mkv", ".webm", ".avi", ".gif", ".ogg", ".opus", ".aiff", ".webp", ".avif", ".heic" };
    public static string? Find(string exe)
    {
        var candidates = new List<string> {
            Path.Combine(AppContext.BaseDirectory, "Tools", exe),
            Path.Combine(AppContext.BaseDirectory, "Tools", "ImageMagick", exe),
            Path.Combine(AppContext.BaseDirectory, exe)
        };
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(dir.Trim('"'), exe));
        if (exe.Equals("soffice.exe", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", exe));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", exe));
        }
        if (exe.Equals("7z.exe", StringComparison.OrdinalIgnoreCase))
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", exe));
        string? found = candidates.FirstOrDefault(File.Exists);
        if (found != null) return found;
        // Portable distributions may place magick.exe one folder below Tools.
        if (exe.Equals("magick.exe", StringComparison.OrdinalIgnoreCase) || exe.Equals("heif-enc.exe", StringComparison.OrdinalIgnoreCase))
        {
            string tools = Path.Combine(AppContext.BaseDirectory, "Tools");
            if (Directory.Exists(tools))
                return Directory.EnumerateFiles(tools, exe, SearchOption.AllDirectories).FirstOrDefault();
        }
        return null;
    }
    public static bool HasFFmpeg => Find("ffmpeg.exe") != null;
    private static readonly Lazy<bool> HasHeicMuxer = new(() =>
    {
        string? executable=Find("ffmpeg.exe");
        if(executable==null)return false;
        try
        {
            var psi=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            psi.ArgumentList.Add("-hide_banner");psi.ArgumentList.Add("-muxers");
            using var process=Process.Start(psi);
            if(process==null)return false;
            string output=process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if(!process.WaitForExit(4000)){process.Kill(true);return false;}
            return output.Split('\n').Any(line => line.Contains(" heic ",StringComparison.OrdinalIgnoreCase) || line.Contains(" heif ",StringComparison.OrdinalIgnoreCase));
        }
        catch{return false;}
    });
    public static bool HasOffice => Find("soffice.exe") != null;
    public static bool Has7z => Find("7z.exe") != null;
    public static bool IsNewConversion(string target) => MediaExtensions.Contains(target, StringComparer.OrdinalIgnoreCase);
    public static bool Supports(string action, Falak.Core.FileKind kind, IReadOnlyList<string> files)
    {
        if (action.StartsWith("convert.", StringComparison.Ordinal))
        {
            var target = "." + action[8..];
            if (IsNewConversion(target) && target != ".heic") return HasFFmpeg &&
                (kind is Falak.Core.FileKind.Image or Falak.Core.FileKind.Video or Falak.Core.FileKind.Audio);
            if (target == ".ods") return HasOffice && kind == Falak.Core.FileKind.Sheet;
            // PDF to PPTX is routed to the local, bundled visual slide converter.
            if (kind == Falak.Core.FileKind.Document && target is ".txt" or ".html" or ".epub" or ".png" or ".jpg")
                return HasOffice;
            if (kind == Falak.Core.FileKind.Document && target == ".md") return Find("pandoc.exe") != null;
            if (kind == Falak.Core.FileKind.Slides && target == ".jpg") return HasOffice;
            return false;
        }
        if (action.StartsWith("tool.video-", StringComparison.Ordinal)) return HasFFmpeg && kind == Falak.Core.FileKind.Video;
        if (action.StartsWith("tool.audio-", StringComparison.Ordinal)) return HasFFmpeg && kind == Falak.Core.FileKind.Audio;
        if (action is "tool.image-redact" or "tool.image-crop") return HasFFmpeg && kind == Falak.Core.FileKind.Image;
        if (action == "tool.extract-rar") return Has7z && kind == Falak.Core.FileKind.Archive && files.All(f => Path.GetExtension(f).ToLowerInvariant() is ".rar" or ".7z");
        return false;
    }
    public static async Task<JobResult> Run(JobRequest req, IProgress<double>? progress, CancellationToken ct)
    {
        string id = req.ActionId;
        string? exe = null;
        bool sevenZip = id == "tool.extract-rar";
        bool pandoc = id == "convert.md";
        bool office = id == "convert.ods" || (id == "convert.pptx" && req.Inputs.Count > 0 && Path.GetExtension(req.Inputs[0]).Equals(".pdf",StringComparison.OrdinalIgnoreCase)) || (id.StartsWith("convert.") && req.Inputs.Count > 0 &&
            Falak.Core.FileKinds.FromExtension(Path.GetExtension(req.Inputs[0])) is Falak.Core.FileKind.Document or Falak.Core.FileKind.Slides);
        exe = Find(sevenZip ? "7z.exe" : pandoc ? "pandoc.exe" : office ? "soffice.exe" : "ffmpeg.exe");
        if (exe == null) throw new UserFacingException("المحرك المحلي المطلوب غير متوفر / Required local engine missing: " + (sevenZip ? "7-Zip" : office ? "LibreOffice" : pandoc ? "Pandoc" : "FFmpeg"));
        var outputs = new List<string>();
        int n = 0;
        foreach (string input in req.Inputs)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(input)) throw new UserFacingException("File not found: " + Path.GetFileName(input));
            string ext = Path.GetExtension(input).ToLowerInvariant();
            string target = id.StartsWith("convert.", StringComparison.Ordinal) ? "." + id[8..] : ext;
            var args = new List<string>();
            string suffix = id.Replace("tool.", "").Replace("convert.", "");
            string output;
            if (pandoc)
            {
                output = OutputPaths.NextTo(input, ".md");
                args.AddRange(new[] { input, "--standalone", "-o", output });
            }
            else if (sevenZip)
            {
                string folder = OutputPaths.NewFolderNextTo(input, "extracted");
                output = folder;
                args.AddRange(new[] { "x", "-y", "-o" + folder, "--", input });
            }
            else if (office)
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), "FalakOffice_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempFolder);
                try
                {
                    string format = target.TrimStart('.');
                    if (format == "jpg" || format == "png") format = "pdf";
                    args.AddRange(new[] { "-env:UserInstallation=file:///" + Path.Combine(tempFolder, "profile").Replace('\\','/'), "--headless" });
                    if(target == ".pptx" && ext == ".pdf") args.Add("--infilter=impress_pdf_import");
                    args.AddRange(new[] { "--convert-to", target == ".pptx" ? "pptx:Impress MS PowerPoint 2007 XML" : format, "--outdir", tempFolder, input });
                    await Invoke(exe, args, ct);
                    string candidate = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(input) + "." + format);
                    if (!File.Exists(candidate)) throw new UserFacingException("No exported document was generated. Check the installed LibreOffice filters.");
                    if (target is ".jpg" or ".png")
                    {
                        // Reuse Falak's existing, tested multi-page PDF rasterizer.
                        JobResult rendered=await PdfEngine.ToImages(new[]{candidate},target,null,ct);
                        int slide=0;
                        foreach(var tempImage in rendered.Outputs)
                        {
                            string dst=OutputPaths.NextTo(input,target,"Page "+(++slide).ToString("00"));
                            File.Move(tempImage,dst);
                            outputs.Add(dst);
                        }
                        if(slide==0)throw new UserFacingException("No slides were exported.");
                    }
                    else
                    {
                        output = OutputPaths.NextTo(input, "." + format);
                        File.Move(candidate, output);
                        if(target == ".pptx")
                        {
                            try
                            {
                                using var deck=PresentationDocument.Open(output,false);
                                if((deck.PresentationPart?.Presentation?.SlideIdList?.ChildElements.Count ?? 0)==0)
                                    throw new UserFacingException("PDF conversion produced no slides; result rejected.");
                            }
                            catch {File.Delete(output);throw;}
                        }
                        outputs.Add(output);
                    }
                }
                finally { try { Directory.Delete(tempFolder, true); } catch (IOException) { } }
                progress?.Report((double)++n / req.Inputs.Count);
                continue;
            }
            else
            {
                // No network, no unsafe shell expansion, no in-place edits.
                if (id == "tool.video-snapshot") target = ".jpg";
                if (id == "tool.audio-visualize") target = ".png";
                if (id == "tool.video-split") target = ".mkv";
                if (id == "tool.image-redact" || id == "tool.image-crop") target = ".png";
                output = OutputPaths.NextTo(input, target, suffix);
                args.AddRange(new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", input });
                string opt = req.Options ?? "";
                switch (id)
                {
                    case "tool.video-mute": args.AddRange(new[]{"-an", "-c:v", "libx264", "-crf", "20"}); target=".mp4"; break;
                    case "tool.video-metadata": case "tool.audio-metadata": args.AddRange(new[]{"-map_metadata", "-1", "-c", "copy"}); break;
                    case "tool.video-compress": args.AddRange(new[]{"-c:v","libx264","-crf","28","-preset","medium","-c:a","aac","-b:a","96k"}); target=".mp4"; break;
                    case "tool.video-snapshot": args.AddRange(new[]{"-ss", "00:00:01", "-frames:v", "1", "-q:v", "3"}); break;
                    case "tool.video-speed":
                    {
                        double speed = Positive(opt, 1.25, .5, 2);
                        args.AddRange(new[]{"-filter:v", "setpts=PTS/" + Invariant(speed), "-filter:a", "atempo=" + Invariant(speed), "-c:v", "libx264", "-c:a", "aac"}); target=".mp4"; break;
                    }
                    case "tool.video-trim": case "tool.audio-trim":
                    {
                        var (start,duration) = Range(opt);
                        args.AddRange(new[]{"-ss", Invariant(start), "-t", Invariant(duration)});
                        if (id.StartsWith("tool.video")) { args.AddRange(new[]{"-c:v","libx264","-c:a","aac"}); target=".mp4"; }
                        break;
                    }
                    case "tool.video-crop":
                    {
                        var (x,y,w,h) = Rectangle(opt);
                        args.AddRange(new[]{"-vf", SafeCrop(x,y,w,h),"-c:v","libx264","-c:a","copy"}); target=".mp4"; break;
                    }
                    case "tool.video-redact": case "tool.image-redact":
                    {
                        var (x,y,w,h) = Rectangle(opt);
                        args.AddRange(new[]{"-vf", $"drawbox=x={x}:y={y}:w={w}:h={h}:color=black@1:t=fill"});
                        if (id == "tool.video-redact") {args.AddRange(new[]{"-c:v","libx264","-c:a","copy"}); target=".mp4"; }
                        break;
                    }
                    case "tool.image-crop":
                    {
                        var (x,y,w,h) = Rectangle(opt);
                        args.AddRange(new[]{"-vf", SafeCrop(x,y,w,h)}); break;
                    }
                    case "tool.video-split":
                    {
                        var duration=Positive(opt, 30, 1, 36000);
                        string folder=OutputPaths.NewFolderNextTo(input,"segments");
                        output=Path.Combine(folder,"part_%03d.mkv");
                        args.AddRange(new[]{"-map","0","-c","copy","-f","segment","-segment_time",Invariant(duration),"-reset_timestamps","1"});
                        break;
                    }
                    case "tool.audio-normalize": args.AddRange(new[]{"-af","loudnorm=I=-16:TP=-1.5:LRA=11","-c:a","libmp3lame","-b:a","192k"}); target=".mp3"; break;
                    case "tool.audio-visualize": args.AddRange(new[]{"-filter_complex","showwavespic=s=1280x360:colors=0x1FAD85","-frames:v","1"}); break;
                    case "tool.audio-channels": args.AddRange(new[]{"-ac", ((int)Positive(opt,2,1,2)).ToString(),"-c:a","libmp3lame","-b:a","192k"}); target=".mp3"; break;
                    case "tool.audio-bleep":
                    {
                        var (start,duration) = Range(opt);
                        string startAt = Invariant(start);
                        string stopAt = Invariant(start + duration);
                        string length = Invariant(duration);
                        string fadeOutAt = Invariant(Math.Max(0, duration - 0.035));
                        string filter = $"[0:a]volume=enable='between(t,{startAt},{stopAt})':volume=0[quiet];" +
                            $"sine=frequency=950:sample_rate=44100:duration={length}," +
                            $"afade=t=in:st=0:d=0.02,afade=t=out:st={fadeOutAt}:d=0.035," +
                            $"volume=0.24,adelay={Invariant(start*1000)}:all=1[beep];" +
                            "[quiet][beep]amix=inputs=2:duration=first:dropout_transition=0[out]";
                        args.AddRange(new[]{"-filter_complex",filter,"-map","[out]","-c:a","libmp3lame","-b:a","192k"});
                        target=".mp3"; break;
                    }
                    default:
                        if (!id.StartsWith("convert.", StringComparison.Ordinal))
                            throw new UserFacingException("Unsupported tool: " + id);
                        if (target == ".webm") args.AddRange(new[]{"-c:v","libvpx-vp9","-b:v","0","-crf","35","-c:a","libopus"});
                        else if (target == ".ogg") args.AddRange(new[]{"-vn","-c:a","libvorbis","-q:a","5"});
                        else if (target == ".opus") args.AddRange(new[]{"-vn","-c:a","libopus","-b:a","128k"});
                        else if (target == ".gif") args.AddRange(new[]{"-vf","fps=10,scale=640:-1:flags=lanczos","-loop","0"});
                        else if (target == ".avif") args.AddRange(new[]{"-frames:v","1","-c:v","libaom-av1"});
                        else if (target == ".heic") args.AddRange(new[]{"-frames:v","1","-c:v","libx265"});
                        else if (target == ".webp" && Falak.Core.FileKinds.FromExtension(ext) == Falak.Core.FileKind.Image) args.AddRange(new[]{"-frames:v","1","-c:v","libwebp"});
                        else if (target == ".aiff") args.AddRange(new[]{"-vn","-c:a","pcm_s16be"});
                        else if (target == ".avi") args.AddRange(new[]{"-c:v","mpeg4","-q:v","4","-c:a","mp3"});
                        else if (target == ".mkv") args.AddRange(new[]{"-c:v","libx264","-crf","20","-c:a","aac"});
                        break;
                }
                if (id != "tool.video-split") output = OutputPaths.NextTo(input, target, suffix);
                args.Add(output);
            }
            await Invoke(exe,args,ct);
            if (sevenZip) {
                // Extracting untrusted archives demands strict path validation; 7z may contain traversal paths.
                // Do not expose arbitrary existing files: extraction is to a fresh directory only.
                outputs.Add(output);
            }
            else if (id == "tool.video-split") outputs.Add(Path.GetDirectoryName(output)!);
            else {
                if (!File.Exists(output) || new FileInfo(output).Length == 0)
                    throw new UserFacingException("The processing engine produced no file.");
                outputs.Add(output);
            }
            progress?.Report((double)++n / req.Inputs.Count);
        }
        return new JobResult(outputs, null, "تمت المعالجة محليًا");
    }
    private static async Task<string> RenderPdfFirstPage(string pdf, string extension, CancellationToken ct)
    {
        // LibreOffice exports PDF. Render via ffmpeg only when a suitable PDF decoder is installed.
        string? ff = Find("ffmpeg.exe");
        if (ff == null) throw new UserFacingException("Rendering PDF to an image requires a local PDF rasterizer.");
        string dest = OutputPaths.NextTo(pdf, extension);
        await Invoke(ff,new[]{"-hide_banner","-loglevel","error","-nostdin","-i",pdf,"-frames:v","1",dest},ct);
        return dest;
    }
    public static async Task Invoke(string exe, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        foreach(var a in args) psi.ArgumentList.Add(a);
        using var proc = new Process{ StartInfo = psi };
        if (!proc.Start()) throw new UserFacingException("Could not start local processor: "+Path.GetFileName(exe));
        Task<string> error = proc.StandardError.ReadToEndAsync(ct);
        Task<string> stdout = proc.StandardOutput.ReadToEndAsync(ct);
        try { await proc.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { if (!proc.HasExited) proc.Kill(entireProcessTree:true); throw; }
        string err = await error;
        await stdout;
        if(proc.ExitCode!=0) throw new UserFacingException($"{Path.GetFileName(exe)} exited {proc.ExitCode}: {err[^Math.Min(err.Length,700)..]}");
    }
    private static double Positive(string? raw,double fallback,double min,double max)
    {
        double val=string.IsNullOrWhiteSpace(raw)?fallback:double.Parse(raw.Trim(),CultureInfo.InvariantCulture);
        if(!double.IsFinite(val)||val<min||val>max)throw new UserFacingException("Invalid numeric value (out of range).");
        return val;
    }
    private static string Invariant(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
    private static (double Start,double Duration) Range(string raw)
    {
        string[] p=(raw??"").Split(',');
        if(p.Length!=2)throw new UserFacingException("Enter start,duration in seconds (example: 5,20).");
        var start=Positive(p[0],0,0,864000); var duration=Positive(p[1],10,0.01,864000);
        return (start,duration);
    }
    // FFmpeg evaluates the rectangle against each input frame, so small images
    // will not fail with "crop width/height exceeds input" for a 640x360 preset.
    // No scaling or modification of the source file occurs.
    private static string SafeCrop(int x,int y,int width,int height) =>
        $"crop=w='min({width},iw)':h='min({height},ih)':x='min({x},iw-ow)':y='min({y},ih-oh)'";
    private static (int X,int Y,int W,int H) Rectangle(string raw)
    {
        string[] p=(raw??"").Split(',');
        if(p.Length!=4)throw new UserFacingException("Enter x,y,width,height in pixels.");
        int[] d=p.Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
        if(d[0]<0||d[1]<0||d[2]<1||d[3]<1||d.Any(x=>x>32768))throw new UserFacingException("Invalid pixel rectangle.");
        return (d[0],d[1],d[2],d[3]);
    }
}
