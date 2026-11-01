using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Falak.Core;

namespace Falak.Engines;

public sealed class FileEngines
{
	private readonly Lazy<bool> _word = new Lazy<bool>(() => OfficeEngine.HasWord);

	private readonly Lazy<bool> _powerPoint = new Lazy<bool>(() => OfficeEngine.HasPowerPoint);

	private readonly Lazy<bool> _excel = new Lazy<bool>(() => OfficeEngine.HasExcel);

	public static bool IsAppAction(string actionId)
	{
		if (!actionId.StartsWith("action.", StringComparison.Ordinal))
		{
			return actionId == "tool.background" || actionId.StartsWith("menu.more", StringComparison.Ordinal) || actionId == "action.share-windows" || actionId == "action.share-android" || actionId == "action.float" || actionId is "tool.image-edit" or "tool.image-annotate";
		}
		return true;
	}

	public bool IsAvailable(string actionId, IReadOnlyList<string> inputs)
	{
		if (IsAppAction(actionId) && actionId != "tool.background")
		{
            // Keep Quick Share visible even before Google Quick Share is installed:
            // the WPF action offers a supported download link or the user's website.
            if (actionId == "action.share-android") return inputs.Count > 0;
			return inputs.Count > 0;
		}
		if (inputs.Count == 0)
		{
			return false;
		}
		List<FileKind> list = inputs.Select(KindOf).Distinct().ToList();
		if (actionId == "tool.merge-all")
		{
			if (inputs.Count >= 2 && list.Contains(FileKind.Image) && list.Contains(FileKind.Pdf))
			{
				return list.All((FileKind k) => (uint)(k - 2) <= 1u);
			}
			return false;
		}
		if (list.Count != 1)
		{
			return actionId == "tool.zip" || actionId == "tool.tar";
		}
		FileKind fileKind = list[0];
		List<string> list2 = inputs.Select((string i) => Path.GetExtension(i).ToLowerInvariant()).Distinct().ToList();
        if (PdfSlidesEngine.Supports(actionId, inputs)) return true;
        if (HeicLocalEngine.Supports(actionId, inputs)) return true;
        if (OdsLocalEngine.Supports(actionId,inputs)) return true;
		if (NativeDocxTextEngine.Supports(actionId, inputs)) return true;
		if (EpubTextEngine.Supports(actionId, inputs)) return true;
		if (LocalToolEngine.Supports(actionId, fileKind, inputs)) return true;
		if(fileKind == FileKind.Pdf && PdfExtraEngine.Supports(actionId)) return true;
		if(fileKind == FileKind.Image && actionId == "tool.image-metadata") return true;
		switch (actionId)
		{
		case "tool.zip":
			return true;
		case "tool.tar":
			return true;
		case "tool.gzip":
			return inputs.Count == 1 && File.Exists(inputs[0]);
		case "tool.extract":
			return list2.All((string e) => e == ".zip" || e == ".tar" || e == ".gz" || e == ".tgz" || ((e == ".rar" || e == ".7z") && LocalToolEngine.Has7z));
		case "tool.merge":
			return fileKind == FileKind.Pdf && inputs.Count >= 2;
		case "tool.split":
			return fileKind == FileKind.Pdf;
		case "tool.compress-pdf":
		case "tool.compress-pdf-strong":
			return fileKind == FileKind.Pdf;
		case "tool.background":
		{
			bool flag = inputs.Count == 1;
			if (flag)
			{
				bool flag2 = (uint)(fileKind - 2) <= 1u;
				flag = flag2;
			}
			return flag;
		}
		case "tool.images-to-pdf":
		case "tool.collage":
			return fileKind == FileKind.Image && inputs.Count >= 2;
		case "tool.half-size":
		case "tool.compress":
		case "tool.strip-metadata":
		case "tool.rotate":
			return fileKind == FileKind.Image;
		case "tool.messaging":
		case "tool.small-video":
			return fileKind == FileKind.Video;
		case "tool.audio-small":
			return fileKind == FileKind.Audio;
		case "tool.fix-encoding":
		case "tool.clean-arabic":
		case "tool.digits-latin":
		case "tool.digits-arabic":
		case "tool.digits-toggle":
		{
			bool flag = ((fileKind == FileKind.Subtitle || fileKind == FileKind.Text) ? true : false);
			return flag || list2.All((string e) => (e == ".csv" || e == ".tsv") ? true : false);
		}
		case "tool.excel-ready":
			return list2.All((string e) => (e == ".csv" || e == ".tsv") ? true : false);
		default:
			if (actionId.StartsWith("convert.", StringComparison.Ordinal))
			{
				return CanConvert(fileKind, "." + actionId.Substring(8), list2);
			}
			return false;
		}
	}

