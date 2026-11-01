using System.Runtime.InteropServices;

namespace Falak.App;

internal static class NativeWindowStyles
{
	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", ExactSpelling = true)]

	public static extern int Get(nint hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongW", ExactSpelling = true)]

	public static extern int Set(nint hwnd, int index, int value);
}
