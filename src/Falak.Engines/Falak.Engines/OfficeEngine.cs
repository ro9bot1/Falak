using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.Engines;

internal static class OfficeEngine
{
	public static bool HasWord => Has("Word.Application");

	public static bool HasPowerPoint => Has("PowerPoint.Application");

	public static bool HasExcel => Has("Excel.Application");

	public static bool Has(string progId)
	{
		return (object)Type.GetTypeFromProgID(progId) != null;
	}

	public static Task<JobResult> ToPdf(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		return Sta.Run(() =>
		{
			StepProgress stepProgress = new StepProgress(progress, inputs.Count);
			List<string> list = new List<string>();
			foreach (string input in inputs)
			{
				ct.ThrowIfCancellationRequested();
				string text = OutputPaths.NextTo(input, ".pdf");
				OutputPaths.WriteAtomically(text, (string temp) =>
				{
					string text2 = temp + ".pdf";
					try
					{
						Export(input, text2);
						File.Move(text2, temp, overwrite: true);
					}
					finally
					{
						if (File.Exists(text2))
						{
							File.Delete(text2);
						}
					}
				});
				list.Add(text);
				stepProgress.Next();
			}
			return new JobResult(list, null, Texts.Made(list.Count, "ملف PDF واحد", "ملفي PDF", "ملفات PDF") + " عبر Microsoft Office");
		}, ct);
	}

	public static Task<JobResult> SlidesToImages(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct, string extension = ".png")
	{
		return Sta.Run(() =>
		{
			StepProgress steps = new StepProgress(progress, inputs.Count);
			List<string> outputs = new List<string>();
			string text = null;
			foreach (string input in inputs)
			{
				ct.ThrowIfCancellationRequested();
				string slideFolder = OutputPaths.NewFolderNextTo(input, "شرائح");
				if (text == null)
				{
					text = slideFolder;
				}
				WithPowerPoint(input, (dynamic presentation) =>
				{
					int num = presentation.Slides.Count;
					int totalWidth = Math.Max(2, num.ToString().Length);
					for (int i = 1; i <= num; i++)
					{
						ct.ThrowIfCancellationRequested();
						string text2 = OutputPaths.Unique(slideFolder, "شريحة " + i.ToString().PadLeft(totalWidth, '0'), extension);
						presentation.Slides[i].Export(text2, extension == ".jpg" ? "JPG" : "PNG", 1920, 1080);
						outputs.Add(text2);
						steps.Item((double)i / (double)num);
					}
				});
				steps.Next();
			}
			return new JobResult(outputs, (inputs.Count == 1) ? text : null, Texts.Made(outputs.Count, "صورة واحدة", "صورتين", "صور") + " بدقة 1920×1080");
		}, ct);
	}

	private static void Export(string input, string pdf)
	{
		string text = Path.GetExtension(input).ToLowerInvariant();
		try
		{
			if (text != null)
			{
				int length = text.Length;
				if (length != 4)
				{
					if (length == 5)
					{
						char c = text[1];
						if (c != 'p')
						{
							if (c == 'x' && text == ".xlsx")
							{
								goto IL_00f3;
							}
						}
						else if (text == ".pptx")
						{
							goto IL_00df;
						}
					}
				}
				else
				{
					char c = text[1];
					if ((uint)c <= 111u)
					{
						if (c != 'c')
						{
							if (c == 'o')
							{
								if (text == ".odp")
								{
									goto IL_00df;
								}
								if (text == ".ods")
								{
									goto IL_00f3;
								}
							}
						}
						else if (text == ".csv")
						{
							goto IL_00f3;
						}
					}
					else if (c != 'p')
					{
						if (c == 'x' && text == ".xls")
						{
							goto IL_00f3;
						}
					}
					else if (text == ".ppt")
					{
						goto IL_00df;
					}
				}
			}
			WithWord(input, (dynamic document) =>
			{
				document.ExportAsFixedFormat(pdf, 17);
			});
			return;
			IL_00f3:
			WithExcel(input, (dynamic workbook) =>
			{
				workbook.ExportAsFixedFormat(0, pdf);
			});
			return;
			IL_00df:
			WithPowerPoint(input, (dynamic presentation) =>
			{
				presentation.SaveAs(pdf, 32);
			});
		}
		catch (COMException inner)
		{
			throw new UserFacingException("لم يتمكن Office من فتح «" + Path.GetFileName(input) + "». إن كان مفتوحا\u064b ويطلب كلمة مرور فأدخلها أولا\u064b.", inner);
		}
	}

	private static dynamic Create(string progId)
	{
		return Activator.CreateInstance(Type.GetTypeFromProgID(progId));
	}

	private static void WithWord(string path, Action<dynamic> use)
	{
		dynamic val = Create("Word.Application");
		bool flag = val.Documents.Count > 0;
		dynamic val2 = null;
		try
		{
			val.DisplayAlerts = 0;
			val2 = val.Documents.Open(path, false, true, false);
			((Action<object>)use)(val2);
		}
		finally
		{
			val2?.Close(0);
			if (!flag && val.Documents.Count == 0)
			{
				val.Quit(0);
			}
			Marshal.FinalReleaseComObject(val);
		}
	}

	private static void WithPowerPoint(string path, Action<dynamic> use)
	{
		dynamic val = Create("PowerPoint.Application");
		bool flag = val.Presentations.Count > 0;
		dynamic val2 = null;
		try
		{
			val2 = val.Presentations.Open(path, -1, 0, 0);
			((Action<object>)use)(val2);
		}
		finally
		{
			val2?.Close();
			if (!flag && val.Presentations.Count == 0)
			{
				val.Quit();
			}
			Marshal.FinalReleaseComObject(val);
		}
	}

	private static void WithExcel(string path, Action<dynamic> use)
	{
		dynamic val = Create("Excel.Application");
		bool flag = val.Workbooks.Count > 0;
		dynamic val2 = null;
		try
		{
			val.DisplayAlerts = false;
			val2 = val.Workbooks.Open(path, 0, true);
			((Action<object>)use)(val2);
		}
		finally
		{
			val2?.Close(false);
			if (!flag && val.Workbooks.Count == 0)
			{
				val.Quit();
			}
			Marshal.FinalReleaseComObject(val);
		}
	}
}
