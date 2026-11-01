using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Falak.Interop.Native;

internal static class User32
{
	private const string Dll = "user32.dll";
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint SetWindowsHookExW(int idHook, delegate* unmanaged<int, nint, nint, nint> lpfn, nint hmod, uint dwThreadId)
	{
		Marshal.SetLastSystemError(0);
		nint result = __PInvoke(idHook, lpfn, hmod, dwThreadId);
		Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
		return result;
		[DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(int __idHook_native, delegate* unmanaged<int, nint, nint, nint> __lpfn_native, nint __hmod_native, uint __dwThreadId_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool UnhookWindowsHookEx(nint hhk)
	{
		Marshal.SetLastSystemError(0);
		int num = __PInvoke(hhk);
		int lastSystemError = Marshal.GetLastSystemError();
		bool result = num != 0;
		Marshal.SetLastPInvokeError(lastSystemError);
		return result;
		[DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", ExactSpelling = true)]
		static extern int __PInvoke(nint __hhk_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

	[DllImport("user32.dll", ExactSpelling = true)]
	public unsafe static extern int GetMessageW(MSG* lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool TranslateMessage(MSG* lpMsg)
	{
		return __PInvoke(lpMsg) != 0;
		[DllImport("user32.dll", EntryPoint = "TranslateMessage", ExactSpelling = true)]
		unsafe static extern int __PInvoke(MSG* __lpMsg_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public unsafe static extern nint DispatchMessageW(MSG* lpMsg);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool PostThreadMessageW(uint idThread, uint msg, nint wParam, nint lParam)
	{
		Marshal.SetLastSystemError(0);
		int num = __PInvoke(idThread, msg, wParam, lParam);
		int lastSystemError = Marshal.GetLastSystemError();
		bool result = num != 0;
		Marshal.SetLastPInvokeError(lastSystemError);
		return result;
		[DllImport("user32.dll", EntryPoint = "PostThreadMessageW", ExactSpelling = true)]
		static extern int __PInvoke(uint __idThread_native, uint __msg_native, nint __wParam_native, nint __lParam_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint FindWindowW(string? lpClassName, string? lpWindowName)
	{
		nint result;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(lpWindowName))
		{
			void* _lpWindowName_native = ptr;
			fixed (char* ptr2 = &Utf16StringMarshaller.GetPinnableReference(lpClassName))
			{
				void* _lpClassName_native = ptr2;
				result = __PInvoke((ushort*)_lpClassName_native, (ushort*)_lpWindowName_native);
			}
		}
		return result;
		[DllImport("user32.dll", EntryPoint = "FindWindowW", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(ushort* __lpClassName_native, ushort* __lpWindowName_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool IsWindowVisible(nint hWnd)
	{
		return __PInvoke(hWnd) != 0;
		[DllImport("user32.dll", EntryPoint = "IsWindowVisible", ExactSpelling = true)]
		static extern int __PInvoke(nint __hWnd_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern int GetSystemMetrics(int nIndex);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool GetCursorPos(out POINT lpPoint)
	{
		lpPoint = default;
		int num;
		fixed (POINT* _lpPoint_native = &lpPoint)
		{
			num = __PInvoke(_lpPoint_native);
		}
		return num != 0;
		[DllImport("user32.dll", EntryPoint = "GetCursorPos", ExactSpelling = true)]
		unsafe static extern int __PInvoke(POINT* __lpPoint_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi)
	{
		int num;
		fixed (MONITORINFO* _lpmi_native = &lpmi)
		{
			num = __PInvoke(hMonitor, _lpmi_native);
		}
		return num != 0;
		[DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true)]
		unsafe static extern int __PInvoke(nint __hMonitor_native, MONITORINFO* __lpmi_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static ushort RegisterClassExW(WNDCLASSEXW* lpwcx)
	{
		Marshal.SetLastSystemError(0);
		ushort result = __PInvoke(lpwcx);
		Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());
		return result;
		[DllImport("user32.dll", EntryPoint = "RegisterClassExW", ExactSpelling = true)]
		unsafe static extern ushort __PInvoke(WNDCLASSEXW* __lpwcx_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint CreateWindowExW(uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam)
	{
		nint result;
		int lastSystemError;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(lpWindowName))
		{
			void* _lpWindowName_native = ptr;
			fixed (char* ptr2 = &Utf16StringMarshaller.GetPinnableReference(lpClassName))
			{
				void* _lpClassName_native = ptr2;
				Marshal.SetLastSystemError(0);
				result = __PInvoke(dwExStyle, (ushort*)_lpClassName_native, (ushort*)_lpWindowName_native, dwStyle, x, y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
				lastSystemError = Marshal.GetLastSystemError();
			}
		}
		Marshal.SetLastPInvokeError(lastSystemError);
		return result;
		[DllImport("user32.dll", EntryPoint = "CreateWindowExW", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(uint __dwExStyle_native, ushort* __lpClassName_native, ushort* __lpWindowName_native, uint __dwStyle_native, int __x_native, int __y_native, int __nWidth_native, int __nHeight_native, nint __hWndParent_native, nint __hMenu_native, nint __hInstance_native, nint __lpParam_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool DestroyWindow(nint hWnd)
	{
		return __PInvoke(hWnd) != 0;
		[DllImport("user32.dll", EntryPoint = "DestroyWindow", ExactSpelling = true)]
		static extern int __PInvoke(nint __hWnd_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool ShowWindow(nint hWnd, int nCmdShow)
	{
		return __PInvoke(hWnd, nCmdShow) != 0;
		[DllImport("user32.dll", EntryPoint = "ShowWindow", ExactSpelling = true)]
		static extern int __PInvoke(nint __hWnd_native, int __nCmdShow_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags)
	{
		return __PInvoke(hWnd, hWndInsertAfter, x, y, cx, cy, uFlags) != 0;
		[DllImport("user32.dll", EntryPoint = "SetWindowPos", ExactSpelling = true)]
		static extern int __PInvoke(nint __hWnd_native, nint __hWndInsertAfter_native, int __x_native, int __y_native, int __cx_native, int __cy_native, uint __uFlags_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool UpdateLayeredWindow(nint hWnd, nint hdcDst, POINT* pptDst, SIZE* psize, nint hdcSrc, POINT* pptSrc, uint crKey, BLENDFUNCTION* pblend, uint dwFlags)
	{
		Marshal.SetLastSystemError(0);
		int num = __PInvoke(hWnd, hdcDst, pptDst, psize, hdcSrc, pptSrc, crKey, pblend, dwFlags);
		int lastSystemError = Marshal.GetLastSystemError();
		bool result = num != 0;
		Marshal.SetLastPInvokeError(lastSystemError);
		return result;
		[DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow", ExactSpelling = true)]
		unsafe static extern int __PInvoke(nint __hWnd_native, nint __hdcDst_native, POINT* __pptDst_native, SIZE* __psize_native, nint __hdcSrc_native, POINT* __pptSrc_native, uint __crKey_native, BLENDFUNCTION* __pblend_native, uint __dwFlags_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern nint GetDC(nint hWnd);

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern int ReleaseDC(nint hWnd, nint hDC);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool GetLastInputInfo(ref LASTINPUTINFO plii)
	{
		int num;
		fixed (LASTINPUTINFO* _plii_native = &plii)
		{
			num = __PInvoke(_plii_native);
		}
		return num != 0;
		[DllImport("user32.dll", EntryPoint = "GetLastInputInfo", ExactSpelling = true)]
		unsafe static extern int __PInvoke(LASTINPUTINFO* __plii_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static uint RegisterClipboardFormatW(string lpszFormat)
	{
		uint result;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(lpszFormat))
		{
			void* _lpszFormat_native = ptr;
			result = __PInvoke((ushort*)_lpszFormat_native);
		}
		return result;
		[DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", ExactSpelling = true)]
		unsafe static extern uint __PInvoke(ushort* __lpszFormat_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static uint RegisterWindowMessageW(string lpString)
	{
		uint result;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(lpString))
		{
			void* _lpString_native = ptr;
			result = __PInvoke((ushort*)_lpString_native);
		}
		return result;
		[DllImport("user32.dll", EntryPoint = "RegisterWindowMessageW", ExactSpelling = true)]
		unsafe static extern uint __PInvoke(ushort* __lpString_native);
	}

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern short GetAsyncKeyState(int vKey);

	[DllImport("user32.dll", ExactSpelling = true)]
	public static extern nint GetForegroundWindow();
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool SetForegroundWindow(nint hWnd)
	{
		return __PInvoke(hWnd) != 0;
		[DllImport("user32.dll", EntryPoint = "SetForegroundWindow", ExactSpelling = true)]
		static extern int __PInvoke(nint __hWnd_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static nint LoadImageW(nint hInst, string name, uint type, int cx, int cy, uint fuLoad)
	{
		nint result;
		int lastSystemError;
		fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(name))
		{
			void* _name_native = ptr;
			Marshal.SetLastSystemError(0);
			result = __PInvoke(hInst, (ushort*)_name_native, type, cx, cy, fuLoad);
			lastSystemError = Marshal.GetLastSystemError();
		}
		Marshal.SetLastPInvokeError(lastSystemError);
		return result;
		[DllImport("user32.dll", EntryPoint = "LoadImageW", ExactSpelling = true)]
		unsafe static extern nint __PInvoke(nint __hInst_native, ushort* __name_native, uint __type_native, int __cx_native, int __cy_native, uint __fuLoad_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static bool DestroyIcon(nint hIcon)
	{
		return __PInvoke(hIcon) != 0;
		[DllImport("user32.dll", EntryPoint = "DestroyIcon", ExactSpelling = true)]
		static extern int __PInvoke(nint __hIcon_native);
	}
}
