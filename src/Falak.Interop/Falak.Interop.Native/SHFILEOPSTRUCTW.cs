using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SHFILEOPSTRUCTW
{
	public nint hwnd;

	public uint wFunc;

	public string pFrom;

	public string? pTo;

	public ushort fFlags;

	[MarshalAs(UnmanagedType.Bool)]
	public bool fAnyOperationsAborted;

	public nint hNameMappings;

	public string? lpszProgressTitle;
}
