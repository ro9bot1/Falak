using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Falak.Engines;

internal static class ArchiveEngine
{
	public static JobResult Zip(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		List<(string Path, string Name)> entries = new List<(string, string)>();
		foreach (string input in inputs)
		{
			if (Directory.Exists(input))
			{
				string directoryName = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(input));
				foreach (string item in Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories))
				{
					entries.Add((item, Path.GetRelativePath(directoryName, item).Replace('\\', '/')));
				}
			}
			else if (File.Exists(input))
			{
				entries.Add((input, Path.GetFileName(input)));
			}
		}
		if (entries.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "لا توجد ملفات لضغطها.");
		}
		string text = Path.TrimEndingDirectorySeparator(inputs[0]);
		string folder = OutputPaths.FolderFor(text);
		string baseName = ((inputs.Count == 1) ? Path.GetFileNameWithoutExtension(text) : $"ملفات مضغوطة ({inputs.Count})");
		string text2 = OutputPaths.Unique(folder, baseName, ".zip");
		StepProgress steps = new StepProgress(progress, entries.Count);
		long before = 0L;
		OutputPaths.WriteAtomically(text2, (string temp) =>
		{
			using ZipArchive destination = ZipFile.Open(temp, ZipArchiveMode.Create);
			foreach (var (text3, entryName) in entries)
			{
				ct.ThrowIfCancellationRequested();
				destination.CreateEntryFromFile(text3, entryName, CompressionLevel.Optimal);
				before += new FileInfo(text3).Length;
				steps.Next();
			}
		});
		long length = new FileInfo(text2).Length;
		return new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(text2), null, $"ضغطنا {Texts.Files(entries.Count)} · من {Texts.Size(before)} إلى {Texts.Size(length)}");
	}

	public static JobResult Extract(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			string text = OutputPaths.Unique(OutputPaths.FolderFor(input), Path.GetFileNameWithoutExtension(input), "");
			try
			{
				ZipFile.ExtractToDirectory(input, text);
			}
			catch (InvalidDataException inner)
			{
				throw new UserFacingException("«" + Path.GetFileName(input) + "» ليس ملف ZIP سليما\u064b.", inner);
			}
			list.Add(text);
			stepProgress.Next();
		}
		return new JobResult(list, (list.Count == 1) ? list[0] : null, "فككنا الضغط في مجلد بجانب الملف");
	}
}
