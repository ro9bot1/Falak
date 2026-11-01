using System;
using System.Collections.Generic;
using System.Linq;

namespace Falak.Core;

public static class DocumentColors
{
	public static readonly Rgb Brand = new Rgb(14, 107, 83);

	public static readonly Rgb Basalt = new Rgb(18, 22, 20);

	public static IReadOnlyList<Rgb> Extract(ReadOnlySpan<byte> bgra, int max = 4)
	{
		int[] array = new int[4096];
		long[] array2 = new long[12288];
		int num = 0;
		for (int i = 0; i + 3 < bgra.Length; i += 4)
		{
			if (bgra[i + 3] >= 128)
			{
				byte b = bgra[i];
				byte b2 = bgra[i + 1];
				byte b3 = bgra[i + 2];
				int num2 = (b3 >> 4 << 8) | (b2 >> 4 << 4) | (b >> 4);
				array[num2]++;
				array2[num2 * 3] += b3;
				array2[num2 * 3 + 1] += b2;
				array2[num2 * 3 + 2] += b;
				num++;
			}
		}
		if (num == 0)
		{
			return Array.Empty<Rgb>();
		}
		List<(Rgb, int)> list = new List<(Rgb, int)>();
		for (int j = 0; j < array.Length; j++)
		{
			int num3 = array[j];
			if (num3 != 0 && !((double)num3 < (double)num * 0.002))
			{
				list.Add((new Rgb((byte)(array2[j * 3] / num3), (byte)(array2[j * 3 + 1] / num3), (byte)(array2[j * 3 + 2] / num3)), num3));
			}
		}
		List<Rgb> list2 = Distinct(list.Where(((Rgb Color, int Count) tuple) => !Neutral(tuple.Color)).OrderByDescending(((Rgb Color, int Count) tuple) => (double)tuple.Count * (0.35 + tuple.Color.Saturation)).Select(((Rgb Color, int Count) tuple) => tuple.Color), max);
		if (list2.Count < max)
		{
			IOrderedEnumerable<(Rgb, int)> source = list.Where(((Rgb Color, int Count) tuple) =>
			{
				if (Neutral(tuple.Color))
				{
					double lightness = tuple.Color.Lightness;
					if (lightness > 0.12)
					{
						return lightness < 0.9;
					}
					return false;
				}
				return false;
			}).OrderByDescending(((Rgb Color, int Count) tuple) => tuple.Count);
			list2 = Distinct(list2.Concat(source.Select(((Rgb Color, int Count) tuple) => tuple.Color)), max);
		}
		return list2;
		static bool Neutral(Rgb c)
		{
			if (!(c.Lightness > 0.93) && !(c.Lightness < 0.07))
			{
				return c.Saturation < 0.12;
			}
			return true;
		}
	}

	private static List<Rgb> Distinct(IEnumerable<Rgb> colours, int max, double minDistance = 40.0)
	{
		List<Rgb> list = new List<Rgb>();
		foreach (Rgb c in colours)
		{
			if (list.Count >= max)
			{
				break;
			}
			if (list.All((Rgb p) => p.DistanceTo(c) >= minDistance))
			{
				list.Add(c);
			}
		}
		return list;
	}

	public static IReadOnlyList<BackgroundSpec> Suggestions(IReadOnlyList<Rgb> documentColours, int solids = 6, int gradients = 4)
	{
		Rgb rgb = ((documentColours.Count > 0) ? documentColours[0] : Brand);
		Rgb? rgb2 = ((documentColours.Count > 1) ? new Rgb?(documentColours[1]) : ((Rgb?)null));
		Rgb? rgb3 = ((documentColours.Count > 2) ? new Rgb?(documentColours[2]) : ((Rgb?)null));
		List<Rgb> list = new List<Rgb>
		{
			Rgb.White,
			rgb.Tint(0.88),
			rgb
		};
		if (rgb2.HasValue)
		{
			Rgb valueOrDefault = rgb2.GetValueOrDefault();
			list.Add(valueOrDefault);
		}
		list.Add(rgb.Shade(0.55));
		if (rgb3.HasValue)
		{
			Rgb valueOrDefault2 = rgb3.GetValueOrDefault();
			list.Add(valueOrDefault2);
		}
		list.Add(rgb.Tint(0.6));
		list.Add(Basalt);
		List<BackgroundSpec> list2 = new List<BackgroundSpec>
		{
			new BackgroundSpec(rgb, rgb.Tint(0.7)),
			new BackgroundSpec(rgb.Shade(0.6), rgb)
		};
		if (rgb2.HasValue)
		{
			Rgb valueOrDefault3 = rgb2.GetValueOrDefault();
			list2.Add(new BackgroundSpec(rgb, valueOrDefault3));
		}
		list2.Add(new BackgroundSpec(rgb.Tint(0.92), Rgb.White, 90.0));
		if (rgb2.HasValue)
		{
			Rgb valueOrDefault4 = rgb2.GetValueOrDefault();
			if (rgb3.HasValue)
			{
				Rgb valueOrDefault5 = rgb3.GetValueOrDefault();
				list2.Add(new BackgroundSpec(valueOrDefault4, valueOrDefault5));
			}
		}
		list2.Add(new BackgroundSpec(Basalt, rgb.Shade(0.35), 90.0));
		return (from s in Distinct(list, solids, 16.0)
			select new BackgroundSpec(s)).Concat(list2.Take(gradients)).ToList();
	}
}
