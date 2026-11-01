using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

internal static class ShellNative
{
	public const uint NIM_ADD = 0u;

	public const uint NIM_MODIFY = 1u;

	public const uint NIM_DELETE = 2u;

	public const uint NIM_SETVERSION = 4u;

	public const uint NIF_MESSAGE = 1u;

	public const uint NIF_ICON = 2u;

	public const uint NIF_TIP = 4u;

	public const uint NIF_SHOWTIP = 128u;

	public const uint NOTIFYICON_VERSION_4 = 4u;

	public const uint NIN_SELECT = 1024u;

	public const uint NIN_KEYSELECT = 1025u;

	public const uint FO_DELETE = 3u;

	public const ushort FOF_SILENT = 4;

	public const ushort FOF_NOCONFIRMATION = 16;

	public const ushort FOF_ALLOWUNDO = 64;

	public const ushort FOF_NOERRORUI = 1024;

	[DllImport("shell32.dll", ExactSpelling = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static extern bool Shell_NotifyIconW(uint dwMessage, NOTIFYICONDATAW* lpData);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
	public static extern int SHFileOperationW(ref SHFILEOPSTRUCTW lpFileOp);
}
