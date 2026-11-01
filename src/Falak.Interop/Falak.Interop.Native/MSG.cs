namespace Falak.Interop.Native;

internal struct MSG
{
	public nint hwnd;

	public uint message;

	public nint wParam;

	public nint lParam;

	public uint time;

	public POINT pt;

	public uint lPrivate;
}
