using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Falak.Engines;

// New, optional offline archive formats. Original ZIP implementation is unchanged.
internal static class ExtraArchiveEngine
{
    public static JobResult CreateTar(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
    {
        var entries = CollectFiles(inputs);
        if (entries.Count == 0) throw new UserFacingException("لا توجد ملفات للأرشفة");
        string origin = Path.TrimEndingDirectorySeparator(inputs[0]);
        string destination = OutputPaths.Unique(OutputPaths.FolderFor(origin),
            inputs.Count == 1 ? Path.GetFileNameWithoutExtension(origin) : "Falak archive", ".tar");
        var steps = new StepProgress(progress, entries.Count);
        OutputPaths.WriteAtomically(destination, temporary =>
        {
            using var stream = File.Create(temporary);
            using var tar = new TarWriter(stream, leaveOpen: false);
            foreach (var item in entries)
            {
                ct.ThrowIfCancellationRequested();
                using var source = File.OpenRead(item.Path);
                var entry = new PaxTarEntry(TarEntryType.RegularFile, item.Name) { DataStream = source };
                tar.WriteEntry(entry);
                steps.Next();
            }
        });
        return new JobResult(new[] { destination }, null, "تم إنشاء TAR");
    }

    public static JobResult CreateGzip(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
    {
        if (inputs.Count != 1 || !File.Exists(inputs[0]))
            throw new UserFacingException("يتطلب GZIP ملفًا واحدًا");
        string input = inputs[0];
        string destination = OutputPaths.Unique(OutputPaths.FolderFor(input), Path.GetFileName(input), ".gz");
        OutputPaths.WriteAtomically(destination, temporary =>
        {
            using var source = File.OpenRead(input);
            using var stream = File.Create(temporary);
            using var gzip = new GZipStream(stream, CompressionLevel.Optimal);
            CopyWithCancellation(source, gzip, ct);
        });
        progress?.Report(1);
        return new JobResult(new[] { destination }, null, "تم إنشاء GZIP");
    }

    public static JobResult Extract(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
    {
        var results = new List<string>();
        var steps = new StepProgress(progress, inputs.Count);
        foreach (string input in inputs)
        {
            ct.ThrowIfCancellationRequested();
            string ext = Path.GetExtension(input).ToLowerInvariant();
            if (ext == ".zip")
            {
                var outcome = ArchiveEngine.Extract(new[] { input }, null, ct);
                results.AddRange(outcome.Outputs);
            }
            else if (ext == ".tar" || ext == ".tgz" ||
                    (ext == ".gz" && input.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)))
            {
                string baseName = Path.GetFileName(input);
                baseName = baseName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? baseName[..^7] : Path.GetFileNameWithoutExtension(baseName);
                string folder = OutputPaths.Unique(OutputPaths.FolderFor(input), baseName, "");
                Directory.CreateDirectory(folder);
                try
                {
                    using var source = File.OpenRead(input);
                    bool compressed = ext is ".tgz" or ".gz";
                    using Stream archive = compressed ? new GZipStream(source, CompressionMode.Decompress, leaveOpen: false) : source;
                    ReadTar(archive, folder, ct);
                    results.Add(folder);
                }
                catch
                {
                    // Only the newly created output is removed on failure; the source is untouched.
                    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                    throw;
                }
            }
            else if (ext == ".gz")
            {
                string path = OutputPaths.Unique(OutputPaths.FolderFor(input), Path.GetFileNameWithoutExtension(input), "");
                OutputPaths.WriteAtomically(path, temporary =>
                {
                    using var source = File.OpenRead(input);
                    using var gzip = new GZipStream(source, CompressionMode.Decompress);
                    using var target = File.Create(temporary);
                    CopyWithCancellation(gzip, target, ct);
                });
                results.Add(path);
            }
            else throw new UserFacingException("صيغة أرشيف غير مدعومة");
            steps.Next();
        }
        return new JobResult(results, results.Count == 1 && Directory.Exists(results[0]) ? results[0] : null,
            "تم استخراج الأرشيف");
    }

    private static void ReadTar(Stream stream, string outputFolder, CancellationToken ct)
    {
        string root = Path.GetFullPath(outputFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var tar = new TarReader(stream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            ct.ThrowIfCancellationRequested();
            // Ignore symlinks and other special entries; never let archive paths escape the output.
            string relative = entry.Name.Replace('/', Path.DirectorySeparatorChar);
            string destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unsafe archive entry path");
            if (entry.EntryType == TarEntryType.Directory)
                Directory.CreateDirectory(destination);
            else if (entry.DataStream != null && entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
                CopyWithCancellation(entry.DataStream, file, ct);
            }
        }
    }

    private static List<(string Path, string Name)> CollectFiles(IReadOnlyList<string> inputs)
    {
        var files = new List<(string, string)>();
        foreach (string input in inputs)
        {
            if (Directory.Exists(input))
            {
                string parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(input))!;
                foreach (string file in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories))
                    files.Add((file, Path.GetRelativePath(parent, file).Replace('\\', '/')));
            }
            else if (File.Exists(input)) files.Add((input, Path.GetFileName(input)));
        }
        return files;
    }

    private static void CopyWithCancellation(Stream input, Stream output, CancellationToken ct)
    {
        byte[] buffer = new byte[131072];
        int count;
        while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            output.Write(buffer, 0, count);
        }
    }
}
