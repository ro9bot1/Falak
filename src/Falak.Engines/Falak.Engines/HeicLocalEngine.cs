using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.Engines;

// Offline HEIC encoder integration.  Supported only if a real encoder is installed;
// adding the .heic suffix or wrapping a JPEG is NEVER considered success.
internal static class HeicLocalEngine
{
    private static readonly Lazy<string?> Encoder = new(FindEncoder);
    public static bool Supports(string id, IReadOnlyList<string> paths) => id == "convert.heic" &&
        paths.Count > 0 && paths.All(p => Path.GetExtension(p).ToLowerInvariant() is
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".webp") && Encoder.Value != null;

    private static string? FindEncoder()
    {
        // Verify REAL HEIC output, not only the presence of a command-line executable.
        // A libheif build may lack an HEVC encoder despite printing its help successfully.
        string tempDir = Path.Combine(Path.GetTempPath(), "FalakHEICProbe_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string input = Path.Combine(AppContext.BaseDirectory, "QA_Samples", "Images", "Falak_Image_01.png");
            if (!File.Exists(input))
            {
                input = Path.Combine(tempDir, "probe.png");
                // Embedded minimal PNG; no user file or network needed.
                File.WriteAllBytes(input, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j6dUAAAAASUVORK5CYII="));
            }
            foreach (string file in new[] { "heif-enc.exe", "magick.exe" })
            {
                string? path = LocalToolEngine.Find(file);
                if (path is null) continue;
                string dest = Path.Combine(tempDir, "probe.heic");
                try
                {
                    var psi = new ProcessStartInfo(path) { UseShellExecute = false,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        CreateNoWindow = true };
                    if (file == "heif-enc.exe")
                        foreach (string arg in new[] { "-q", "75", "-o", dest, input }) psi.ArgumentList.Add(arg);
                    else
                        foreach (string arg in new[] { input, "-quality", "75", "HEIC:" + dest }) psi.ArgumentList.Add(arg);
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    Task<string> stdout = proc.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = proc.StandardError.ReadToEndAsync();
                    if (!proc.WaitForExit(8000)) { proc.Kill(entireProcessTree:true); continue; }
                    Task.WaitAll(stdout, stderr);
                    if (proc.ExitCode == 0 && ValidHeic(dest)) return path;
                }
                catch { }
                finally { if (File.Exists(dest)) File.Delete(dest); }
            }
        }
        finally { try { Directory.Delete(tempDir, true); } catch { } }
        return null;
    }

    public static async Task<JobResult> Run(JobRequest req, IProgress<double>? progress, CancellationToken ct)
    {
        string encoder = Encoder.Value ?? throw new UserFacingException(
            "HEIC requires a local HEVC-capable encoder. Install the verified Falak ImageMagick tools first.");
        var outputs = new List<string>();
        for (int i = 0; i < req.Inputs.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string input = req.Inputs[i];
            string final = OutputPaths.NextTo(input, ".heic");
            string temp = Path.Combine(Path.GetDirectoryName(final)!, ".falak-" + Guid.NewGuid().ToString("N") + ".heic");
            try
            {
                if (Path.GetFileName(encoder).Equals("heif-enc.exe", StringComparison.OrdinalIgnoreCase))
                    await LocalToolEngine.Invoke(encoder, new[] { "-q", "85", "-o", temp, input }, ct);
                else
                    await LocalToolEngine.Invoke(encoder, new[] { input, "-quality", "85", "HEIC:" + temp }, ct);
                if (!ValidHeic(temp)) throw new UserFacingException("HEIC encoder returned no valid HEIC file.");
                File.Move(temp, final);
                outputs.Add(final);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            progress?.Report((double)(i+1) / req.Inputs.Count);
        }
        return new JobResult(outputs, null, "HEIC images generated locally using a supported HEVC codec.");
    }

    private static bool ValidHeic(string file)
    {
        if (!File.Exists(file) || new FileInfo(file).Length < 40) return false;
        Span<byte> header = stackalloc byte[32];
        using var stream = File.OpenRead(file);
        if (stream.Read(header) < 16) return false;
        if (System.Text.Encoding.ASCII.GetString(header[4..8]) != "ftyp") return false;
        var brands = System.Text.Encoding.ASCII.GetString(header[8..]);
        return new[] { "heic", "heix", "hevc", "hevx", "mif1", "msf1" }.Any(brands.Contains);
    }
}
