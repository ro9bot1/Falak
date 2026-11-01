namespace Falak.Interop.Native;

internal struct KBDLLHOOKSTRUCT
{
	public uint vkCode;

	public uint scanCode;

	public uint flags;

	public uint time;

	public nint dwExtraInfo;
}
