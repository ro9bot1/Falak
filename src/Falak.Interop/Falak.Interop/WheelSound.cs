using System;
using System.IO;
using Falak.Interop.Native;

namespace Falak.Interop;

public static class WheelSound
{
	private const int SampleRate = 44100;

	private static readonly byte[] Wave = Build();

	public unsafe static bool Tick()
	{
		fixed (byte* wave = Wave)
		{
			return Winmm.PlaySound(wave, 0, 7u);
		}
	}

	private static byte[] Build()
	{
		int num = 1323;
		Random random = new Random(7);
		short[] array = new short[num];
		for (int i = 0; i < num; i++)
		{
			double num2 = (double)i / 44100.0;
			double num3 = Math.Exp((0.0 - num2) / 0.0028) * (0.62 * Math.Sin(Math.PI * 3700.0 * num2) + 0.38 * Math.Sin(Math.PI * 6600.0 * num2));
			double num4 = Math.Exp((0.0 - num2) / 0.0007) * (random.NextDouble() * 2.0 - 1.0) * 0.45;
			double num5 = Math.Min(1.0, num2 / 0.0004);
			array[i] = (short)Math.Round(Math.Clamp((num3 + num4) * num5 * 0.18, -1.0, 1.0) * 32767.0);
		}
		int num6 = num * 2;
		byte[] array2 = GC.AllocateArray<byte>(44 + num6, pinned: true);
		using MemoryStream output = new MemoryStream(array2);
		using BinaryWriter binaryWriter = new BinaryWriter(output);
		binaryWriter.Write("RIFF"u8);
		binaryWriter.Write(36 + num6);
		binaryWriter.Write("WAVE"u8);
		binaryWriter.Write("fmt "u8);
		binaryWriter.Write(16);
		binaryWriter.Write((short)1);
		binaryWriter.Write((short)1);
		binaryWriter.Write(44100);
		binaryWriter.Write(88200);
		binaryWriter.Write((short)2);
		binaryWriter.Write((short)16);
		binaryWriter.Write("data"u8);
		binaryWriter.Write(num6);
		short[] array3 = array;
		foreach (short value in array3)
		{
			binaryWriter.Write(value);
		}
		return array2;
	}
}
