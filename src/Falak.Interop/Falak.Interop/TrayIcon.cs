using System;
using System.Runtime.InteropServices;
using Falak.Interop.Native;

namespace Falak.Interop;

public sealed class TrayIcon : IDisposable
{
	private const string ClassName = "FalakTrayWindow";

	private const uint CallbackMessage = 32769u;

	private static TrayIcon? s_instance;

	private static bool s_classRegistered;

	private static uint s_taskbarCreated;

	private readonly nint _hwnd;

	private nint _icon;

	private string _tooltip;

	public nint Hwnd => _hwnd;

	public bool HasIcon => _icon != 0;

	public event Action? Activated;

	public event Action<int, int>? MenuRequested;

	public TrayIcon(string iconPath, string tooltip)
	{
		s_instance = this;
		_tooltip = tooltip;
		nint moduleHandleW = Kernel32.GetModuleHandleW(null);
		RegisterClass(moduleHandleW);
		s_taskbarCreated = User32.RegisterWindowMessageW("TaskbarCreated");
		_hwnd = User32.CreateWindowExW(0u, "FalakTrayWindow", "Falak tray", 0u, 0, 0, 0, 0, 0, 0, moduleHandleW, 0);
		_icon = User32.LoadImageW(0, iconPath, 1u, User32.GetSystemMetrics(49), User32.GetSystemMetrics(50), 16u);
		Add();
	}

	public unsafe void SetTooltip(string tooltip)
	{
		_tooltip = tooltip;
		NOTIFYICONDATAW nOTIFYICONDATAW = Data(132u);
		ShellNative.Shell_NotifyIconW(1u, &nOTIFYICONDATAW);
	}

	private unsafe NOTIFYICONDATAW Data(uint flags)
	{
		NOTIFYICONDATAW result = new NOTIFYICONDATAW
		{
			cbSize = (uint)sizeof(NOTIFYICONDATAW),
			hWnd = _hwnd,
			uID = 1u,
			uFlags = flags,
			uCallbackMessage = 32769u,
			hIcon = _icon,
			uVersion = 4u
		};
		_tooltip.AsSpan(0, Math.Min(_tooltip.Length, 127)).CopyTo(new Span<char>(result.szTip, 128));
		return result;
	}

	private unsafe void Add()
	{
		NOTIFYICONDATAW nOTIFYICONDATAW = Data(135u);
		ShellNative.Shell_NotifyIconW(0u, &nOTIFYICONDATAW);
		ShellNative.Shell_NotifyIconW(4u, &nOTIFYICONDATAW);
	}

	private unsafe static void RegisterClass(nint instance)
	{
		if (!s_classRegistered)
		{
			fixed (char* lpszClassName = &"FalakTrayWindow".GetPinnableReference())
			{
				WNDCLASSEXW wNDCLASSEXW = new WNDCLASSEXW
				{
					cbSize = (uint)sizeof(WNDCLASSEXW),
					lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)(&WndProc),
					hInstance = instance,
					lpszClassName = lpszClassName
				};
				User32.RegisterClassExW(&wNDCLASSEXW);
			}
			s_classRegistered = true;
		}
	}

	[UnmanagedCallersOnly]
	private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
	{
		TrayIcon trayIcon = s_instance;
		if (trayIcon != null && hwnd == trayIcon._hwnd)
		{
			try
			{
				if (msg == 32769)
				{
					uint num = (uint)(lParam & 0xFFFF);
					if ((num == 514 || num - 1024 <= 1) ? true : false)
					{
						trayIcon.Activated?.Invoke();
					}
					else if (num == 123)
					{
						int arg = (short)(wParam & 0xFFFF);
						int arg2 = (short)((wParam >> 16) & 0xFFFF);
						User32.SetForegroundWindow(hwnd);
						trayIcon.MenuRequested?.Invoke(arg, arg2);
					}
					return 0;
				}
				if (msg == s_taskbarCreated)
				{
					trayIcon.Add();
					return 0;
				}
			}
			catch
			{
			}
		}
		return User32.DefWindowProcW(hwnd, msg, wParam, lParam);
	}

	public unsafe void Dispose()
	{
		NOTIFYICONDATAW nOTIFYICONDATAW = Data(0u);
		ShellNative.Shell_NotifyIconW(2u, &nOTIFYICONDATAW);
		if (_icon != 0)
		{
			User32.DestroyIcon(_icon);
		}
		_icon = 0;
		User32.DestroyWindow(_hwnd);
		if (s_instance == this)
		{
			s_instance = null;
		}
	}
}
