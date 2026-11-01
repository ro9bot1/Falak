using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using Falak.Core;
using Falak.Interop.Native;

namespace Falak.Interop;

public sealed class GestureEngine : IDisposable
{
	private const int DismissIfNoDragEnterMs = 450;

	private const int DismissAfterReleaseMs = 400;

	private const int CursorSlackPx = 8;

	private const int HookSilenceLimitMs = 2000;

	private readonly Dispatcher _ui;

	private readonly WheelOverlay _overlay;

	private readonly DragTracker _tracker;

	private readonly LowLevelHooks _hooks;

	private readonly DispatcherTimer _dismissTimer;

	private readonly DispatcherTimer _watchdog;

	private POINT _shownAt;

	private long _shownTick;

	private bool _buttonReleased;

	private long _releasedTick;

	private int _showGeneration;

	public bool HooksRunning => _hooks.IsRunning;

	public bool SoundEnabled
	{
		get
		{
			return _overlay.SoundEnabled;
		}
		set
		{
			_overlay.SoundEnabled = value;
		}
	}

	public bool Paused
	{
		get
		{
			return !_tracker.Enabled;
		}
		set
		{
			_tracker.Enabled = !value;
			if (value)
			{
				_overlay.Hide();
			}
		}
	}

	public event Action<string>? Log;

	public event Action<WheelDropResult>? Dropped;

	public event Action<double>? WheelShown;

	public GestureEngine(Dispatcher ui, GestureKeyMap keys, BrandPalette palette, Func<string, IReadOnlyList<string>, bool> isAvailable, bool arabic = true, bool glass = false)
	{
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected Obj, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Expected Obj, but got Unknown
		_ui = ui;
		_overlay = new WheelOverlay(new WheelRenderer(palette, arabic, glass), isAvailable, Write);
		_overlay.Dropped += (WheelDropResult result) =>
		{
			Dropped?.Invoke(result);
		};
		_overlay.Dropped += (WheelDropResult _) =>
		{
			_dismissTimer.Stop();
		};
		(int X, int Y) tuple = LowLevelHooks.DragThreshold();
		int item = tuple.X;
		int item2 = tuple.Y;
		_tracker = new DragTracker(keys, ShellDragSignal.IsVisible, LowLevelHooks.IsPrimaryButtonDown, item, item2, (GestureEvent e) =>
		{
			_ui.BeginInvoke((DispatcherPriority)10, (Delegate)(Action)(() =>
			{
				Handle(e);
			}));
		});
		_hooks = new LowLevelHooks(_tracker);
		_dismissTimer = new DispatcherTimer((DispatcherPriority)5, ui)
		{
			Interval = TimeSpan.FromMilliseconds(40L)
		};
		_dismissTimer.Tick += (object? _, EventArgs _) =>
		{
			CheckDismiss();
		};
		_watchdog = new DispatcherTimer((DispatcherPriority)4, ui)
		{
			Interval = TimeSpan.FromSeconds(3L)
		};
		_watchdog.Tick += (object? _, EventArgs _) =>
		{
			CheckHooksAlive();
		};
	}

	public void SetPalette(BrandPalette palette, bool glass, bool arabic = true)
	{
		_overlay.SetRenderer(new WheelRenderer(palette, arabic, glass));
	}

	public void Start()
	{
		_overlay.Create();
		Stopwatch stopwatch = Stopwatch.StartNew();
		_overlay.Prewarm(1.0, 1.25, 1.5, 2.0);
		Write($"Wheel window ready, frames prewarmed in {stopwatch.ElapsedMilliseconds} ms.");
		_hooks.Start();
		_watchdog.Start();
		Write("Low-level mouse and keyboard hooks installed.");
	}

	private void Write(string line)
	{
		Log?.Invoke(line);
	}

	private void Handle(GestureEvent e)
	{
		if (!(e is WheelRequested wheelRequested))
		{
			if (!(e is DragEnded dragEnded))
			{
				return;
			}
			if (dragEnded.Cancelled)
			{
				if (_overlay.IsVisible)
				{
					_overlay.Hide();
					Write("Drag cancelled with Escape; wheel hidden.");
				}
			}
			else if (_overlay.IsVisible)
			{
				_buttonReleased = true;
				_releasedTick = Environment.TickCount64;
			}
			return;
		}
		bool isVisible = _overlay.IsVisible;
		nint explorerWindow = ShellServices.ForegroundWindow();
		_overlay.Show(wheelRequested.Mode, wheelRequested.X, wheelRequested.Y);
		double totalMilliseconds = Stopwatch.GetElapsedTime(wheelRequested.Timestamp).TotalMilliseconds;
		_shownAt = new POINT(wheelRequested.X, wheelRequested.Y);
		_shownTick = Environment.TickCount64;
		_buttonReleased = false;
		_dismissTimer.Start();
		WheelShown?.Invoke(totalMilliseconds);
		Write($"{wheelRequested.Mode} wheel shown in {totalMilliseconds:0.0} ms at ({wheelRequested.X},{wheelRequested.Y}).");
		if (!isVisible)
		{
			PrefetchSelection(explorerWindow, ++_showGeneration);
		}
	}

	private async Task PrefetchSelection(nint explorerWindow, int generation)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		IReadOnlyList<string> paths = await ShellServices.ExplorerSelectionAsync(explorerWindow, TimeSpan.FromMilliseconds(400L));
		await _ui.InvokeAsync((Action)(() =>
		{
			if (generation == _showGeneration && _overlay.IsVisible && !_overlay.HasDragEnter && paths.Count != 0)
			{
				_overlay.SetFiles(paths);
				Write($"Explorer selection read in {stopwatch.ElapsedMilliseconds} ms: {paths.Count} item(s), {_overlay.Kind}.");
			}
		}));
	}

	private void CheckDismiss()
	{
		if (!_overlay.IsVisible)
		{
			_dismissTimer.Stop();
			return;
		}
		long tickCount = Environment.TickCount64;
		POINT lpPoint;
		if (_buttonReleased && tickCount - _releasedTick > 400)
		{
			_overlay.Hide();
			Write("Button released away from the wheel; wheel hidden.");
		}
		else if (!_overlay.HasDragEnter && tickCount - _shownTick > 450 && User32.GetCursorPos(out lpPoint) && (Math.Abs(lpPoint.X - _shownAt.X) > 8 || Math.Abs(lpPoint.Y - _shownAt.Y) > 8))
		{
			_overlay.Hide();
			Write("No file drag reached the wheel; wheel hidden.");
		}
	}

	private void CheckHooksAlive()
	{
		int num = LowLevelHooks.LastInputTick() - _hooks.LastCallbackTick;
		if (num <= 2000)
		{
			return;
		}
		Write($"Hooks silent for {num} ms while input continued; reinstalling.");
		try
		{
			_hooks.Restart();
		}
		catch (Exception ex)
		{
			Write("Hook reinstall failed: " + ex.Message);
		}
	}

	public void Dispose()
	{
		_watchdog.Stop();
		_dismissTimer.Stop();
		_hooks.Dispose();
		_overlay.Dispose();
	}
}
