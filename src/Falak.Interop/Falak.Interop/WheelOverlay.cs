using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Falak.Core;
using Falak.Interop.Native;

namespace Falak.Interop;

public sealed class WheelOverlay : IWheelSurface, IDisposable
{
	private const string ClassName = "FalakWheelOverlay";

	private static bool s_classRegistered;

	private WheelRenderer _renderer;

	private readonly Func<string, IReadOnlyList<string>, bool> _isAvailable;

	private readonly Action<string> _log;

	private WheelDropTarget? _dropTarget;

	private nint _hwnd;

	private nint _memDc;

	private nint _dib;

	private nint _previousBitmap;

	private unsafe void* _bits;

	private int _dibSize;

	private int _left;

	private int _top;

	private double _scale = 1.0;

	private IReadOnlyList<string> _paths = Array.Empty<string>();

	private WheelMenu _menu = SliceCatalog.For(WheelMode.Convert, FileKind.Pending);

	public nint Hwnd => _hwnd;

	public bool IsVisible { get; private set; }

	public bool HasDragEnter { get; private set; }

	public WheelMode Mode { get; private set; }

	public FileKind Kind { get; private set; }

	public WheelHit Highlight { get; private set; } = WheelHit.None;

	public WheelMenu Menu => _menu;

	public bool SoundEnabled { get; set; } = true;

	public event Action<DroppedFiles, FileKind>? Entered;

	public event Action<WheelDropResult>? Dropped;

	public WheelOverlay(WheelRenderer renderer, Func<string, IReadOnlyList<string>, bool> isAvailable, Action<string> log)
	{
		_renderer = renderer;
		_isAvailable = isAvailable;
		_log = log;
	}

	public void SetRenderer(WheelRenderer renderer)
	{
		_renderer = renderer;
	}

	public void Create()
	{
		int num = Ole32.OleInitialize(0);
		if (num < 0)
		{
			Marshal.ThrowExceptionForHR(num);
		}
		nint moduleHandleW = Kernel32.GetModuleHandleW(null);
		RegisterClass(moduleHandleW);
		_hwnd = User32.CreateWindowExW(134742152u, "FalakWheelOverlay", "Falak wheel", 2147483648u, 0, 0, 1, 1, 0, 0, moduleHandleW, 0);
		if (_hwnd == 0)
		{
			throw new Win32Exception(Marshal.GetLastPInvokeError());
		}
		_dropTarget = new WheelDropTarget(this, _log);
		num = Ole32.RegisterDragDrop(_hwnd, _dropTarget);
		if (num < 0)
		{
			Marshal.ThrowExceptionForHR(num);
		}
		_memDc = Gdi32.CreateCompatibleDC(0);
	}

	public void Prewarm(params double[] scales)
	{
		foreach (double scale in scales)
		{
			WheelMode[] values = Enum.GetValues<WheelMode>();
			foreach (WheelMode mode in values)
			{
				_renderer.Render(SliceCatalog.For(mode, FileKind.Pending), mode, WheelHit.None, new WheelGeometry(6, scale));
			}
		}
	}

