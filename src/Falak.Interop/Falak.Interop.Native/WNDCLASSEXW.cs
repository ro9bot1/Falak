namespace Falak.Interop.Native;

internal struct WNDCLASSEXW
{
	public uint cbSize;

	public uint style;

	public nint lpfnWndProc;

	public int cbClsExtra;

	public int cbWndExtra;

	public nint hInstance;

	public nint hIcon;

	public nint hCursor;

	public nint hbrBackground;

	public unsafe char* lpszMenuName;

	public unsafe char* lpszClassName;

	public nint hIconSm;
}
