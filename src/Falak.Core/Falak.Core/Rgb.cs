using System;
using System.Globalization;
using System.Linq;

namespace Falak.Core;

public readonly record struct Rgb(byte R, byte G, byte B)
{
	public string Hex => $"#{R:X2}{G:X2}{B:X2}";

	public double Lightness => (0.2126 * (double)(int)R + 0.7152 * (double)(int)G + 0.0722 * (double)(int)B) / 255.0;

	public double Saturation => ToHsv().S;

	public static readonly Rgb White = new Rgb(byte.MaxValue, byte.MaxValue, byte.MaxValue);

	public static readonly Rgb Black = new Rgb(0, 0, 0);

	public static bool TryParseHex(string? text, out Rgb rgb)
	{
		rgb = default;
		string text2 = (text ?? "").Trim().TrimStart('#');
		if (text2.Length == 3)
		{
			text2 = string.Concat(text2.Select((char c) => new string(c, 2)));
		}
		if (text2.Length != 6 || !int.TryParse(text2, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
		{
			return false;
		}
		rgb = new Rgb((byte)(result >> 16), (byte)(result >> 8), (byte)result);
		return true;
	}

	public (double H, double S, double V) ToHsv()
	{
		double num = (double)(int)R / 255.0;
		double num2 = (double)(int)G / 255.0;
		double num3 = (double)(int)B / 255.0;
		double num4 = Math.Max(num, Math.Max(num2, num3));
		double num5 = Math.Min(num, Math.Min(num2, num3));
		double num6 = num4 - num5;
		double num7;
		if (num6 == 0.0)
		{
			num7 = 0.0;
		}
		else if (num4 == num)
		{
			num7 = 60.0 * ((num2 - num3) / num6 % 6.0);
		}
		else
		{
			num7 = ((num4 == num2) ? (60.0 * ((num3 - num) / num6 + 2.0)) : (60.0 * ((num - num2) / num6 + 4.0)));
		}
		if (num7 < 0.0)
		{
			num7 += 360.0;
		}
		return (H: num7, S: (num4 == 0.0) ? 0.0 : (num6 / num4), V: num4);
	}

	public static Rgb FromHsv(double h, double s, double v)
	{
		h = (h % 360.0 + 360.0) % 360.0;
		s = Math.Clamp(s, 0.0, 1.0);
		v = Math.Clamp(v, 0.0, 1.0);
		double num = v * s;
		double num2 = num * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
		double num3 = v - num;
		var (num4, num5, num6) = (int)(h / 60.0) switch
		{
			0 => (num, num2, 0.0), 
			1 => (num2, num, 0.0), 
			2 => (0.0, num, num2), 
			3 => (0.0, num2, num), 
			4 => (num2, 0.0, num), 
			_ => (num, 0.0, num2), 
		};
		return new Rgb(ToByte(num4 + num3), ToByte(num5 + num3), ToByte(num6 + num3));
	}

	public Rgb Mix(Rgb other, double t)
	{
		t = Math.Clamp(t, 0.0, 1.0);
		return new Rgb(ToByte(((double)(int)R + (double)(other.R - R) * t) / 255.0), ToByte(((double)(int)G + (double)(other.G - G) * t) / 255.0), ToByte(((double)(int)B + (double)(other.B - B) * t) / 255.0));
	}

	public Rgb Tint(double t)
	{
		return Mix(White, t);
	}

	public Rgb Shade(double t)
	{
		return Mix(Black, t);
	}

	public double DistanceTo(Rgb o)
	{
		int num = R - o.R;
		int num2 = G - o.G;
		int num3 = B - o.B;
		return Math.Sqrt(2 * num * num + 4 * num2 * num2 + 3 * num3 * num3) / 3.0;
	}

	private static byte ToByte(double unit)
	{
		return (byte)Math.Clamp(Math.Round(unit * 255.0), 0.0, 255.0);
	}
}
