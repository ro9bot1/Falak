using Falak.Interop.Native;

namespace Falak.Interop;

public static class ShellDragSignal
{
	public static bool IsVisible()
	{
		nint num = User32.FindWindowW("SysDragImage", null);
		if (num != 0)
		{
			return User32.IsWindowVisible(num);
		}
		return false;
	}
}
