namespace Falak.Interop.Native;

internal struct MONITORINFO
{
	public uint cbSize;

	public RECT rcMonitor;

	public RECT rcWork;

	public uint dwFlags;
}
