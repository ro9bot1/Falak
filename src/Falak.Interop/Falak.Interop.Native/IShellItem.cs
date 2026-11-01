using System;
using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

[ComImport]
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
	[PreserveSig]
	int BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);

	[PreserveSig]
	int GetParent(out IShellItem ppsi);

	[PreserveSig]
	int GetDisplayName(uint sigdnName, out nint ppszName);

	[PreserveSig]
	int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

	[PreserveSig]
	int Compare(IShellItem psi, uint hint, out int piOrder);
}
