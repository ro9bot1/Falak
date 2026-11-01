using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Falak.Core;
using Falak.Interop.Native;

namespace Falak.Interop;

public sealed class LowLevelHooks : IDisposable
{
	private static LowLevelHooks? s_active;

	private readonly DragTracker _tracker;

	private readonly ManualResetEventSlim _started = new ManualResetEventSlim();

	private Thread? _thread;

	private uint _threadId;

	private nint _mouseHook;

	private nint _keyboardHook;

	private int _startError;

	private int _lastCallbackTick;

	public bool IsRunning
	{
		get
		{
			if (_mouseHook != 0)
			{
				return _keyboardHook != 0;
			}
			return false;
		}
	}

	public int LastCallbackTick => Volatile.Read(in _lastCallbackTick);

	public LowLevelHooks(DragTracker tracker)
	{
		_tracker = tracker;
	}

	public void Start()
	{
		LowLevelHooks lowLevelHooks = Interlocked.CompareExchange(ref s_active, this, null);
		if (lowLevelHooks != null && lowLevelHooks != this)
		{
			throw new InvalidOperationException("Only one set of hooks may run per process.");
		}
		_started.Reset();
		_thread = new Thread(Run)
		{
			IsBackground = true,
			Priority = ThreadPriority.Highest,
			Name = "Falak.Hooks"
		};
		_thread.Start();
		_started.Wait(TimeSpan.FromSeconds(3L));
		if (!IsRunning)
		{
			Stop();
			throw new Win32Exception(_startError, "Could not install the low-level input hooks.");
		}
		Volatile.Write(ref _lastCallbackTick, Environment.TickCount);
	}

	public void Stop()
	{
		Thread thread = _thread;
		if (thread != null && thread.IsAlive)
		{
			User32.PostThreadMessageW(_threadId, 18u, 0, 0);
			_thread.Join(TimeSpan.FromSeconds(2L));
		}
		_thread = null;
		Interlocked.CompareExchange(ref s_active, null, this);
	}

	public void Restart()
	{
		Stop();
		Start();
	}

	public void Dispose()
	{
		Stop();
		_started.Dispose();
	}

	private unsafe void Run()
	{
		_threadId = Kernel32.GetCurrentThreadId();
		nint moduleHandleW = Kernel32.GetModuleHandleW(null);
		_mouseHook = User32.SetWindowsHookExW(14, (delegate* unmanaged<int, nint, nint, nint>)(&MouseProc), moduleHandleW, 0u);
		if (_mouseHook == 0)
		{
			_startError = Marshal.GetLastPInvokeError();
		}
		_keyboardHook = User32.SetWindowsHookExW(13, (delegate* unmanaged<int, nint, nint, nint>)(&KeyboardProc), moduleHandleW, 0u);
		if (_keyboardHook == 0)
		{
			_startError = Marshal.GetLastPInvokeError();
		}
		_started.Set();
		MSG mSG = default;
		while (User32.GetMessageW(&mSG, 0, 0u, 0u) > 0)
		{
			User32.TranslateMessage(&mSG);
			User32.DispatchMessageW(&mSG);
		}
		if (_mouseHook != 0)
		{
			User32.UnhookWindowsHookEx(_mouseHook);
		}
		if (_keyboardHook != 0)
		{
			User32.UnhookWindowsHookEx(_keyboardHook);
		}
		_mouseHook = (_keyboardHook = 0);
	}

	[UnmanagedCallersOnly]
	private unsafe static nint MouseProc(int code, nint wParam, nint lParam)
	{
		if (code == 0)
		{
			LowLevelHooks lowLevelHooks = s_active;
			if (lowLevelHooks != null)
			{
				try
				{
					Volatile.Write(ref lowLevelHooks._lastCallbackTick, Environment.TickCount);
					long timestamp = Stopwatch.GetTimestamp();
					switch ((uint)wParam)
					{
					case 512u:
						lowLevelHooks._tracker.OnMouseMove(((MSLLHOOKSTRUCT*)lParam)->pt.X, ((MSLLHOOKSTRUCT*)lParam)->pt.Y, timestamp);
						break;
					case 513u:
						lowLevelHooks._tracker.OnMouseDown(((MSLLHOOKSTRUCT*)lParam)->pt.X, ((MSLLHOOKSTRUCT*)lParam)->pt.Y, timestamp);
						break;
					case 514u:
						lowLevelHooks._tracker.OnMouseUp(((MSLLHOOKSTRUCT*)lParam)->pt.X, ((MSLLHOOKSTRUCT*)lParam)->pt.Y, timestamp);
						break;
					}
				}
				catch
				{
				}
			}
		}
		return User32.CallNextHookEx(0, code, wParam, lParam);
	}

	[UnmanagedCallersOnly]
	private unsafe static nint KeyboardProc(int code, nint wParam, nint lParam)
	{
		if (code == 0)
		{
			LowLevelHooks lowLevelHooks = s_active;
			if (lowLevelHooks != null)
			{
				try
				{
					Volatile.Write(ref lowLevelHooks._lastCallbackTick, Environment.TickCount);
					uint num = (uint)wParam;
					bool flag = ((num == 256 || num == 260) ? true : false);
					bool flag2 = flag;
					flag = ((num == 257 || num == 261) ? true : false);
					bool flag3 = flag;
					if ((flag2 | flag3) && lowLevelHooks._tracker.OnKey((int)((KBDLLHOOKSTRUCT*)lParam)->vkCode, flag2, Stopwatch.GetTimestamp()))
					{
						return 1;
					}
				}
				catch
				{
				}
			}
		}
		return User32.CallNextHookEx(0, code, wParam, lParam);
	}

	public static bool IsPrimaryButtonDown()
	{
		return (User32.GetAsyncKeyState((User32.GetSystemMetrics(23) == 0) ? 1 : 2) & 0x8000) != 0;
	}

	public static (int X, int Y) DragThreshold()
	{
		return (X: User32.GetSystemMetrics(68), Y: User32.GetSystemMetrics(69));
	}

	public unsafe static int LastInputTick()
	{
		LASTINPUTINFO plii = new LASTINPUTINFO
		{
			cbSize = (uint)sizeof(LASTINPUTINFO)
		};
		if (!User32.GetLastInputInfo(ref plii))
		{
			return Environment.TickCount;
		}
		return (int)plii.dwTime;
	}
}
