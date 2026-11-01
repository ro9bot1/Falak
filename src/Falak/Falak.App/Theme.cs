using System;
using System.Windows;
using System.Windows.Media;
using Falak.Interop;

namespace Falak.App;

internal static class Theme
{
	public static BrandPalette Current { get; private set; } = BrandPalette.Light;

	public static void Apply(BrandPalette palette)
	{
		Current = palette;
		ResourceDictionary resources = Application.Current.Resources;
		Set("Bg", palette.Background);
		// Popups and confirmation dialogs must remain opaque, even when Glass
		// makes the regular Surface brush translucent.
		Set("PopupSurface", palette.IsDark ? BrandPalette.Hex("#161A19") : Colors.White);
		Set("PopupBorder", palette.IsDark ? BrandPalette.Hex("#3C4742") : BrandPalette.Hex("#C9D0C8"));
		Set("Surface", palette.Surface);
		Set("SurfaceRaised", palette.SurfaceRaised);
		Set("Ink", palette.Ink);
		Set("Muted", palette.Muted);
		Set("Line", palette.Line);
		Set("Accent", palette.Accent);
		Set("AccentHover", palette.AccentHover);
		Set("AccentInk", palette.AccentInk);
		Set("AccentSoft", palette.AccentSoft);
		Set("Gold", palette.Gold);
		Set("Danger", palette.Danger);
		Set("DangerSoft", palette.IsDark ? BrandPalette.Hex("#2E1716") : BrandPalette.Hex("#FBE9E7"));
		// WPF ThemeMode is experimental in .NET 10. Preserve theme switching.
#pragma warning disable WPF0001
		try
		{
			Application.Current.ThemeMode = palette.IsDark ? ThemeMode.Dark : ThemeMode.Light;
		}
		catch (Exception ex)
		{
			RuntimeDiagnostics.Report("Optional WPF ThemeMode property failed; dynamic colors remain active", ex);
		}
#pragma warning restore WPF0001
		void Set(string key, Color color)
		{
			resources[key] = palette.Brush(color);
		}
	}
}
