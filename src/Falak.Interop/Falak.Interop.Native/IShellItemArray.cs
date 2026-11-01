using System;
using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[ComImport]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemArray
{
	[PreserveSig]
	int BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppvOut);

	[PreserveSig]
	int GetPropertyStore(int flags, ref Guid riid, out nint ppv);

	[PreserveSig]
	int GetPropertyDescriptionList(nint keyType, ref Guid riid, out nint ppv);

	[PreserveSig]
	int GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);

	[PreserveSig]
	int GetCount(out uint pdwNumItems);

	[PreserveSig]
	int GetItemAt(uint dwIndex, out IShellItem ppsi);

	[PreserveSig]
	int EnumItems(out nint ppenumShellItems);
}
