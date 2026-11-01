using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Filters;
using PdfSharp.Pdf.IO;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Falak.Engines;

internal static class PdfCompressEngine
{
	private const int MaxImageSide = 1800;

	private const int SmartQuality = 62;

	private const double StrongDpi = 110.0;

	private const float StrongQuality = 0.55f;

	public static JobResult Smart(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		long before = 0L;
		long after = 0L;
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			PdfSharp.Pdf.PdfDocument val;
			try
			{
				val = PdfReader.Open(input, (PdfDocumentOpenMode)0, (PdfReaderOptions)null);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				throw new UserFacingException("تعذ\u0651ر فتح «" + Path.GetFileName(input) + "». قد يكون محميا\u064b بكلمة مرور أو تالفا\u064b.", ex);
			}
			PdfSharp.Pdf.PdfDocument val2 = val;
			try
			{
				List<PdfDictionary> list2 = CollectImages(val);
				for (int i = 0; i < list2.Count; i++)
				{
					ct.ThrowIfCancellationRequested();
					TryRecompress(list2[i]);
					stepProgress.Item(((double)i + 1.0) / (double)Math.Max(1, list2.Count) * 0.9);
				}
				val.Options.CompressContentStreams = true;
				val.Options.FlateEncodeMode = (PdfFlateEncodeMode)2;
				val.Options.NoCompression = false;
				string destination = OutputPaths.NextTo(input, ".pdf", "مضغوط");
				OutputPaths.WriteAtomically(destination, (Action<string>)val.Save);
				Keep(input, destination, ref before, ref after, list);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			stepProgress.Next();
		}
		if (list.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "الملف مضغوط أصلا\u064b. جر\u0651ب «ضغط قوي» إن كان مستندا\u064b ممسوحا\u064b ضوئيا\u064b.");
		}
		return new JobResult(list, null, Saved(before, after) + " · النص بقي قابلا\u064b للنسخ");
	}

	public static async Task<JobResult> Strong(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress steps = new StepProgress(progress, inputs.Count);
		List<string> outputs = new List<string>();
		long before = 0L;
		long after = 0L;
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			StorageFile inputFile = await WindowsRuntimeSystemExtensions.AsTask(
				StorageFile.GetFileFromPathAsync(Path.GetFullPath(input)));
			Windows.Data.Pdf.PdfDocument source;
			try
			{
				source = await WindowsRuntimeSystemExtensions.AsTask(
					Windows.Data.Pdf.PdfDocument.LoadFromFileAsync(inputFile));
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				throw new UserFacingException($"تعذّر فتح «{Path.GetFileName(input)}». قد يكون محمياً بكلمة مرور أو تالفاً.", ex);
			}
			PdfSharp.Pdf.PdfDocument output = new PdfSharp.Pdf.PdfDocument();
			try
			{
				for (uint i = 0u; i < source.PageCount; i++)
				{
					ct.ThrowIfCancellationRequested();
					Windows.Data.Pdf.PdfPage page = source.GetPage(i);
					try
					{
						byte[] buffer = await RenderJpeg(page, 110.0, 0.55f);
						PdfSharp.Pdf.PdfPage val = output.AddPage();
						Size size = page.Size;
						val.Width = XUnit.FromPoint(size.Width * 0.75);
						size = page.Size;
						val.Height = XUnit.FromPoint(size.Height * 0.75);
						XGraphics val2 = XGraphics.FromPdfPage(val);
						try
						{
							using MemoryStream memoryStream = new MemoryStream(buffer);
							XImage val3 = XImage.FromStream((Stream)memoryStream);
							try
							{
								XUnit val4 = val.Width;
								double point = val4.Point;
								val4 = val.Height;
								val2.DrawImage(val3, 0.0, 0.0, point, val4.Point);
								steps.Item(((double)i + 1.0) / (double)source.PageCount);
							}
							finally
							{
								((IDisposable)val3)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val2)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)page)?.Dispose();
					}
				}
				output.Options.CompressContentStreams = true;
				string destination = OutputPaths.NextTo(input, ".pdf", "مضغوط بقوة");
				OutputPaths.WriteAtomically(destination, (Action<string>)output.Save);
				Keep(input, destination, ref before, ref after, outputs);
				steps.Next();
			}
			finally
			{
				((IDisposable)output)?.Dispose();
			}
		}
		if (outputs.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "لم نستطع تصغير الملف أكثر.");
		}
		return new JobResult(outputs, null, Saved(before, after) + " · الصفحات صارت صورا\u064b، والنص لم يعد قابلا\u064b للنسخ");
	}

	private static bool Keep(string input, string destination, ref long before, ref long after, List<string> outputs)
	{
		long length = new FileInfo(input).Length;
		long length2 = new FileInfo(destination).Length;
		if ((double)length2 >= (double)length * 0.97)
		{
			File.Delete(destination);
			return false;
		}
		before += length;
		after += length2;
		outputs.Add(destination);
		return true;
	}

	private static string Saved(long before, long after)
	{
		return $"وف\u0651رنا {(int)Math.Round(100.0 * (double)(before - after) / (double)before)}٪ · من {Texts.Size(before)} إلى {Texts.Size(after)}";
	}

	private static List<PdfDictionary> CollectImages(PdfSharp.Pdf.PdfDocument document)
	{
		List<PdfDictionary> found = new List<PdfDictionary>();
		HashSet<PdfDictionary> seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
		foreach (PdfSharp.Pdf.PdfPage page in document.Pages)
		{
			Walk(((PdfDictionary)page).Elements.GetDictionary("/Resources"), 0);
		}
		return found;
		void Walk(PdfDictionary? resources, int depth)
		{
			if (resources != null && depth <= 6)
			{
				PdfDictionary dictionary = resources.Elements.GetDictionary("/XObject");
				if (dictionary != null)
				{
					foreach (PdfItem value in dictionary.Elements.Values)
					{
						PdfItem obj = ((value is PdfReference) ? value : null);
						PdfObject obj2 = ((obj != null) ? ((PdfReference)obj).Value : null);
						PdfDictionary val = (PdfDictionary)(object)((obj2 is PdfDictionary) ? obj2 : null);
						if (val != null && seen.Add(val))
						{
							string name = val.Elements.GetName("/Subtype");
							if (name == "/Image")
							{
								found.Add(val);
							}
							else if (name == "/Form")
							{
								Walk(val.Elements.GetDictionary("/Resources"), depth + 1);
							}
						}
					}
				}
			}
		}
	}

	private static void TryRecompress(PdfDictionary image)
	{
		try
		{
			if (image.Stream == null || image.Elements.ContainsKey("/SMask") || image.Elements.ContainsKey("/Mask") || image.Elements.GetInteger("/BitsPerComponent") != 8)
			{
				return;
			}
			int integer = image.Elements.GetInteger("/Width");
			int integer2 = image.Elements.GetInteger("/Height");
			if (integer < 64 || integer2 < 64)
			{
				return;
			}
			int num = Channels(image);
			if ((num != 1 && num != 3) || 1 == 0)
			{
				return;
			}
			PdfItem val = image.Elements["/Filter"];
			PdfArray val2 = (PdfArray)(object)((val is PdfArray) ? val : null);
			string text = ((val2 != null && val2.Elements.Count == 1) ? val2.Elements.GetName(0) : image.Elements.GetName("/Filter"));
			byte[] value = image.Stream.Value;
			BitmapSource bitmapSource;
			if (text == "/DCTDecode")
			{
				bitmapSource = DecodeJpeg(value);
			}
			else
			{
				bitmapSource = ((!(text == "/FlateDecode")) ? null : DecodeRaw(image, integer, integer2, num));
			}
			BitmapSource bitmapSource2 = bitmapSource;
			if (bitmapSource2 == null)
			{
				return;
			}
			double num2 = Math.Min(1.0, 1800.0 / (double)Math.Max(bitmapSource2.PixelWidth, bitmapSource2.PixelHeight));
			if (num2 < 1.0)
			{
				bitmapSource2 = new TransformedBitmap(bitmapSource2, new ScaleTransform(num2, num2));
			}
			if (num == 1 && bitmapSource2.Format != PixelFormats.Gray8)
			{
				bitmapSource2 = new FormatConvertedBitmap(bitmapSource2, PixelFormats.Gray8, null, 0.0);
			}
			if (num == 3 && bitmapSource2.Format != PixelFormats.Bgr24 && bitmapSource2.Format != PixelFormats.Bgr32)
			{
				bitmapSource2 = new FormatConvertedBitmap(bitmapSource2, PixelFormats.Bgr24, null, 0.0);
			}
			JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder
			{
				QualityLevel = 62
			};
			jpegBitmapEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource2));
			using MemoryStream memoryStream = new MemoryStream();
			jpegBitmapEncoder.Save(memoryStream);
			byte[] array = memoryStream.ToArray();
			if (!((double)array.Length >= (double)value.Length * 0.9))
			{
				image.Stream.Value = array;
				image.Elements.SetName("/Filter", "/DCTDecode");
				image.Elements.Remove("/DecodeParms");
				image.Elements.SetInteger("/Width", bitmapSource2.PixelWidth);
				image.Elements.SetInteger("/Height", bitmapSource2.PixelHeight);
				image.Elements.SetName("/ColorSpace", (num == 1) ? "/DeviceGray" : "/DeviceRGB");
				image.Elements.SetInteger("/Length", array.Length);
			}
		}
		catch (Exception ex) when ((ex is NotSupportedException || ex is FileFormatException || ex is ArgumentException || ex is InvalidOperationException || ex is COMException) ? true : false)
		{
		}
	}

	private static int Channels(PdfDictionary image)
	{
		PdfItem val = image.Elements["/ColorSpace"];
		PdfReference val2 = (PdfReference)(object)((val is PdfReference) ? val : null);
		if (val2 != null)
		{
			val = (PdfItem)(object)val2.Value;
		}
		PdfName val3 = (PdfName)(object)((val is PdfName) ? val : null);
		if (val3 != null)
		{
			switch (val3.Value)
			{
			case "/DeviceRGB":
			case "/CalRGB":
				return 3;
			case "/DeviceGray":
			case "/CalGray":
				return 1;
			default:
				return 0;
			}
		}
		PdfArray val4 = (PdfArray)(object)((val is PdfArray) ? val : null);
		if (val4 != null && val4.Elements.Count >= 2 && val4.Elements.GetName(0) == "/ICCBased")
		{
			PdfObject val5 = val4.Elements.GetObject(1);
			PdfDictionary val6 = (PdfDictionary)(object)((val5 is PdfDictionary) ? val5 : null);
			if (val6 != null)
			{
				int integer = val6.Elements.GetInteger("/N");
				if ((integer != 1 && integer != 3) || 1 == 0)
				{
					return 0;
				}
				return val6.Elements.GetInteger("/N");
			}
		}
		return 0;
	}

	private static BitmapSource? DecodeJpeg(byte[] bytes)
	{
		using MemoryStream bitmapStream = new MemoryStream(bytes);
		System.Windows.Media.Imaging.BitmapFrame bitmapFrame = System.Windows.Media.Imaging.BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
		return (bitmapFrame.Format == PixelFormats.Cmyk32) ? null : bitmapFrame;
	}

	private static BitmapSource? DecodeRaw(PdfDictionary image, int width, int height, int channels)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (image.Elements.ContainsKey("/DecodeParms"))
		{
			return null;
		}
		byte[] array = ((Filter)new FlateDecode()).Decode(image.Stream.Value, (FilterParms)null);
		int num = width * channels;
		if (array.Length < num * height)
		{
			return null;
		}
		if (channels == 3)
		{
			return BitmapSource.Create(width, height, 96.0, 96.0, PixelFormats.Rgb24, null, array, num);
		}
		return BitmapSource.Create(width, height, 96.0, 96.0, PixelFormats.Gray8, null, array, num);
	}

	private static async Task<byte[]> RenderJpeg(Windows.Data.Pdf.PdfPage page, double dpi, float quality)
	{
		using InMemoryRandomAccessStream bmp = new InMemoryRandomAccessStream();
		PdfPageRenderOptions options = new PdfPageRenderOptions
		{
			DestinationWidth = (uint)Math.Max(1.0, Math.Round(page.Size.Width * dpi / 96.0)),
			BackgroundColor = new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 },
			BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId
		};
		await WindowsRuntimeSystemExtensions.AsTask(page.RenderToStreamAsync(bmp, options));
		bmp.Seek(0);
		Windows.Graphics.Imaging.BitmapDecoder decoder = await WindowsRuntimeSystemExtensions.AsTask(
			Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(bmp));
		using SoftwareBitmap pixels = await WindowsRuntimeSystemExtensions.AsTask(
			decoder.GetSoftwareBitmapAsync((BitmapPixelFormat)87, (BitmapAlphaMode)2));
		using InMemoryRandomAccessStream jpeg = new InMemoryRandomAccessStream();
		BitmapPropertySet properties = new BitmapPropertySet
		{
			{ "ImageQuality", new BitmapTypedValue(quality, PropertyType.Single) }
		};
		Windows.Graphics.Imaging.BitmapEncoder encoder = await WindowsRuntimeSystemExtensions.AsTask(
			Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
				Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, jpeg, properties));
		encoder.SetSoftwareBitmap(pixels);
		await WindowsRuntimeSystemExtensions.AsTask(encoder.FlushAsync());
		jpeg.Seek(0);
		using MemoryStream managed = new MemoryStream();
		await WindowsRuntimeStreamExtensions.AsStreamForRead(jpeg).CopyToAsync(managed);
		return managed.ToArray();
	}

}
