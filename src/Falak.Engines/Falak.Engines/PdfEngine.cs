using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Falak.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Falak.Engines;

internal static class PdfEngine
{
	private const double RenderDpi = 200.0;

	public static async Task<JobResult> ToImages(IReadOnlyList<string> inputs, string extension, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress steps = new StepProgress(progress, inputs.Count);
		List<string> outputs = new List<string>();
		string folder = null;
		foreach (string input in inputs)
		{
			Windows.Data.Pdf.PdfDocument document = await Open(input);
			uint pages = document.PageCount;
			string pageFolder = ((pages > 1) ? OutputPaths.NewFolderNextTo(input, "صفحات") : null);
			if (folder == null)
			{
				folder = pageFolder;
			}
			int digits = Math.Max(2, pages.ToString().Length);
			for (uint i = 0u; i < pages; i++)
			{
				ct.ThrowIfCancellationRequested();
				Windows.Data.Pdf.PdfPage page = document.GetPage(i);
				try
				{
					PdfPageRenderOptions val = new PdfPageRenderOptions();
					Size size = page.Size;
					val.DestinationWidth = (uint)Math.Max(1.0, Math.Round(size.Width * 200.0 / 96.0));
					Color backgroundColor = default;
					backgroundColor.A = byte.MaxValue;
					backgroundColor.R = byte.MaxValue;
					backgroundColor.G = byte.MaxValue;
					backgroundColor.B = byte.MaxValue;
					val.BackgroundColor = backgroundColor;
					val.BitmapEncoderId = ((extension == ".png") ? BitmapEncoder.PngEncoderId : BitmapEncoder.JpegEncoderId);
					PdfPageRenderOptions val2 = val;
					InMemoryRandomAccessStream rendered = new InMemoryRandomAccessStream();
					try
					{
						await WindowsRuntimeSystemExtensions.AsTask(
							page.RenderToStreamAsync((IRandomAccessStream)(object)rendered, val2));
						string destination = ((pageFolder == null) ? OutputPaths.NextTo(input, extension) : OutputPaths.Unique(pageFolder, "صفحة " + (i + 1).ToString().PadLeft(digits, '0'), extension));
						await OutputPaths.WriteAtomicallyAsync(destination, async (string temp) =>
						{
							rendered.Seek(0uL);
							await using FileStream file = File.Create(temp);
							await WindowsRuntimeStreamExtensions.AsStreamForRead((IInputStream)(object)rendered).CopyToAsync(file, ct);
						});
						outputs.Add(destination);
						steps.Item(((double)i + 1.0) / (double)pages);
					}
					finally
					{
						if (rendered != null)
						{
							((IDisposable)rendered).Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)page)?.Dispose();
				}
			}
			steps.Next();
		}
		string message = Texts.Made(outputs.Count, "صورة واحدة", "صورتين", "صور") + " بدقة 200 نقطة/بوصة";
		return new JobResult(outputs, (inputs.Count == 1) ? folder : null, message);
	}

