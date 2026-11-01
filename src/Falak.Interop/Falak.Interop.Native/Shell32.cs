using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Falak.Interop.Native;

internal static class Shell32
{
	private const string Dll = "shell32.dll";

	[DllImport("shell32.dll", ExactSpelling = true)]
	public unsafe static extern uint DragQueryFileW(nint hDrop, uint iFile, char* lpszFile, uint cch);
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static int SHParseDisplayName(string pszName, nint pbc, out nint ppidl, uint sfgaoIn, out uint psfgaoOut)
	{
		ppidl = 0;
		psfgaoOut = 0u;
		int result;
		fixed (uint* _psfgaoOut_native = &psfgaoOut)
		{
			fixed (nint* _ppidl_native = &ppidl)
			{
				fixed (char* ptr = &Utf16StringMarshaller.GetPinnableReference(pszName))
				{
					void* _pszName_native = ptr;
					result = __PInvoke((ushort*)_pszName_native, pbc, _ppidl_native, sfgaoIn, _psfgaoOut_native);
				}
			}
		}
		return result;
		[DllImport("shell32.dll", EntryPoint = "SHParseDisplayName", ExactSpelling = true)]
		unsafe static extern int __PInvoke(ushort* __pszName_native, nint __pbc_native, nint* __ppidl_native, uint __sfgaoIn_native, uint* __psfgaoOut_native);
	}
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	public unsafe static int SHCreateShellItemArrayFromIDLists(uint cidl, nint* rgpidl, out nint ppsiItemArray)
	{
		ppsiItemArray = 0;
		int result;
		fixed (nint* _ppsiItemArray_native = &ppsiItemArray)
		{
			result = __PInvoke(cidl, rgpidl, _ppsiItemArray_native);
		}
		return result;
		[DllImport("shell32.dll", EntryPoint = "SHCreateShellItemArrayFromIDLists", ExactSpelling = true)]
		unsafe static extern int __PInvoke(uint __cidl_native, nint* __rgpidl_native, nint* __ppsiItemArray_native);
	}

	[DllImport("shell32.dll", ExactSpelling = true)]
	public static extern void ILFree(nint pidl);

	[DllImport("shell32.dll", ExactSpelling = true)]
	public static extern int SHCreateShellItemArrayFromDataObject([MarshalAs(UnmanagedType.Interface)] object pdo, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppv);
}
