using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Falak.App;

internal static class IconExport
{
	private static readonly int[] Sizes = new int[9] { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

	public static void Write(string path)
	{
		List<(int, byte[])> list = Sizes.Select((int size) => (size: size, png: Png(Art.Mark(size)))).ToList();
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
		using FileStream output = File.Create(path);
		using BinaryWriter binaryWriter = new BinaryWriter(output);
		binaryWriter.Write((ushort)0);
		binaryWriter.Write((ushort)1);
		binaryWriter.Write((ushort)list.Count);
		int num = 6 + 16 * list.Count;
		foreach (var (num2, array) in list)
		{
			binaryWriter.Write((byte)((num2 < 256) ? ((uint)num2) : 0u));
			binaryWriter.Write((byte)((num2 < 256) ? ((uint)num2) : 0u));
			binaryWriter.Write((byte)0);
			binaryWriter.Write((byte)0);
			binaryWriter.Write((ushort)1);
			binaryWriter.Write((ushort)32);
			binaryWriter.Write(array.Length);
			binaryWriter.Write(num);
			num += array.Length;
		}
		foreach (var item2 in list)
		{
			byte[] item = item2.Item2;
			binaryWriter.Write(item);
		}
	}

	public static byte[] Png(BitmapSource image)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}
}
