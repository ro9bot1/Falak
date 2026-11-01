using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Falak.Engines;

public static class Previews
{
	public static BitmapSource LoadImage(string path) => ImageEngine.Load(path);

	public static async Task<int> PdfPageCount(string path)
	{
		StorageFile file = await WindowsRuntimeSystemExtensions.AsTask(
			StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)));
		PdfDocument document = await WindowsRuntimeSystemExtensions.AsTask(
			PdfDocument.LoadFromFileAsync(file));
		return checked((int)document.PageCount);
	}

	public static async Task<byte[]> RenderPdfPage(string path, int pageIndex, int widthPx)
	{
		StorageFile file = await WindowsRuntimeSystemExtensions.AsTask(
			StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)));
		PdfDocument document = await WindowsRuntimeSystemExtensions.AsTask(
			PdfDocument.LoadFromFileAsync(file));
		if (document.PageCount == 0) throw new InvalidOperationException("PDF has no pages.");
		PdfPage page = document.GetPage((uint)Math.Clamp(pageIndex, 0, (int)document.PageCount - 1));
		try
		{
			using InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
			PdfPageRenderOptions options = new PdfPageRenderOptions
			{
				DestinationWidth = (uint)Math.Max(1, widthPx),
				BackgroundColor = new Windows.UI.Color { A = 255, R = 255, G = 255, B = 255 },
				BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId
			};
			await WindowsRuntimeSystemExtensions.AsTask(page.RenderToStreamAsync(stream, options));
			stream.Seek(0);
			using MemoryStream managed = new MemoryStream();
			await WindowsRuntimeStreamExtensions.AsStreamForRead(stream).CopyToAsync(managed);
			return managed.ToArray();
		}
		finally
		{
			((IDisposable)page).Dispose();
		}
	}
}
