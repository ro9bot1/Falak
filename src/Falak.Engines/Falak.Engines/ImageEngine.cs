using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Falak.Engines;

internal static class ImageEngine
{
	public static readonly string[] Encodable = new string[5] { ".jpg", ".png", ".tiff", ".bmp", ".gif" };

	private const int CompressMaxSide = 2560;

	public static JobResult Convert(IReadOnlyList<string> inputs, string extension, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			BitmapSource image = Load(input);
			string text = OutputPaths.NextTo(input, extension);
			OutputPaths.WriteAtomically(text, (string temp) =>
			{
				Save(image, temp, extension, 92);
			});
			list.Add(text);
			stepProgress.Next();
		}
		return new JobResult(list, null, Texts.Made(list.Count, "صورة واحدة", "صورتين", "صور") + " بصيغة " + extension.TrimStart('.').ToUpperInvariant());
	}

	public static JobResult Compress(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		long num = 0L;
		long num2 = 0L;
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			BitmapSource bitmapSource = Load(input);
			double num3 = Math.Min(1.0, 2560.0 / (double)Math.Max(bitmapSource.PixelWidth, bitmapSource.PixelHeight));
			BitmapSource smaller = ((num3 < 1.0) ? Resize(bitmapSource, num3) : bitmapSource);
			string text = OutputPaths.NextTo(input, ".jpg", "مضغوط");
			OutputPaths.WriteAtomically(text, (string temp) =>
			{
				Save(smaller, temp, ".jpg", 74);
			});
			long length = new FileInfo(input).Length;
			long length2 = new FileInfo(text).Length;
			if (length2 >= length)
			{
				File.Delete(text);
				stepProgress.Next();
				continue;
			}
			num += length;
			num2 += length2;
			list.Add(text);
			stepProgress.Next();
		}
		if (list.Count == 0)
		{
			return new JobResult(Array.Empty<string>(), null, "الصور مضغوطة أصلا\u064b، لم يتغير شيء.");
		}
		int value = (int)Math.Round(100.0 * (double)(num - num2) / (double)num);
		return new JobResult(list, null, $"وف\u0651رنا {value}٪ · من {Texts.Size(num)} إلى {Texts.Size(num2)}");
	}

	public static JobResult HalfSize(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		return Transform(inputs, "نصف الحجم", (BitmapSource image) => Resize(image, 0.5), progress, ct, "صغ\u0651رنا الأبعاد إلى النصف");
	}

	public static JobResult Rotate(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		return Transform(inputs, "مدو\u0651رة", (BitmapSource image) => Frozen(new TransformedBitmap(image, new RotateTransform(90.0))), progress, ct, "دو\u0651رنا الصورة 90° مع عقارب الساعة");
	}

	public static JobResult StripMetadata(IReadOnlyList<string> inputs, IProgress<double>? progress, CancellationToken ct)
	{
		return Transform(inputs, "بلا بيانات", (BitmapSource image) => image, progress, ct, "أزلنا الموقع وبيانات الكاميرا");
	}

	private static JobResult Transform(IReadOnlyList<string> inputs, string suffix, Func<BitmapSource, BitmapSource> change, IProgress<double>? progress, CancellationToken ct, string message)
	{
		StepProgress stepProgress = new StepProgress(progress, inputs.Count);
		List<string> list = new List<string>();
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			BitmapSource image = change(Load(input));
			string extension = KeepableExtension(input);
			string text = OutputPaths.NextTo(input, extension, suffix);
			OutputPaths.WriteAtomically(text, (string temp) =>
			{
				Save(image, temp, extension, 95);
			});
			list.Add(text);
			stepProgress.Next();
		}
		return new JobResult(list, null, message);
	}

	public static byte[] ToJpeg(string path, out int width, out int height)
	{
		BitmapSource bitmapSource = Load(path);
		width = bitmapSource.PixelWidth;
		height = bitmapSource.PixelHeight;
		using MemoryStream memoryStream = new MemoryStream();
		JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder();
		jpegBitmapEncoder.QualityLevel = 90;
		jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(Flatten(bitmapSource)));
		jpegBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}

	public static BitmapSource Load(string path)
	{
		try
		{
			using FileStream bitmapStream = File.OpenRead(path);
			BitmapDecoder bitmapDecoder = BitmapDecoder.Create(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			return Frozen(ApplyOrientation(bitmapDecoder.Frames[0], ReadOrientation(bitmapDecoder.Frames[0])));
		}
		catch (Exception ex) when ((ex is NotSupportedException || ex is FileFormatException || ex is COMException || ex is ArgumentException) ? true : false)
		{
			string text;
			switch (Path.GetExtension(path).ToLowerInvariant())
			{
			case ".heic":
			case ".heif":
				text = " ثب\u0651ت «HEIF Image Extensions» المجانية من متجر مايكروسوفت ثم أعد المحاولة.";
				break;
			case ".webp":
				text = " ثب\u0651ت «Webp Image Extensions» المجانية من متجر مايكروسوفت ثم أعد المحاولة.";
				break;
			case ".avif":
				text = " ثب\u0651ت «AV1 Video Extension» المجانية من متجر مايكروسوفت ثم أعد المحاولة.";
				break;
			default:
				text = " قد يكون الملف تالفا\u064b.";
				break;
			}
			string text2 = text;
			throw new UserFacingException("تعذ\u0651ر فتح الصورة «" + Path.GetFileName(path) + "»." + text2, ex);
		}
	}

	private static ushort ReadOrientation(BitmapFrame frame)
	{
		try
		{
			if (frame.Metadata is BitmapMetadata bitmapMetadata)
			{
				object query = bitmapMetadata.GetQuery("System.Photo.Orientation");
				if (query is ushort)
				{
					return (ushort)query;
				}
			}
		}
		catch (Exception ex) when ((ex is NotSupportedException || ex is InvalidOperationException || ex is ArgumentException) ? true : false)
		{
		}
		return 1;
	}

	private static BitmapSource ApplyOrientation(BitmapSource source, ushort orientation)
	{
		Transform transform = orientation switch
		{
			2 => (Transform)new ScaleTransform(-1.0, 1.0), 
			3 => new RotateTransform(180.0), 
			4 => new ScaleTransform(1.0, -1.0), 
			5 => new TransformGroup
			{
				Children = 
				{
					(Transform)new RotateTransform(90.0),
					(Transform)new ScaleTransform(-1.0, 1.0)
				}
			}, 
			6 => new RotateTransform(90.0), 
			7 => new TransformGroup
			{
				Children = 
				{
					(Transform)new RotateTransform(270.0),
					(Transform)new ScaleTransform(-1.0, 1.0)
				}
			}, 
			8 => new RotateTransform(270.0), 
			_ => null, 
		};
		if (transform != null)
		{
			return new TransformedBitmap(source, transform);
		}
		return source;
	}

	private static string KeepableExtension(string path)
	{
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text == ".jpeg")
		{
			return ".jpg";
		}
		if (text == ".tif")
		{
			return ".tiff";
		}
		if (!Encodable.Contains(text))
		{
			return ".jpg";
		}
		return text;
	}

	public static void Save(BitmapSource image, string path, string extension, int jpegQuality)
	{
		BitmapEncoder bitmapEncoder;
		switch (extension)
		{
		case ".png":
			bitmapEncoder = new PngBitmapEncoder();
			break;
		case ".tiff":
			bitmapEncoder = new TiffBitmapEncoder
			{
				Compression = TiffCompressOption.Lzw
			};
			break;
		case ".gif":
			bitmapEncoder = new GifBitmapEncoder();
			break;
		case ".bmp":
			bitmapEncoder = new BmpBitmapEncoder();
			image = Flatten(image);
			break;
		default:
			bitmapEncoder = new JpegBitmapEncoder
			{
				QualityLevel = jpegQuality
			};
			image = Flatten(image);
			break;
		}
		bitmapEncoder.Frames.Add(BitmapFrame.Create(image));
		using FileStream stream = File.Create(path);
		bitmapEncoder.Save(stream);
	}

	private static bool HasAlpha(PixelFormat format)
	{
		if (!(format == PixelFormats.Bgra32) && !(format == PixelFormats.Pbgra32) && !(format == PixelFormats.Rgba64) && !(format == PixelFormats.Prgba64) && !(format == PixelFormats.Rgba128Float) && !(format == PixelFormats.Prgba128Float) && !(format == PixelFormats.Indexed8) && !(format == PixelFormats.Indexed4) && !(format == PixelFormats.Indexed2))
		{
			return format == PixelFormats.Indexed1;
		}
		return true;
	}

	private static BitmapSource Flatten(BitmapSource image)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		if (!HasAlpha(image.Format))
		{
			return image;
		}
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			Rect rectangle = new Rect(0.0, 0.0, (double)image.PixelWidth, (double)image.PixelHeight);
			drawingContext.DrawRectangle(Brushes.White, null, rectangle);
			drawingContext.DrawImage(image, rectangle);
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		return Frozen(new FormatConvertedBitmap(renderTargetBitmap, PixelFormats.Bgr24, null, 0.0));
	}

	private static BitmapSource Resize(BitmapSource image, double scale)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		int num = Math.Max(1, (int)Math.Round((double)image.PixelWidth * scale));
		int num2 = Math.Max(1, (int)Math.Round((double)image.PixelHeight * scale));
		DrawingVisual drawingVisual = new DrawingVisual();
		RenderOptions.SetBitmapScalingMode((DependencyObject)(object)drawingVisual, BitmapScalingMode.HighQuality);
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawImage(image, new Rect(0.0, 0.0, (double)num, (double)num2));
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num, num2, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		return Frozen(renderTargetBitmap);
	}

	private static BitmapSource Frozen(BitmapSource image)
	{
		if (((Freezable)image).CanFreeze)
		{
			((Freezable)image).Freeze();
		}
		return image;
	}
}
