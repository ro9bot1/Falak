using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Falak.App;

internal static class Backdrop
{
	private struct MARGINS
	{
		public int Left;

		public int Right;

		public int Top;

		public int Bottom;
	}

	private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

	private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

	private const int DWMSBT_NONE = 1;

	private const int DWMSBT_TRANSIENTWINDOW = 3;

	public static bool Supported => Environment.OSVersion.Version.Build >= 22621;

	[SkipLocalsInit]
	private unsafe static int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size)
	{
		int result;
		fixed (int* _value_native = &value)
		{
			result = __PInvoke(hwnd, attribute, _value_native, size);
		}
		return result;
		[DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute", ExactSpelling = true)]
		unsafe static extern int __PInvoke(nint __hwnd_native, int __attribute_native, int* __value_native, int __size_native);
	}

	[SkipLocalsInit]
	private unsafe static int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins)
	{
		int result;
		fixed (MARGINS* _margins_native = &margins)
		{
			result = __PInvoke(hwnd, _margins_native);
		}
		return result;
		[DllImport("dwmapi.dll", EntryPoint = "DwmExtendFrameIntoClientArea", ExactSpelling = true)]
		unsafe static extern int __PInvoke(nint __hwnd_native, MARGINS* __margins_native);
	}

	public static void Apply(Window window, bool glass, bool dark)
	{
		nint hwnd = new WindowInteropHelper(window).EnsureHandle();
		int value = (dark ? 1 : 0);
		DwmSetWindowAttribute(hwnd, 20, ref value, 4);
		if (Supported)
		{
			HwndSource hwndSource = HwndSource.FromHwnd(hwnd);
			bool flag = glass;
			HwndTarget hwndTarget = hwndSource?.CompositionTarget;
			if (hwndTarget != null)
			{
				HwndTarget hwndTarget2 = hwndTarget;
				Color backgroundColor;
				if (flag)
				{
					backgroundColor = Colors.Transparent;
				}
				else
				{
					backgroundColor = (dark ? Color.FromRgb(14, 17, 16) : Color.FromRgb(245, 244, 239));
				}
				hwndTarget2.BackgroundColor = backgroundColor;
			}
			MARGINS margins = (flag ? new MARGINS
			{
				Left = -1,
				Right = -1,
				Top = -1,
				Bottom = -1
			} : default(MARGINS));
			DwmExtendFrameIntoClientArea(hwnd, ref margins);
			int value2 = ((!flag) ? 1 : 3);
			DwmSetWindowAttribute(hwnd, 38, ref value2, 4);
			if (flag)
			{
				window.Background = Brushes.Transparent;
			}
			else
			{
				window.SetResourceReference(Control.BackgroundProperty, "Bg");
			}
		}
	}
}
