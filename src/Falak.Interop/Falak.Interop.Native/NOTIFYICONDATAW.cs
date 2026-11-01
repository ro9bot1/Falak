using System;
using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NOTIFYICONDATAW
{
	public uint cbSize;

	public nint hWnd;

	public uint uID;

	public uint uFlags;

	public uint uCallbackMessage;

	public nint hIcon;

	public unsafe fixed char szTip[128];

	public uint dwState;

	public uint dwStateMask;

	public unsafe fixed char szInfo[256];

	public uint uVersion;

	public unsafe fixed char szInfoTitle[64];

	public uint dwInfoFlags;

	public Guid guidItem;

	public nint hBalloonIcon;
}
