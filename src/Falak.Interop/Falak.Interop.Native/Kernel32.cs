using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Falak.Interop.Native;

internal static class Kernel32
{
	private const string Dll = "kernel32.dll";
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint GetModuleHandleW(string? lpModuleName)
	{
		nint result;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(lpModuleName))
		{
			void* _lpModuleName_native = ptr;
			result = __PInvoke((ushort*)_lpModuleName_native);
		}
		return result;
		[DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(ushort* __lpModuleName_native);
	}

	[DllImport("kernel32.dll", ExactSpelling = true)]
	public static extern uint GetCurrentThreadId();
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public static nint GlobalAlloc(uint uFlags, nuint dwBytes)
	{
		Marshal.SetLastSystemError(0);
		nint result = __PInvoke(uFlags, dwBytes);
		Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
		return result;
		[DllImport("kernel32.dll", EntryPoint = "GlobalAlloc", ExactSpelling = true)]
		static extern nint __PInvoke(uint __uFlags_native, nuint __dwBytes_native);
	}

	[DllImport("kernel32.dll", ExactSpelling = true)]
	public static extern nint GlobalLock(nint hMem);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool GlobalUnlock(nint hMem)
	{
		return __PInvoke(hMem) != 0;
		[DllImport("kernel32.dll", EntryPoint = "GlobalUnlock", ExactSpelling = true)]
		static extern int __PInvoke(nint __hMem_native);
	}

	[DllImport("kernel32.dll", ExactSpelling = true)]
	public static extern nint GlobalFree(nint hMem);
}
