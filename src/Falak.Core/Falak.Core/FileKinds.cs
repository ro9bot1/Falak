using System;
using System.Collections.Generic;
using System.IO;

namespace Falak.Core;

public static class FileKinds
{
	private static readonly Dictionary<string, FileKind> ByExtension = new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase)
	{
		[".jpg"] = FileKind.Image,
		[".jpeg"] = FileKind.Image,
		[".png"] = FileKind.Image,
		[".webp"] = FileKind.Image,
		[".heic"] = FileKind.Image,
		[".heif"] = FileKind.Image,
		[".tif"] = FileKind.Image,
		[".tiff"] = FileKind.Image,
		[".avif"] = FileKind.Image,
		[".bmp"] = FileKind.Image,
		[".gif"] = FileKind.Image,
		[".svg"] = FileKind.Image,
		[".pdf"] = FileKind.Pdf,
		[".mp4"] = FileKind.Video,
		[".mov"] = FileKind.Video,
		[".mkv"] = FileKind.Video,
		[".webm"] = FileKind.Video,
		[".avi"] = FileKind.Video,
		[".wmv"] = FileKind.Video,
		[".m4v"] = FileKind.Video,
		[".mp3"] = FileKind.Audio,
		[".m4a"] = FileKind.Audio,
		[".wav"] = FileKind.Audio,
		[".flac"] = FileKind.Audio,
		[".ogg"] = FileKind.Audio,
		[".opus"] = FileKind.Audio,
		[".aiff"] = FileKind.Audio,
		[".aif"] = FileKind.Audio,
		[".wma"] = FileKind.Audio,
		[".docx"] = FileKind.Document,
		[".doc"] = FileKind.Document,
		[".rtf"] = FileKind.Document,
		[".odt"] = FileKind.Document,
		[".epub"] = FileKind.Document,
		[".md"] = FileKind.Document,
		[".html"] = FileKind.Document,
		[".htm"] = FileKind.Document,
		[".pptx"] = FileKind.Slides,
		[".ppt"] = FileKind.Slides,
		[".odp"] = FileKind.Slides,
		[".xlsx"] = FileKind.Sheet,
		[".xls"] = FileKind.Sheet,
		[".ods"] = FileKind.Sheet,
		[".csv"] = FileKind.Sheet,
		[".tsv"] = FileKind.Sheet,
		[".srt"] = FileKind.Subtitle,
		[".vtt"] = FileKind.Subtitle,
		[".zip"] = FileKind.Archive,
		[".rar"] = FileKind.Archive,
		[".7z"] = FileKind.Archive,
		[".tar"] = FileKind.Archive,
		[".gz"] = FileKind.Archive,
		[".tgz"] = FileKind.Archive,
		[".txt"] = FileKind.Text,
		[".log"] = FileKind.Text,
		[".json"] = FileKind.Text,
		[".xml"] = FileKind.Text
	};

	public static FileKind FromExtension(string? extension)
	{
		if (extension == null || !ByExtension.TryGetValue(extension, out var value))
		{
			return FileKind.Unknown;
		}
		return value;
	}

	public static FileKind Classify(IEnumerable<string> paths, Func<string, bool> isDirectory)
	{
		FileKind? fileKind = null;
		foreach (string path in paths)
		{
			FileKind fileKind2 = (isDirectory(path) ? FileKind.Folder : FromExtension(Path.GetExtension(path)));
			if (!fileKind.HasValue)
			{
				fileKind = fileKind2;
			}
			else if (fileKind != fileKind2)
			{
				return FileKind.Mixed;
			}
		}
		return fileKind ?? FileKind.Unknown;
	}
}
