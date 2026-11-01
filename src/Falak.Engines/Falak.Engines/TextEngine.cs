using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ClosedXML.Excel;
using Falak.Core;

namespace Falak.Engines;

internal static class TextEngine
{
	private static readonly UTF8Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

	private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static bool WantsBom(string extension)
	{
		if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
		{
			return extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static ArabicText.Decoded Read(string path)
	{
		return ArabicText.Decode(File.ReadAllBytes(path));
	}

	private static string WriteText(string source, string extension, string? suffix, string text)
	{
		string text2 = OutputPaths.NextTo(source, extension, suffix);
		OutputPaths.WriteAtomically(text2, (string temp) =>
		{
			File.WriteAllText(temp, text, WantsBom(extension) ? Utf8Bom : Utf8);
		});
		return text2;
	}

	private static JobResult PerFile(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct, Func<string, ArabicText.Decoded, (string Extension, string? Suffix, string? Text)> transform, string doneMessage, string nothingMessage)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			var (extension, suffix, text) = transform(input, Read(input));
			if (text != null)
			{
				list.Add(WriteText(input, extension, suffix, text));
			}
			stepProgress.Next();
		}
		return new JobResult(list, null, (list.Count > 0) ? doneMessage : nothingMessage);
	}

	public static JobResult FixEncoding(IReadOnlyList<string> inputs, IProgress<double>? p, CancellationToken ct)
	{
		return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) => (Extension: Path.GetExtension(input), Suffix: "م\u064fصلح", Text: decoded.WasUtf8 ? null : decoded.Text), "أصلحنا الترميز: كان Windows-1256 وصار UTF-8 وي\u064fقرأ في كل مكان", "الملف سليم أصلا\u064b (UTF-8)، لم يتغير شيء.");
	}

	public static JobResult ExcelReady(IReadOnlyList<string> inputs, IProgress<double>? p, CancellationToken ct)
	{
		return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) => (Extension: Path.GetExtension(input), Suffix: "إكسل", Text: decoded.Text), "جاهز: افتحه في إكسل وستظهر العربية صحيحة", "لم يتغير شيء.");
	}

	public static JobResult Digits(IReadOnlyList<string> inputs, bool toLatin, IProgress<double>? p, CancellationToken ct)
	{
		return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) =>
		{
			string text = (toLatin ? ArabicText.ToLatinDigits(decoded.Text) : ArabicText.ToArabicDigits(decoded.Text));
			return (Extension: Path.GetExtension(input), Suffix: toLatin ? "أرقام 123" : "أرقام ١٢٣", Text: (text == decoded.Text) ? null : text);
		}, toLatin ? "حو\u0651لنا الأرقام إلى 123" : "حو\u0651لنا الأرقام إلى ١٢٣", "لا توجد أرقام تحتاج تحويلا\u064b.");
	}

    // One wheel action; detect the source numeral set and convert it in the opposite direction.
    // Existing explicit conversions remain supported for callers of the older API.
    public static JobResult DigitsAuto(IReadOnlyList<string> inputs, IProgress<double>? p, CancellationToken ct)
    {
        return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) =>
        {
            bool hasArabicDigits = decoded.Text.Any(c => (c >= '\u0660' && c <= '\u0669') ||
                                                       (c >= '\u06f0' && c <= '\u06f9'));
            string converted = hasArabicDigits ? ArabicText.ToLatinDigits(decoded.Text) : ArabicText.ToArabicDigits(decoded.Text);
            return (Path.GetExtension(input), hasArabicDigits ? "Latin digits" : "Arabic digits",
                    converted == decoded.Text ? null : converted);
        }, "Digits converted / تم تحويل الأرقام", "No digits require conversion / لا توجد أرقام للتحويل");
    }

	public static JobResult CleanArabic(IReadOnlyList<string> inputs, IProgress<double>? p, CancellationToken ct)
	{
		return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) =>
		{
			string text = ArabicText.CleanForSearch(decoded.Text);
			return (Extension: Path.GetExtension(input), Suffix: "للبحث", Text: (text == decoded.Text) ? null : text);
		}, "نسخة بلا تشكيل وبأحرف موح\u0651دة، مناسبة للبحث والمطابقة", "النص نظيف أصلا\u064b، لم يتغير شيء.");
	}

	public static JobResult ConvertSubtitles(IReadOnlyList<string> inputs, string target, IProgress<double>? p, CancellationToken ct)
	{
		return PerFile(inputs, p, ct, (string input, ArabicText.Decoded decoded) =>
		{
			string text = Path.GetExtension(input).ToLowerInvariant();
			string text2;
			if (!(text == ".srt"))
			{
				if (!(text == ".vtt") || !(target == ".srt"))
				{
					goto IL_004e;
				}
				text2 = Subtitles.VttToSrt(decoded.Text);
			}
			else
			{
				if (!(target == ".vtt"))
				{
					goto IL_004e;
				}
				text2 = Subtitles.SrtToVtt(decoded.Text);
			}
			goto IL_0093;
			IL_004e:
			text2 = ((!(target == ".txt")) ? decoded.Text : Subtitles.ToPlainText(decoded.Text));
			goto IL_0093;
			IL_0093:
			string item = text2;
			return ((string Extension, string? Suffix, string? Text))(Extension: target, Suffix: null, Text: item);
		}, (target == ".txt") ? "استخرجنا النص بلا توقيتات" : "حو\u0651لنا الترجمة بترميز UTF-8", "لم يتغير شيء.");
	}

	public static JobResult ConvertTable(IReadOnlyList<string> inputs, string target, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			List<IReadOnlyList<string>> rows = ReadRows(input);
			string text = OutputPaths.NextTo(input, target);
			if (target == ".xlsx")
			{
				OutputPaths.WriteAtomically(text, (string temp) =>
				{
					WriteWorkbook(rows, Path.GetFileNameWithoutExtension(input), temp);
				});
			}
			else
			{
				OutputPaths.WriteAtomically(text, (string temp) =>
				{
					File.WriteAllText(temp, Csv.Write(rows, (target == ".tsv") ? '\t' : ','), Utf8Bom);
				});
			}
			list.Add(text);
			stepProgress.Next();
		}
		string message = ((target == ".xlsx") ? "أنشأنا ملف إكسل من اليمين لليسار" : ("حو\u0651لنا الجدول إلى " + target.TrimStart('.').ToUpperInvariant() + " بترميز يفهمه إكسل"));
		return new JobResult(list, null, message);
	}

	private static List<IReadOnlyList<string>> ReadRows(string path)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected Obj, but got Unknown
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text == ".xlsx")
		{
			try
			{
				XLWorkbook val = new XLWorkbook(path);
				try
				{
					IXLRange val2 = ((IEnumerable<IXLWorksheet>)val.Worksheets).First().RangeUsed();
					if (val2 == null)
					{
						return new List<IReadOnlyList<string>>();
					}
					return ((IEnumerable<IXLRangeRow>)val2.Rows((Func<IXLRangeRow, bool>)null)).Select((Func<IXLRangeRow, IReadOnlyList<string>>)((IXLRangeRow r) => ((IEnumerable<IXLCell>)((IXLRangeBase)r).Cells()).Select((IXLCell c) => c.GetFormattedString((CultureInfo)null)).ToList())).ToList();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				throw new UserFacingException("تعذ\u0651ر قراءة «" + Path.GetFileName(path) + "». إن كان مفتوحا\u064b في إكسل فأغلقه وأعد المحاولة.", ex);
			}
		}
		string text2 = Read(path).Text;
		return ((IEnumerable<List<string>>)Csv.Parse(text2, Csv.Sniff(text2, text))).Select((Func<List<string>, IReadOnlyList<string>>)((List<string> r) => r)).ToList();
	}

	private static void WriteWorkbook(List<IReadOnlyList<string>> rows, string name, string path)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected Obj, but got Unknown
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
		XLWorkbook val = new XLWorkbook();
		try
		{
			string text = new string(name.Where((char c) => !"[]:*?/\\".Contains(c)).ToArray());
			if (text.Length == 0)
			{
				text = "ورقة1";
			}
			IXLWorksheet val2 = val.AddWorksheet((text.Length > 31) ? text.Substring(0, 31) : text);
			bool flag = false;
			for (int num = 0; num < rows.Count; num++)
			{
				for (int num2 = 0; num2 < rows[num].Count; num2++)
				{
					string text2 = rows[num][num2];
					flag |= ArabicText.ContainsArabic(text2);
					IXLCell val3 = val2.Cell(num + 1, num2 + 1);
					if (text2.Length > 0 && text2.Length < 16 && (text2.Length <= 1 || text2[0] != '0' || text2[1] == '.') && double.TryParse(ArabicText.ToLatinDigits(text2), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						val3.Value = result;
					}
					else
					{
						val3.Value = text2;
					}
				}
			}
			val2.RightToLeft = flag;
			if (rows.Count > 1)
			{
				((IXLFontBase)((IXLRangeBase)val2.Row(1)).Style.Font).Bold = true;
				val2.SheetView.FreezeRows(1);
			}
			val2.Columns().AdjustToContents(1, Math.Min(rows.Count, 500));
			using FileStream fileStream = File.Create(path);
			val.SaveAs((Stream)fileStream);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}
}
