namespace Falak.Interop.Native;

internal struct MSLLHOOKSTRUCT
{
	public POINT pt;

	public uint mouseData;

	public uint flags;

	public uint time;

	public nint dwExtraInfo;
}
