using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[ComImport]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleDropTarget
{
	[PreserveSig]
	int DragEnter([MarshalAs(UnmanagedType.Interface)] object pDataObj, int grfKeyState, long pt, ref int pdwEffect);

	[PreserveSig]
	int DragOver(int grfKeyState, long pt, ref int pdwEffect);

	[PreserveSig]
	int DragLeave();

	[PreserveSig]
	int Drop([MarshalAs(UnmanagedType.Interface)] object pDataObj, int grfKeyState, long pt, ref int pdwEffect);
}