	private bool CanConvert(FileKind kind, string target, List<string> extensions)
	{
		switch (kind)
		{
		case FileKind.Image:
			return ImageEngine.Encodable.Contains(target) || target == ".pdf";
		case FileKind.Pdf:
		{
			bool result;
			switch (target)
			{
			case ".jpg":
			case ".png":
			case ".txt":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		case FileKind.Video:
		{
			bool result;
			switch (target)
			{
			case ".mp4":
			case ".wmv":
			case ".mp3":
			case ".m4a":
			case ".wav":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		case FileKind.Audio:
		{
			bool result;
			switch (target)
			{
			case ".mp3":
			case ".m4a":
			case ".wav":
			case ".flac":
			case ".wma":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		case FileKind.Subtitle:
		{
			bool result;
			switch (target)
			{
			case ".srt":
			case ".vtt":
			case ".txt":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		case FileKind.Document:
			return target == ".pdf" && _word.Value && extensions.All((string e) =>
			{
				switch (e)
				{
				default:
					return !(e == ".epub");
				case ".md":
				case ".html":
				case ".htm":
					return false;
				}
			});
		case FileKind.Slides:
		{
			bool result = _powerPoint.Value;
			if (result)
			{
				bool flag = ((target == ".pdf" || target == ".png" || target == ".jpg") ? true : false);
				result = flag;
			}
			return result;
		}
		case FileKind.Sheet:
		{
			bool result;
			switch (target)
			{
			case ".pdf":
				result = _excel.Value;
				break;
			case ".xlsx":
			case ".csv":
			case ".tsv":
				result = extensions.All((string e) =>
				{
					switch (e)
					{
					case ".csv":
					case ".tsv":
					case ".xlsx":
						return true;
					default:
						return false;
					}
				});
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		default:
			return false;
		}
	}

	public static FileKind KindOf(string path)
	{
		if (!Directory.Exists(path))
		{
			return FileKinds.FromExtension(Path.GetExtension(path));
		}
		return FileKind.Folder;
	}

	public Task<JobResult> RunAsync(JobRequest request, IProgress<double>? progress, CancellationToken ct)
	{
        if (PdfSlidesEngine.Supports(request.ActionId, request.Inputs)) return PdfSlidesEngine.Run(request, progress, ct);
        if (HeicLocalEngine.Supports(request.ActionId, request.Inputs)) return HeicLocalEngine.Run(request, progress, ct);
        if (OdsLocalEngine.Supports(request.ActionId,request.Inputs)) return OdsLocalEngine.Run(request,progress,ct);
		if (NativeDocxTextEngine.Supports(request.ActionId,request.Inputs)) return NativeDocxTextEngine.Run(request,progress,ct);
        if (EpubTextEngine.Supports(request.ActionId,request.Inputs)) return EpubTextEngine.Run(request,progress,ct);
		if (request.Inputs.Count == 0) throw new UserFacingException("No files selected.");
		if (LocalToolEngine.Supports(request.ActionId, KindOf(request.Inputs[0]), request.Inputs) || request.ActionId == "tool.extract-rar") return LocalToolEngine.Run(request, progress, ct);
		if (KindOf(request.Inputs[0]) == FileKind.Pdf && PdfExtraEngine.Supports(request.ActionId)) return Task.Run(() => PdfExtraEngine.Run(request, progress, ct), ct);
		if (KindOf(request.Inputs[0]) == FileKind.Image && request.ActionId == "tool.image-metadata") return Sta.Run(() => ImageMetadataEngine.Export(request.Inputs, progress, ct), ct);
		IReadOnlyList<string> inputs = request.Inputs;
		string actionId = request.ActionId;
		FileKind kind = ((inputs.Select(KindOf).Distinct().Count() == 1) ? KindOf(inputs[0]) : FileKind.Mixed);
		switch (actionId)
		{
		case "tool.zip":
			return Task.Run(() => ArchiveEngine.Zip(inputs, progress, ct), ct);
		case "tool.tar":
			return Task.Run(() => ExtraArchiveEngine.CreateTar(inputs, progress, ct), ct);
		case "tool.gzip":
			return Task.Run(() => ExtraArchiveEngine.CreateGzip(inputs, progress, ct), ct);
		case "tool.extract":
			return (inputs.All(i => Path.GetExtension(i).ToLowerInvariant() is ".rar" or ".7z") ? LocalToolEngine.Run(request, progress, ct) : Task.Run(() => ExtraArchiveEngine.Extract(inputs, progress, ct), ct));
		case "tool.merge":
			return Task.Run(() => PdfEngine.Merge(inputs, progress, ct), ct);
		case "tool.split":
			return Task.Run(() => PdfEngine.Split(inputs, progress, ct), ct);
		case "tool.merge-all":
			return Sta.Run(() => PdfEngine.MergeAll(inputs, progress, ct), ct);
		case "tool.compress-pdf":
			return Sta.Run(() => PdfCompressEngine.Smart(inputs, progress, ct), ct);
		case "tool.compress-pdf-strong":
			return Task.Run(() => PdfCompressEngine.Strong(inputs, progress, ct), ct);
		case "tool.images-to-pdf":
			return Sta.Run(() => PdfEngine.ImagesToPdf(inputs, combine: true, progress, ct), ct);
		case "tool.collage":
			return Sta.Run(() => CollageEngine.Create(inputs, progress, ct), ct);
		case "tool.compress":
			return Sta.Run(() => ImageEngine.Compress(inputs, progress, ct), ct);
		case "tool.half-size":
			return Sta.Run(() => ImageEngine.HalfSize(inputs, progress, ct), ct);
		case "tool.rotate":
			return Sta.Run(() => ImageEngine.Rotate(inputs, progress, ct), ct);
		case "tool.strip-metadata":
			return Sta.Run(() => ImageEngine.StripMetadata(inputs, progress, ct), ct);
		case "tool.messaging":
			return Task.Run(() => MediaEngine.Convert(inputs, ".mp4", MediaEngine.Preset.Messaging, progress, ct), ct);
		case "tool.small-video":
			return Task.Run(() => MediaEngine.Convert(inputs, ".mp4", MediaEngine.Preset.Small, progress, ct), ct);
		case "tool.audio-small":
			return Task.Run(() => MediaEngine.Convert(inputs, ".mp3", MediaEngine.Preset.LightAudio, progress, ct), ct);
		case "tool.fix-encoding":
			return Task.Run(() => TextEngine.FixEncoding(inputs, progress, ct), ct);
		case "tool.excel-ready":
			return Task.Run(() => TextEngine.ExcelReady(inputs, progress, ct), ct);
		case "tool.digits-latin":
			return Task.Run(() => TextEngine.Digits(inputs, toLatin: true, progress, ct), ct);
		case "tool.digits-toggle":
			return Task.Run(() => TextEngine.DigitsAuto(inputs, progress, ct), ct);
		case "tool.digits-arabic":
			return Task.Run(() => TextEngine.Digits(inputs, toLatin: false, progress, ct), ct);
		case "tool.clean-arabic":
			return Task.Run(() => TextEngine.CleanArabic(inputs, progress, ct), ct);
		default:
			if (actionId.StartsWith("convert.", StringComparison.Ordinal))
			{
				return Convert(kind, "." + actionId.Substring(8), inputs, progress, ct);
			}
			throw new UserFacingException("هذا الإجراء غير متاح بعد.");
		}
	}

	private static Task<JobResult> Convert(FileKind kind, string target, IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		switch (kind)
		{
		case FileKind.Image:
			if (target == ".pdf")
			{
				return Sta.Run(() => PdfEngine.ImagesToPdf(inputs, combine: false, progress, ct), ct);
			}
			return Sta.Run(() => ImageEngine.Convert(inputs, target, progress, ct), ct);
		case FileKind.Pdf:
			if (target == ".txt")
			{
				return Task.Run(() => PdfEngine.ToText(inputs, progress, ct), ct);
			}
			return Task.Run(() => PdfEngine.ToImages(inputs, target, progress, ct), ct);
		case FileKind.Video:
		case FileKind.Audio:
			return Task.Run(() => MediaEngine.Convert(inputs, target, MediaEngine.Preset.Source, progress, ct), ct);
		case FileKind.Subtitle:
			return Task.Run(() => TextEngine.ConvertSubtitles(inputs, target, progress, ct), ct);
		case FileKind.Sheet:
			if (target == ".pdf")
			{
				return OfficeEngine.ToPdf(inputs, progress, ct);
			}
			return Task.Run(() => TextEngine.ConvertTable(inputs, target, progress, ct), ct);
		case FileKind.Slides:
			if (target == ".png" || target == ".jpg")
			{
				return OfficeEngine.SlidesToImages(inputs, progress, ct, target);
			}
			goto case FileKind.Document;
		case FileKind.Document:
			return OfficeEngine.ToPdf(inputs, progress, ct);
		default:
			throw new UserFacingException("هذا التحويل غير متاح لهذا النوع من الملفات.");
		}
	}
}
