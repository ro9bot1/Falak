using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Falak.Interop.Native;

internal static class Ole32
{
	private const string Dll = "ole32.dll";

	[DllImport("ole32.dll", ExactSpelling = true)]
	public static extern int OleInitialize(nint pvReserved);

	[DllImport("ole32.dll", ExactSpelling = true)]
	public static extern int RegisterDragDrop(nint hwnd, [MarshalAs(UnmanagedType.Interface)] IOleDropTarget pDropTarget);

	[DllImport("ole32.dll", ExactSpelling = true)]
	public static extern int RevokeDragDrop(nint hwnd);

	[DllImport("ole32.dll", ExactSpelling = true)]
	public static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);
}
