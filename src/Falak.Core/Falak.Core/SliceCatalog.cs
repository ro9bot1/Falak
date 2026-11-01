using System;
using System.Collections.Generic;
using System.Linq;

namespace Falak.Core;

public static class SliceCatalog
{
	// Supplied by the host application; never changes the built-in action ordering.
    private static WheelAction[] _macroActions = Array.Empty<WheelAction>();
    public static void SetMacroActions(IEnumerable<WheelAction> actions) =>
        _macroActions = actions.Where(x => x.Id.StartsWith("macro.", StringComparison.Ordinal)).ToArray();

	public static readonly WheelAction Pending = new WheelAction("pending", "…", "…");

	public static readonly WheelAction Nothing = new WheelAction("nothing", "لا خيارات لهذا الملف", "Nothing for this file");

	public static readonly WheelAction Ready = new WheelAction("ready", "", "");

	private static readonly Dictionary<string, (string Ar, string En)> Labels = new Dictionary<string, (string, string)>
	{
		["tool.compress"] = ("ضغط", "Compress"),
		["tool.half-size"] = ("نصف الحجم", "Half size"),
		["tool.strip-metadata"] = ("إزالة الموقع", "Remove location"),
		["tool.images-to-pdf"] = ("دمج في PDF", "Merge to PDF"),
		["tool.collage"] = ("كولاج صور", "Photo collage"),
		["tool.merge-all"] = ("دمج الكل PDF", "Merge all to PDF"),
		["tool.compress-pdf"] = ("ضغط PDF", "Compress PDF"),
		["tool.compress-pdf-strong"] = ("ضغط قوي", "Strong compress"),
		["tool.background"] = ("خلفية وتصميم", "Background"),
		["tool.rotate"] = ("تدوير", "Rotate"),
		["tool.zip"] = ("ضغط ZIP", "Zip"),
		["tool.tar"] = ("إنشاء TAR", "Create TAR"),
		["tool.gzip"] = ("إنشاء GZIP", "Create GZIP"),
		["tool.extract"] = ("فك الضغط", "Extract"),
		["tool.merge"] = ("دمج PDF", "Merge PDFs"),
		["tool.split"] = ("فصل الصفحات", "Split pages"),
		["tool.messaging"] = ("للمراسلة", "For messaging"),
		["tool.small-video"] = ("حجم صغير", "Small size"),
		["tool.audio-small"] = ("صوت خفيف", "Light audio"),
		["tool.fix-encoding"] = ("إصلاح الترميز", "Fix encoding"),
		["tool.excel-ready"] = ("جاهز لإكسل", "Excel-ready"),
		["tool.digits-latin"] = ("الأرقام إلى 123", "To Western digits"),
		["tool.digits-toggle"] = ("تحويل الأرقام", "Convert digits"),
		["tool.digits-arabic"] = ("أرقام ١٢٣", "Digits ١٢٣"),
		["tool.clean-arabic"] = ("تنظيف للبحث", "Clean for search"),
		["tool.video-mute"] = ("كتم الفيديو", "Mute video"),
		["tool.video-metadata"] = ("حذف بيانات الفيديو", "Strip metadata"),
		["tool.video-compress"] = ("ضغط الفيديو المحسن", "Video compress"),
		["tool.video-trim"] = ("قص زمني", "Trim video"),
		["tool.video-crop"] = ("قص الإطار", "Crop video"),
		["tool.video-speed"] = ("تغيير السرعة", "Change speed"),
		["tool.video-snapshot"] = ("التقاط صورة", "Snapshot"),
		["tool.video-split"] = ("تقسيم الفيديو", "Split video"),
		["tool.video-redact"] = ("حجب جزء من الفيديو", "Redact video"),
		["tool.audio-metadata"] = ("حذف بيانات الصوت", "Strip metadata"),
		["tool.audio-normalize"] = ("ضبط الصوت", "Normalize volume"),
		["tool.audio-visualize"] = ("موجة صوتية", "Visualize audio"),
		["tool.audio-trim"] = ("قص مقطع صوتي", "Trim audio"),
		["tool.audio-channels"] = ("قنوات الصوت", "Audio channels"),
		["tool.audio-bleep"] = ("كتم مقطع", "Bleep segment"),
		["tool.image-redact"] = ("حجب منطقة", "Redact image"),
		["tool.image-crop"] = ("قص محدد", "Crop region"),
		["tool.image-edit"] = ("تعديل الصورة", "Edit image"),
		["tool.image-annotate"] = ("كتابة ورسم", "Annotate image"),
		["tool.extract-rar"] = ("استخراج RAR / 7Z", "Extract RAR / 7Z"),
		["action.float"] = ("رف ملفات عائم", "Floating shelf"),
		["action.share-windows"] = ("مشاركة ويندوز", "Windows share"),
		["action.share-android"] = ("Quick Share", "Quick Share"),
		["tool.image-metadata"] = ("معلومات الصورة", "Image metadata"),
		["tool.pdf-metadata"] = ("بيانات PDF", "PDF metadata"),
		["tool.pdf-organize"] = ("تنظيم صفحات", "Organize pages"),
		["action.macros"] = ("التسجيلات", "Recordings"),
		["action.copy-path"] = ("نسخ المسار", "Copy path"),
		["action.copy-files"] = ("نسخ الملف", "Copy file"),
		["action.copy-to"] = ("نسخ إلى…", "Copy to…"),
		["action.move-to"] = ("نقل إلى…", "Move to…"),
		["action.terminal"] = ("Terminal هنا", "Terminal here"),
		["action.show"] = ("إظهار في المجلد", "Show in folder")
	};

