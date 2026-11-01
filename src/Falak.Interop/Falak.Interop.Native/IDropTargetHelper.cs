using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[ComImport]
[Guid("4657278B-411B-11D2-839A-00C04FD918D0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDropTargetHelper
{
	[PreserveSig]
	int DragEnter(nint hwndTarget, [MarshalAs(UnmanagedType.Interface)] object pDataObject, ref POINT ppt, int dwEffect);

	[PreserveSig]
	int DragLeave();

	[PreserveSig]
	int DragOver(ref POINT ppt, int dwEffect);

	[PreserveSig]
	int Drop([MarshalAs(UnmanagedType.Interface)] object pDataObject, ref POINT ppt, int dwEffect);

	[PreserveSig]
	int Show([MarshalAs(UnmanagedType.Bool)] bool fShow);
}
