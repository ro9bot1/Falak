using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Falak.Interop;

public sealed record BrandPalette(bool IsDark, bool IsGlass, Color Background, Color Surface, Color SurfaceRaised, Color Ink, Color Muted, Color Line, Color Accent, Color AccentHover, Color AccentInk, Color AccentSoft, Color Gold, Color Danger, Color WheelDisc, Color WheelSlice)
{
	public static BrandPalette Light { get; } = Build(dark: false, AccentChoice.Emerald);

	public static BrandPalette Dark { get; } = Build(dark: true, AccentChoice.Emerald);

	public static BrandPalette Build(bool dark, AccentChoice accent, bool glass = false)
	{
		Color color = (dark ? accent.Dark : accent.Light);
		Color color2 = (dark ? Hex("#0E1110") : Hex("#F5F4EF"));
		Color color3 = (dark ? Hex("#161A19") : Hex("#FFFFFF"));
		Color color4 = (dark ? Hex("#1C2120") : Hex("#FBFAF7"));
		Color color5 = (dark ? Hex("#2A302E") : Hex("#E3E1D8"));
		return new BrandPalette(dark, glass, glass ? Color.FromArgb(0, color2.R, color2.G, color2.B) : color2, glass ? WithAlpha(color3, (byte)(dark ? 156u : 184u)) : color3, glass ? WithAlpha(color4, (byte)(dark ? 128u : 160u)) : color4, dark ? Hex("#ECEFEC") : Hex("#141816"), dark ? Hex("#9AA39F") : Hex("#5F6662"), glass ? WithAlpha(color5, 160) : color5, color, Mix(color, dark ? Colors.White : Colors.Black, 0.14), (dark && accent.Id == "graphite") ? Hex("#111414") : Colors.White, Mix(color2, color, dark ? 0.2 : 0.11), dark ? Hex("#D2AE68") : Hex("#B08A3E"), dark ? Hex("#F0716A") : Hex("#B42318"), dark ? Hex("#F2161A19") : Hex("#F5FFFFFF"), dark ? Hex("#222826") : Hex("#F1F0EA"));
	}

	public static bool SystemPrefersDark()
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
		return registryKey?.GetValue("AppsUseLightTheme") is int num && num == 0;
	}

	public static BrandPalette From(ThemePreference theme, AccentChoice accent, bool glass)
	{
		return Build(theme switch
		{
			ThemePreference.Dark => true, 
			ThemePreference.Light => false, 
			_ => SystemPrefersDark(), 
		}, accent, glass);
	}

	public static BrandPalette FromSystem()
	{
		if (!SystemPrefersDark())
		{
			return Light;
		}
		return Dark;
	}

	public static Color Hex(string hex)
	{
		return (Color)ColorConverter.ConvertFromString(hex);
	}

	public static Color WithAlpha(Color c, byte alpha)
	{
		return Color.FromArgb(alpha, c.R, c.G, c.B);
	}

	public static Color Mix(Color a, Color b, double t)
	{
		return Color.FromArgb(byte.MaxValue, (byte)Math.Round((double)(int)a.R + (double)(b.R - a.R) * t), (byte)Math.Round((double)(int)a.G + (double)(b.G - a.G) * t), (byte)Math.Round((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	public SolidColorBrush Brush(Color color)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(color);
		((Freezable)solidColorBrush).Freeze();
		return solidColorBrush;
	}
}