	private static readonly string[] Actions = new string[] { "action.copy-path", "action.copy-files", "action.copy-to", "action.move-to", "action.terminal", "action.show", "action.float", "action.share-android", "action.macros" };

	private static readonly string[] TextTools = new string[3] { "tool.fix-encoding", "tool.digits-toggle", "tool.clean-arabic" };

	public static string CaptionAr(WheelMode mode)
	{
		return mode switch
		{
			WheelMode.Convert => "تحويل", 
			WheelMode.Tools => "أدوات", 
			_ => "إجراءات", 
		};
	}

	public static string CaptionEn(WheelMode mode)
	{
		return mode switch
		{
			WheelMode.Convert => "Convert", 
			WheelMode.Tools => "Tools", 
			_ => "Actions", 
		};
	}

	public static WheelAction Action(string id)
	{
		if (_macroActions.FirstOrDefault(a => a.Id == id) is WheelAction macro) return macro;
		if (id.StartsWith("menu.more", StringComparison.Ordinal)) return new WheelAction(id, "المزيد…", "More…");
		if (id.StartsWith("convert.", StringComparison.Ordinal))
		{
			string text = id.Substring("convert.".Length).ToUpperInvariant();
			return new WheelAction(id, text, text);
		}
		string labelAr;
		string labelEn;
		if (Labels.TryGetValue(id, out (string, string) value))
		{
			(labelAr, labelEn) = value;
		}
		else
		{
			labelEn = id;
			labelAr = id;
		}
		return new WheelAction(id, labelAr, labelEn);
	}

	public static WheelMenu For(WheelMode mode, FileKind kind, string? sourceExtension = null, Func<string, bool>? isAvailable = null, int count = 1)
	{
		if (kind == FileKind.Pending)
		{
			return new WheelMenu(Pending, Enumerable.Repeat(Pending, 6).ToArray());
		}
		string source = NormalizeExtension(sourceExtension);
		IEnumerable<string> source2;
		if (count <= 1)
		{
			IEnumerable<string> enumerable = Candidates(mode, kind);
			source2 = enumerable;
		}
		else
		{
			source2 = MergeFirst(mode, kind).Concat(Candidates(mode, kind));
		}
		WheelAction[] all = (from id in source2
			where !IsSameFormat(id, source)
			where isAvailable?.Invoke(id) ?? true
			select id).Distinct().Select(Action)
			.ToArray();
        // Tools/Ctrl: allow exactly two additional tools compared with v1.3
        // (9 direct tools + More). Space/Actions remains unchanged (Quick Share).
        // Compact, legible text layout for 9-10 slices is handled in WheelRenderer.
        int maxVisible = mode == WheelMode.Tools ? 10 : 12;
        WheelAction[] array = all.Length <= maxVisible
            ? all
            : all.Take(maxVisible - 1).Append(Action("menu.more." + mode + "." + kind)).ToArray();
		if (array.Length != 0)
		{
			return new WheelMenu(Ready, array);
		}
		return new WheelMenu(Nothing, Array.Empty<WheelAction>());
	}

	public static WheelAction[] ExtraActions(WheelMode mode, FileKind kind, string? sourceExt, Func<string,bool> available, int count)
	{
		string source = NormalizeExtension(sourceExt);
		IEnumerable<string> ids = count > 1 ? MergeFirst(mode, kind).Concat(Candidates(mode, kind)) : Candidates(mode, kind);
		return ids.Where(id => !IsSameFormat(id, source) && available(id)).Distinct().Skip(mode == WheelMode.Tools ? 9 : 11).Select(Action).ToArray();
	}

	public static IEnumerable<string> AllActionIds()
	{
		return Enum.GetValues<WheelMode>().SelectMany((WheelMode m) => Enum.GetValues<FileKind>().SelectMany((FileKind k) => Candidates(m, k))).Distinct();
	}

	private static IEnumerable<string> MergeFirst(WheelMode mode, FileKind kind)
	{
		if (mode != WheelMode.Actions)
		{
			return kind switch
			{
				FileKind.Image => (IEnumerable<string>)new _003C_003Ez__ReadOnlySingleElementList<string>("tool.images-to-pdf"), 
				FileKind.Pdf => new _003C_003Ez__ReadOnlySingleElementList<string>("tool.merge"), 
				FileKind.Mixed => new _003C_003Ez__ReadOnlySingleElementList<string>("tool.merge-all"), 
				_ => Array.Empty<string>(), 
			};
		}
		return Array.Empty<string>();
	}