	public unsafe void Show(WheelMode mode, int x, int y)
	{
		Mode = mode;
		if (!IsVisible)
		{
			HasDragEnter = false;
			_paths = Array.Empty<string>();
			Kind = FileKind.Pending;
		}
		Highlight = WheelHit.None;
		RebuildMenu();
		nint num = User32.MonitorFromPoint(new POINT(x, y), 2u);
		_scale = ((Shcore.GetDpiForMonitor(num, 0, out var dpiX, out var _) == 0) ? ((double)dpiX / 96.0) : 1.0);
		MONITORINFO lpmi = new MONITORINFO
		{
			cbSize = (uint)sizeof(MONITORINFO)
		};
		User32.GetMonitorInfoW(num, ref lpmi);
		int sizePx = Geometry().SizePx;
		_left = ClampToRange(x - sizePx / 2, lpmi.rcWork.Left, lpmi.rcWork.Right - sizePx);
		_top = ClampToRange(y - sizePx / 2, lpmi.rcWork.Top, lpmi.rcWork.Bottom - sizePx);
		Present();
		if (!IsVisible)
		{
			User32.ShowWindow(_hwnd, 4);
			IsVisible = true;
		}
		User32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, 19u);
	}

	public void SetFiles(IReadOnlyList<string> paths)
	{
		if (paths.Count != 0)
		{
			_paths = paths;
			Kind = FileKinds.Classify(paths, Directory.Exists);
			RebuildMenu();
			if (IsVisible)
			{
				Present();
			}
		}
	}

	public void Hide()
	{
		if (IsVisible)
		{
			User32.ShowWindow(_hwnd, 0);
			IsVisible = false;
			Highlight = WheelHit.None;
		}
	}

	private void RebuildMenu()
	{
		string sourceExtension = ((_paths.Select((string p) => Path.GetExtension(p).ToLowerInvariant()).Distinct().Count() == 1) ? Path.GetExtension(_paths[0]) : null);
		IReadOnlyList<string> paths = _paths;
		_menu = SliceCatalog.For(Mode, Kind, sourceExtension, (string id) => _isAvailable(id, paths), paths.Count);
	}

	private static int ClampToRange(int value, int min, int max)
	{
		return Math.Max(min, Math.Min(value, max));
	}

	private WheelGeometry Geometry()
	{
		return new WheelGeometry(_menu.Slices.Count, _scale);
	}

	private unsafe void Present()
	{
		WheelGeometry wheelGeometry = Geometry();
		byte[] array = _renderer.Render(_menu, Mode, Highlight, wheelGeometry);
		int sizePx = wheelGeometry.SizePx;
		EnsureSurface(sizePx);
		Marshal.Copy(array, 0, (nint)_bits, array.Length);
		POINT pOINT = new POINT(_left, _top);
		SIZE sIZE = new SIZE
		{
			Cx = sizePx,
			Cy = sizePx
		};
		POINT pOINT2 = new POINT(0, 0);
		BLENDFUNCTION bLENDFUNCTION = new BLENDFUNCTION
		{
			BlendOp = 0,
			SourceConstantAlpha = byte.MaxValue,
			AlphaFormat = 1
		};
		if (!User32.UpdateLayeredWindow(_hwnd, 0, &pOINT, &sIZE, _memDc, &pOINT2, 0u, &bLENDFUNCTION, 2u))
		{
			_log($"UpdateLayeredWindow failed: error {Marshal.GetLastPInvokeError()}");
		}
	}

	private unsafe void EnsureSurface(int size)
	{
		if (_dibSize != size || _dib == 0)
		{
			ReleaseSurface();
			BITMAPINFOHEADER bITMAPINFOHEADER = new BITMAPINFOHEADER
			{
				biSize = (uint)sizeof(BITMAPINFOHEADER),
				biWidth = size,
				biHeight = -size,
				biPlanes = 1,
				biBitCount = 32
			};
			_dib = Gdi32.CreateDIBSection(_memDc, &bITMAPINFOHEADER, 0u, out var ppvBits, 0, 0u);
			if (_dib == 0)
			{
				throw new InvalidOperationException("CreateDIBSection failed.");
			}
			_bits = (void*)ppvBits;
			_previousBitmap = Gdi32.SelectObject(_memDc, _dib);
			_dibSize = size;
		}
	}

	private unsafe void ReleaseSurface()
	{
		if (_dib != 0)
		{
			Gdi32.SelectObject(_memDc, _previousBitmap);
			Gdi32.DeleteObject(_dib);
			_dib = 0;
			_bits = null;
			_dibSize = 0;
		}
	}

	public WheelHit HitTestScreen(int x, int y)
	{
		if (!IsVisible)
		{
			return WheelHit.None;
		}
		WheelGeometry wheelGeometry = Geometry();
		double num = (double)wheelGeometry.SizePx / 2.0;
		WheelHit wheelHit = wheelGeometry.HitTest((double)x - ((double)_left + num), (double)y - ((double)_top + num));
		if ((object)_menu.ActionFor(wheelHit) != null)
		{
			return wheelHit;
		}
		return WheelHit.None;
	}

	public void OnDragEntered(DroppedFiles files)
	{
		HasDragEnter = true;
		if (files.Paths.Count > 0)
		{
			SetFiles(files.Paths);
		}
		else if (files.Count > 0)
		{
			_paths = Array.Empty<string>();
			Kind = FileKind.Unknown;
			RebuildMenu();
			if (IsVisible)
			{
				Present();
			}
		}
		Entered?.Invoke(files, Kind);
	}

	public void OnHover(WheelHit hit)
	{
		if (!(hit == Highlight) && IsVisible)
		{
			Highlight = hit;
			Present();
			if (SoundEnabled && hit.Kind == WheelHitKind.Slice)
			{
				WheelSound.Tick();
			}
		}
	}

	public void OnDragLeft()
	{
		if (IsVisible && !(Highlight == WheelHit.None))
		{
			Highlight = WheelHit.None;
			Present();
		}
	}

	public void OnDropped(WheelHit hit, DroppedFiles files, DropEffect answered)
	{
		WheelDropResult obj = new WheelDropResult(Mode, Kind, _menu.ActionFor(hit), files, answered);
		Hide();
		Dropped?.Invoke(obj);
	}

	private unsafe static void RegisterClass(nint instance)
	{
		if (s_classRegistered)
		{
			return;
		}
		fixed (char* lpszClassName = &"FalakWheelOverlay".GetPinnableReference())
		{
			WNDCLASSEXW wNDCLASSEXW = new WNDCLASSEXW
			{
				cbSize = (uint)sizeof(WNDCLASSEXW),
				lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)(&WndProc),
				hInstance = instance,
				lpszClassName = lpszClassName
			};
			if (User32.RegisterClassExW(&wNDCLASSEXW) == 0 && Marshal.GetLastPInvokeError() != 1410)
			{
				throw new Win32Exception(Marshal.GetLastPInvokeError());
			}
		}
		s_classRegistered = true;
	}

	[UnmanagedCallersOnly]
	private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
	{
		if (msg != 33)
		{
			return User32.DefWindowProcW(hwnd, msg, wParam, lParam);
		}
		return 3;
	}

	public void Dispose()
	{
		if (_hwnd != 0)
		{
			Ole32.RevokeDragDrop(_hwnd);
			User32.DestroyWindow(_hwnd);
			_hwnd = 0;
		}
		ReleaseSurface();
		if (_memDc != 0)
		{
			Gdi32.DeleteDC(_memDc);
			_memDc = 0;
		}
	}
}
