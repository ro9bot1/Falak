using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Falak.Interop;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Falak.App;

internal static class SampleFiles
{
	public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Falak", "File Simple");

    public static string Create()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "QA_Samples");
        string dest = Folder; // Stable File Simple directory. Never overwrite user files.
        Directory.CreateDirectory(dest);
        if (Directory.Exists(source))
        {
            foreach (string original in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, original);
                if (relative.Equals("FALAK_TEST_MANIFEST.json", StringComparison.OrdinalIgnoreCase)) continue;
                string target = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (!File.Exists(target)) File.Copy(original, target, overwrite: false);
            }
            return dest;
        }

        // When distributed as a self-contained single EXE, the sample corpus is
        // embedded in the app; only unpack when File Simple is requested.
        using Stream bundle = typeof(SampleFiles).Assembly.GetManifestResourceStream("Falak.SampleCorpus.zip")
            ?? throw new IOException("Falak's embedded File Simple samples are missing.");
        using var archive = new ZipArchive(bundle, ZipArchiveMode.Read);
        string safeRoot = Path.GetFullPath(dest + Path.DirectorySeparatorChar);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (relative.Equals("FALAK_TEST_MANIFEST.json", StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.GetFullPath(Path.Combine(dest, relative));
            if (!target.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid embedded sample path.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (!File.Exists(target)) entry.ExtractToFile(target, overwrite: false);
        }
        return dest;
    }

	private static void Make(string name, Action<string> write)
	{
		string text = Path.Combine(Folder, name);
		if (!File.Exists(text))
		{
			write(text);
		}
	}

	private static BitmapSource Poster(string title, string subtitle, int width, int height)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			LinearGradientBrush brush = new LinearGradientBrush(BrandPalette.Hex("#0E1311"), BrandPalette.Hex("#123A2E"), 35.0);
			drawingContext.DrawRectangle(brush, null, new Rect(0.0, 0.0, (double)width, (double)height));
			drawingContext.PushTransform(new TranslateTransform((double)width * 0.62, (double)height * 0.18));
			BrandMark.Draw(drawingContext, (double)height * 0.64, tile: false);
			drawingContext.Pop();
			Typeface typeface = new Typeface(new FontFamily("Thmanyah Sans, Dubai, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
			FormattedText formattedText = new FormattedText(title, CultureInfo.GetCultureInfo("ar-SA"), FlowDirection.RightToLeft, typeface, (double)height * 0.16, Brushes.White, 1.0);
			FormattedText formattedText2 = new FormattedText(subtitle, CultureInfo.GetCultureInfo("ar-SA"), FlowDirection.RightToLeft, typeface, (double)height * 0.05, new SolidColorBrush(BrandMark.GoldHub), 1.0);
			drawingContext.DrawText(formattedText, new Point((double)width * 0.52 - formattedText.Width, (double)height * 0.36));
			drawingContext.DrawText(formattedText2, new Point((double)width * 0.52 - formattedText2.Width, (double)height * 0.36 + formattedText.Height + 10.0));
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		((Freezable)renderTargetBitmap).Freeze();
		return renderTargetBitmap;
	}

	private static void SavePng(BitmapSource image, string path)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}

	private static void MakePdf(string path)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected Obj, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		PdfDocument val = new PdfDocument();
		try
		{
			string[] array = new string[3] { "الصفحة الأولى", "الصفحة الثانية", "الصفحة الثالثة" };
			foreach (string subtitle in array)
			{
				BitmapSource source = Poster("فَلَك", subtitle, 1240, 1754);
				using MemoryStream memoryStream = new MemoryStream();
				JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder();
				jpegBitmapEncoder.QualityLevel = 88;
				jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(source));
				jpegBitmapEncoder.Save(memoryStream);
				memoryStream.Position = 0L;
				PdfPage val2 = val.AddPage();
				val2.Width = XUnit.FromPoint(595.28);
				val2.Height = XUnit.FromPoint(841.89);
				XGraphics val3 = XGraphics.FromPdfPage(val2);
				try
				{
					XImage val4 = XImage.FromStream((Stream)memoryStream);
					try
					{
						XUnit val5 = val2.Width;
						double point = val5.Point;
						val5 = val2.Height;
						val3.DrawImage(val4, 0.0, 0.0, point, val5.Point);
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
			val.Save(path);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}
}