	private static string[] Convert(params string[] formats)
	{
		return formats.Select((string f) => "convert." + f).ToArray();
	}

	private static IEnumerable<string> Candidates(WheelMode mode, FileKind kind)
	{
		switch (mode)
		{
		case WheelMode.Actions:
			return Actions.Concat(_macroActions.Select(m => m.Id));
		case WheelMode.Convert:
			return kind switch
			{
				FileKind.Image => (IEnumerable<string>)Convert("jpg", "png", "pdf", "tiff", "bmp", "gif", "webp", "avif", "heic"), 
				FileKind.Pdf => Convert("jpg", "png", "txt", "md", "docx", "pptx", "xlsx", "epub"), 
				FileKind.Video => Convert("mp4", "mp3", "m4a", "wav", "wmv", "mkv", "webm", "avi", "gif"), 
				FileKind.Audio => Convert("mp3", "m4a", "wav", "flac", "wma", "ogg", "opus", "aiff"), 
				FileKind.Document => Convert("pdf", "txt", "html", "md", "epub", "jpg", "png"), 
				FileKind.Slides => Convert("pdf", "png", "jpg"), 
				FileKind.Sheet => Convert("xlsx", "csv", "tsv", "pdf", "ods"), 
				FileKind.Subtitle => Convert("srt", "vtt", "txt"), 
				FileKind.Archive => new string[2] { "tool.extract", "tool.extract-rar" }, 
				FileKind.Text => TextTools, 
				FileKind.Mixed => new string[2] { "tool.merge-all", "tool.zip" }, 
				_ => new string[1] { "tool.zip" }, 
			};
		default:
		{
			string[] result;
			switch (kind)
			{
			case FileKind.Image:
				result = new string[15] { "tool.compress", "tool.background", "tool.half-size", "tool.strip-metadata", "tool.rotate", "tool.images-to-pdf", "tool.collage", "tool.zip", "tool.tar", "tool.gzip", "tool.image-redact", "tool.image-crop", "tool.image-edit", "tool.image-annotate", "tool.image-metadata" };
				break;
			case FileKind.Pdf:
				result = new string[10] { "tool.compress-pdf", "tool.compress-pdf-strong", "tool.background", "tool.split", "tool.merge", "tool.zip", "tool.tar", "tool.gzip", "tool.pdf-metadata", "tool.pdf-organize" };
				break;
			case FileKind.Video:
				result = new string[15] { "tool.messaging", "tool.small-video", "convert.m4a", "tool.zip", "tool.tar", "tool.gzip", "tool.video-compress", "tool.video-metadata", "tool.video-mute", "tool.video-trim", "tool.video-crop", "tool.video-speed", "tool.video-snapshot", "tool.video-split", "tool.video-redact" };
				break;
			case FileKind.Audio:
				result = new string[10] { "tool.audio-small", "tool.zip", "tool.tar", "tool.gzip", "tool.audio-metadata", "tool.audio-normalize", "tool.audio-visualize", "tool.audio-trim", "tool.audio-channels", "tool.audio-bleep" };
				break;
			case FileKind.Subtitle:
				result = TextTools;
				break;
			case FileKind.Sheet:
			{
				string text = "tool.excel-ready";
				string[] textTools = TextTools;
				int num = 0;
				string[] array = new string[1 + textTools.Length];
				array[num] = text;
				num++;
				ReadOnlySpan<string> readOnlySpan = new ReadOnlySpan<string>(textTools);
				readOnlySpan.CopyTo(new Span<string>(array).Slice(num, readOnlySpan.Length));
				num += readOnlySpan.Length;
				result = array;
				break;
			}
			case FileKind.Text:
				result = TextTools;
				break;
			case FileKind.Archive:
				result = new string[4] { "tool.extract", "tool.tar", "tool.gzip", "tool.extract-rar" };
				break;
			case FileKind.Mixed:
				result = new string[3] { "tool.merge-all", "tool.zip", "tool.tar" };
				break;
			default:
				result = new string[3] { "tool.zip", "tool.tar", "tool.gzip" };
				break;
			}
			return result;
		}
		}
	}

	private static string? NormalizeExtension(string? ext)
	{
		string text = ext?.ToLowerInvariant();
		if (text != null && (text == null || text.Length != 0))
		{
			if (!(text == ".jpeg"))
			{
				if (text == ".tif")
				{
					return ".tiff";
				}
				return text;
			}
			return ".jpg";
		}
		return null;
	}

	private static bool IsSameFormat(string id, string? source)
	{
		if (source != null && id.StartsWith("convert.", StringComparison.Ordinal))
		{
			return "." + id.Substring("convert.".Length) == source;
		}
		return false;
	}
}
