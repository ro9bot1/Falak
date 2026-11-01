using System;
using System.Collections.Generic;

namespace Falak.Core;

public sealed class GestureKeyMap
{
	private readonly Dictionary<int, WheelMode> _modes = new Dictionary<int, WheelMode>();

	public static GestureKeyMap Default => new GestureKeyMap().Assign(16, WheelMode.Convert).Assign(17, WheelMode.Tools).Assign(32, WheelMode.Actions);

	public IEnumerable<KeyValuePair<int, WheelMode>> Entries => _modes;

	public GestureKeyMap Assign(int vk, WheelMode mode)
	{
		vk = VirtualKeys.Normalize(vk);
		if ((vk == 27 || (uint)(vk - 91) <= 1u) ? true : false)
		{
			throw new ArgumentException("Escape and the Windows key cannot open a wheel.", "vk");
		}
		_modes[vk] = mode;
		return this;
	}

	public bool TryGetMode(int vk, out WheelMode mode)
	{
		return _modes.TryGetValue(VirtualKeys.Normalize(vk), out mode);
	}
}
