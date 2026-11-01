using System;
using System.Collections.Generic;

namespace Falak.Core;

public sealed class DragTracker
{
	private enum State
	{
		Idle,
		Pressed,
		Dragging
	}

	private readonly GestureKeyMap _keys;

	private readonly Func<bool> _isShellDragVisible;

	private readonly Func<bool> _isLeftButtonDown;

	private readonly int _halfThresholdX;

	private readonly int _halfThresholdY;

	private readonly Action<GestureEvent> _emit;

	private readonly HashSet<int> _keysDown = new HashSet<int>();

	private readonly HashSet<int> _swallowedDown = new HashSet<int>();

	private State _state;

	private int _downX;

	private int _downY;

	private int _lastX;

	private int _lastY;

	private WheelMode? _heldModeAwaitingConfirm;

	private volatile bool _enabled = true;

	public bool IsDragging => _state == State.Dragging;

	public bool Enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			_enabled = value;
		}
	}

	public DragTracker(GestureKeyMap keys, Func<bool> isShellDragVisible, Func<bool> isLeftButtonDown, int dragThresholdX, int dragThresholdY, Action<GestureEvent> emit)
	{
		_keys = keys;
		_isShellDragVisible = isShellDragVisible;
		_isLeftButtonDown = isLeftButtonDown;
		_halfThresholdX = Math.Max(1, dragThresholdX / 2);
		_halfThresholdY = Math.Max(1, dragThresholdY / 2);
		_emit = emit;
	}

	public void OnMouseDown(int x, int y, long timestamp)
	{
		_state = State.Pressed;
		_downX = (_lastX = x);
		_downY = (_lastY = y);
		_heldModeAwaitingConfirm = null;
	}

	public void OnMouseMove(int x, int y, long timestamp)
	{
		_lastX = x;
		_lastY = y;
		if (_state == State.Pressed)
		{
			if (Math.Abs(x - _downX) <= _halfThresholdX && Math.Abs(y - _downY) <= _halfThresholdY)
			{
				return;
			}
			_state = State.Dragging;
			_emit(new DragArmed(x, y, timestamp));
			foreach (var (item, value) in _keys.Entries)
			{
				if (_keysDown.Contains(item))
				{
					_heldModeAwaitingConfirm = value;
					break;
				}
			}
		}
		if (_state != State.Dragging)
		{
			return;
		}
		WheelMode? heldModeAwaitingConfirm = _heldModeAwaitingConfirm;
		if (heldModeAwaitingConfirm.HasValue)
		{
			WheelMode valueOrDefault = heldModeAwaitingConfirm.GetValueOrDefault();
			if (_enabled && _isShellDragVisible())
			{
				_heldModeAwaitingConfirm = null;
				_emit(new WheelRequested(valueOrDefault, x, y, timestamp));
			}
		}
	}

	public void OnMouseUp(int x, int y, long timestamp)
	{
		if (_state == State.Dragging)
		{
			_emit(new DragEnded(x, y, Cancelled: false, timestamp));
		}
		_state = State.Idle;
		_heldModeAwaitingConfirm = null;
	}

	public bool OnKey(int vk, bool isDown, long timestamp)
	{
		int num = VirtualKeys.Normalize(vk);
		if (num == 27)
		{
			if (isDown && _state == State.Dragging)
			{
				_emit(new DragEnded(_lastX, _lastY, Cancelled: true, timestamp));
				_state = State.Idle;
				_heldModeAwaitingConfirm = null;
			}
			return false;
		}
		if (!_keys.TryGetMode(num, out var mode))
		{
			return false;
		}
		if (!isDown)
		{
			_keysDown.Remove(num);
			return _swallowedDown.Remove(num);
		}
		if (!_keysDown.Add(num))
		{
			return _swallowedDown.Contains(num);
		}
		if (!_enabled || _state != State.Dragging)
		{
			return false;
		}
		if (!_isLeftButtonDown())
		{
			_state = State.Idle;
			return false;
		}
		if (!_isShellDragVisible())
		{
			return false;
		}
		_heldModeAwaitingConfirm = null;
		_emit(new WheelRequested(mode, _lastX, _lastY, timestamp));
		_swallowedDown.Add(num);
		return true;
	}
}
