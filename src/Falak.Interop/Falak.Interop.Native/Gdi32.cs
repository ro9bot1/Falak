using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

internal static class Gdi32
{
	private const string Dll = "gdi32.dll";

	[DllImport("gdi32.dll", ExactSpelling = true)]
	public static extern nint CreateCompatibleDC(nint hdc);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* pbmi, uint usage, out nint ppvBits, nint hSection, uint offset)
	{
		ppvBits = 0;
		nint result;
		fixed (nint* _ppvBits_native = &ppvBits)
		{
			result = __PInvoke(hdc, pbmi, usage, _ppvBits_native, hSection, offset);
		}
		return result;
		[DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(nint __hdc_native, BITMAPINFOHEADER* __pbmi_native, uint __usage_native, nint* __ppvBits_native, nint __hSection_native, uint __offset_native);
	}

	[DllImport("gdi32.dll", ExactSpelling = true)]
	public static extern nint SelectObject(nint hdc, nint h);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool DeleteObject(nint ho)
	{
		return __PInvoke(ho) != 0;
		[DllImport("gdi32.dll", EntryPoint = "DeleteObject", ExactSpelling = true)]
		static extern int __PInvoke(nint __ho_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool DeleteDC(nint hdc)
	{
		return __PInvoke(hdc) != 0;
		[DllImport("gdi32.dll", EntryPoint = "DeleteDC", ExactSpelling = true)]
		static extern int __PInvoke(nint __hdc_native);
	}
}