	private static async Task<Windows.Data.Pdf.PdfDocument> Open(string path)
	{
		try
		{
			StorageFile file = await WindowsRuntimeSystemExtensions.AsTask(
				StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)));
			return await WindowsRuntimeSystemExtensions.AsTask(
				Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(file));
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			throw new UserFacingException(
				(ex.HResult == -2147023573 || ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
				? $"الملف «{Path.GetFileName(path)}» محمي بكلمة مرور. افتحه واحفظ نسخة بلا كلمة مرور ثم أعد المحاولة."
				: $"تعذّر فتح «{Path.GetFileName(path)}». قد يكون الملف تالفاً.", ex);
		}
	}

	public static JobResult ToText(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		int num = 0;
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			StringBuilder text = new StringBuilder();
			try
			{
				UglyToad.PdfPig.PdfDocument val = UglyToad.PdfPig.PdfDocument.Open(input, (ParsingOptions)null);
				try
				{
					int numberOfPages = val.NumberOfPages;
					foreach (Page page in val.GetPages())
					{
						ct.ThrowIfCancellationRequested();
						if (numberOfPages > 1)
						{
							StringBuilder stringBuilder = text;
							StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder);
							handler.AppendLiteral("— صفحة ");
							handler.AppendFormatted(page.Number);
							handler.AppendLiteral(" —");
							stringBuilder.AppendLine(ref handler);
						}
						string text2 = ContentOrderTextExtractor.GetText(page, false);
						text.AppendLine(ArabicText.FoldPresentationForms(text2)).AppendLine();
						stepProgress.Item((double)page.Number / (double)numberOfPages);
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			catch (Exception ex) when (!(ex is OperationCanceledException) && !(ex is UserFacingException))
			{
				throw new UserFacingException("تعذ\u0651ر قراءة النص من «" + Path.GetFileName(input) + "». قد يكون محميا\u064b أو تالفا\u064b.", ex);
			}
			if (text.ToString().Count(char.IsLetterOrDigit) < 10)
			{
				num++;
				stepProgress.Next();
				continue;
			}
			string text3 = OutputPaths.NextTo(input, ".txt");
			OutputPaths.WriteAtomically(text3, (string temp) =>
			{
				File.WriteAllText(temp, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			});
			list.Add(text3);
			stepProgress.Next();
		}
		if (list.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "لا يوجد نص قابل للنسخ في الملف. غالبا\u064b هو صور ممسوحة ضوئيا\u064b.");
		}
		string text4 = ((num > 0) ? $" · {num} بلا نص" : "");
		return new JobResult(list, null, "استخرجنا النص (قد يحتاج تنسيق الجداول مراجعة)" + text4);
	}

	public static JobResult Merge(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected Obj, but got Unknown
		List<string> list = inputs.Order(NaturalComparer.Instance).ToList();
		StepProgress stepProgress = new StepProgress(progress, list.Count);
		PdfSharp.Pdf.PdfDocument val = new PdfSharp.Pdf.PdfDocument();
		try
		{
			foreach (string item in list)
			{
				ct.ThrowIfCancellationRequested();
				PdfSharp.Pdf.PdfDocument val2 = OpenForImport(item);
				try
				{
					foreach (PdfSharp.Pdf.PdfPage page in val2.Pages)
					{
						val.AddPage(page);
					}
					stepProgress.Next();
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			int pageCount = val.PageCount;
			string text = OutputPaths.NextTo(list[0], ".pdf", "مدموج");
			OutputPaths.WriteAtomically(text, (Action<string>)val.Save);
			return new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(text), null, $"دمجنا {Texts.Files(list.Count)} في ملف واحد · {pageCount} صفحة");
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static JobResult Split(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected Obj, but got Unknown
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		string text = null;
		foreach (string input in inputs)
		{
			PdfSharp.Pdf.PdfDocument val = OpenForImport(input);
			try
			{
				int pageCount = val.PageCount;
				if (pageCount < 2)
				{
					stepProgress.Next();
					continue;
				}
				string text2 = OutputPaths.NewFolderNextTo(input, "صفحات منفصلة");
				if (text == null)
				{
					text = text2;
				}
				int totalWidth = Math.Max(2, pageCount.ToString().Length);
				for (int i = 0; i < pageCount; i++)
				{
					ct.ThrowIfCancellationRequested();
					PdfSharp.Pdf.PdfDocument val2 = new PdfSharp.Pdf.PdfDocument();
					try
					{
						val2.AddPage(val.Pages[i]);
						string text3 = OutputPaths.Unique(text2, "صفحة " + (i + 1).ToString().PadLeft(totalWidth, '0'), ".pdf");
						OutputPaths.WriteAtomically(text3, (Action<string>)val2.Save);
						list.Add(text3);
						stepProgress.Item(((double)i + 1.0) / (double)pageCount);
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				stepProgress.Next();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (list.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "الملف صفحة واحدة، لا يوجد ما نفصله.");
		}
		return new JobResult(list, (inputs.Count == 1) ? text : null, Texts.Made(list.Count, "ملف PDF واحد", "ملفي PDF", "ملفات PDF") + "، صفحة في كل ملف");
	}

	public static JobResult MergeAll(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Expected Obj, but got Unknown
		List<string> list = inputs.Order(NaturalComparer.Instance).ToList();
		StepProgress stepProgress = new StepProgress(progress, list.Count);
		PdfSharp.Pdf.PdfDocument val = new PdfSharp.Pdf.PdfDocument();
		try
		{
			foreach (string item in list)
			{
				ct.ThrowIfCancellationRequested();
				if (Path.GetExtension(item).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
				{
					PdfSharp.Pdf.PdfDocument val2 = OpenForImport(item);
					try
					{
						foreach (PdfSharp.Pdf.PdfPage page in val2.Pages)
						{
							val.AddPage(page);
						}
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				else
				{
					byte[] jpeg = ImageEngine.ToJpeg(item, out var width, out var height);
					AddImagePage(val, jpeg, width, height);
				}
				stepProgress.Next();
			}
			int pageCount = val.PageCount;
			string text = OutputPaths.NextTo(list[0], ".pdf", "مدموج");
			OutputPaths.WriteAtomically(text, (Action<string>)val.Save);
			return new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(text), null, $"دمجنا {Texts.Files(list.Count)} في ملف PDF واحد · {pageCount} صفحة");
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public static JobResult ImagesToPdf(IReadOnlyList<string> inputs, bool combine, IProgress<double>? progress, CancellationToken ct)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected Obj, but got Unknown
		List<string> list = inputs.Order(NaturalComparer.Instance).ToList();
		StepProgress stepProgress = new StepProgress(progress, list.Count);
		List<string> list2 = new List<string>();
		PdfSharp.Pdf.PdfDocument val = null;
		foreach (string item in list)
		{
			ct.ThrowIfCancellationRequested();
			if (val == null)
			{
				val = new PdfSharp.Pdf.PdfDocument();
			}
			byte[] jpeg = ImageEngine.ToJpeg(item, out var width, out var height);
			AddImagePage(val, jpeg, width, height);
			if (!combine)
			{
				list2.Add(SaveNew(val, OutputPaths.NextTo(item, ".pdf")));
				val = null;
			}
			stepProgress.Next();
		}
		if (combine && val != null)
		{
			list2.Add(SaveNew(val, OutputPaths.NextTo(list[0], ".pdf", (list.Count > 1) ? "مجم\u0651ع" : null)));
		}
		string message = ((combine && list.Count > 1) ? $"جمعنا {list.Count} صور في ملف PDF واحد" : Texts.Made(list2.Count, "ملف PDF واحد", "ملفي PDF", "ملفات PDF"));
		return new JobResult(list2, null, message);
	}

	private static string SaveNew(PdfSharp.Pdf.PdfDocument document, string destination)
	{
		try
		{
			OutputPaths.WriteAtomically(destination, (Action<string>)document.Save);
			return destination;
		}
		finally
		{
			((IDisposable)document)?.Dispose();
		}
	}

	private static void AddImagePage(PdfSharp.Pdf.PdfDocument document, byte[] jpeg, int width, int height)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		bool flag = width > height;
		PdfSharp.Pdf.PdfPage val = document.AddPage();
		val.Width = XUnit.FromPoint(flag ? 841.89 : 595.28);
		val.Height = XUnit.FromPoint(flag ? 595.28 : 841.89);
		XUnit val2 = val.Width;
		double num = val2.Point - 48.0;
		val2 = val.Height;
		double num2 = Math.Min(val2: (val2.Point - 48.0) / (double)height, val1: num / (double)width);
		double num3 = (double)width * num2;
		double num4 = (double)height * num2;
		XGraphics val3 = XGraphics.FromPdfPage(val);
		try
		{
			using MemoryStream memoryStream = new MemoryStream(jpeg);
			XImage val4 = XImage.FromStream((Stream)memoryStream);
			try
			{
				val2 = val.Width;
				double num5 = (val2.Point - num3) / 2.0;
				val2 = val.Height;
				val3.DrawImage(val4, num5, (val2.Point - num4) / 2.0, num3, num4);
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
	}

	private static PdfSharp.Pdf.PdfDocument OpenForImport(string path)
	{
		try
		{
			return PdfReader.Open(path, (PdfDocumentOpenMode)1, (PdfReaderOptions)null);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			throw new UserFacingException("تعذ\u0651ر فتح «" + Path.GetFileName(path) + "». قد يكون محميا\u064b بكلمة مرور أو تالفا\u064b.", ex);
		}
	}
}
