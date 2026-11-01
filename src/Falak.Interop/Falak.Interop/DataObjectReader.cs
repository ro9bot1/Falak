using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Falak.Interop.Native;

namespace Falak.Interop;

internal static class DataObjectReader
{
	private static readonly short CfFileDescriptorW = (short)User32.RegisterClipboardFormatW("FileGroupDescriptorW");

	private static readonly short CfPerformedDropEffect = (short)User32.RegisterClipboardFormatW("Performed DropEffect");

	private static readonly short CfLogicalPerformedDropEffect = (short)User32.RegisterClipboardFormatW("Logical Performed DropEffect");

	public static DroppedFiles Read(IDataObject? data)
	{
		if (data == null)
		{
			return DroppedFiles.Empty;
		}
		return TryHDrop(data) ?? TryShellItems(data) ?? TryVirtualFiles(data) ?? DroppedFiles.Empty;
	}

	private static FORMATETC Format(short cf)
	{
		return new FORMATETC
		{
			cfFormat = cf,
			ptd = 0,
			dwAspect = DVASPECT.DVASPECT_CONTENT,
			lindex = -1,
			tymed = TYMED.TYMED_HGLOBAL
		};
	}

	private unsafe static DroppedFiles? TryHDrop(IDataObject data)
	{
		FORMATETC format = Format(15);
		if (data.QueryGetData(ref format) != 0)
		{
			return null;
		}
		data.GetData(ref format, out var medium);
		try
		{
			nint unionmember = medium.unionmember;
			uint num = Shell32.DragQueryFileW(unionmember, uint.MaxValue, null, 0u);
			List<string> list = new List<string>((int)num);
			for (uint num2 = 0u; num2 < num; num2++)
			{
				uint num3 = Shell32.DragQueryFileW(unionmember, num2, null, 0u);
				char[] array = new char[num3 + 1];
				fixed (char* lpszFile = array)
				{
					Shell32.DragQueryFileW(unionmember, num2, lpszFile, num3 + 1);
				}
				list.Add(new string(array, 0, (int)num3));
			}
			return new DroppedFiles(list, Array.Empty<string>(), "CF_HDROP");
		}
		finally
		{
			Ole32.ReleaseStgMedium(ref medium);
		}
	}

	private static DroppedFiles? TryShellItems(IDataObject data)
	{
		Guid riid = ComIds.IID_IShellItemArray;
		if (Shell32.SHCreateShellItemArrayFromDataObject(data, ref riid, out IShellItemArray ppv) != 0)
		{
			return null;
		}
		try
		{
			if (ppv.GetCount(out var pdwNumItems) != 0)
			{
				return null;
			}
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			for (uint num = 0u; num < pdwNumItems; num++)
			{
				if (ppv.GetItemAt(num, out IShellItem ppsi) != 0)
				{
					continue;
				}
				try
				{
					string text = TryDisplayName(ppsi, 2147844096u);
					if (text != null)
					{
						list.Add(text);
						continue;
					}
					string text2 = TryDisplayName(ppsi, 0u);
					if (text2 != null)
					{
						list2.Add(text2);
					}
				}
				finally
				{
					Marshal.ReleaseComObject(ppsi);
				}
			}
			return new DroppedFiles(list, list2, "Shell IDList");
		}
		finally
		{
			Marshal.ReleaseComObject(ppv);
		}
	}

	private static string? TryDisplayName(IShellItem item, uint sigdn)
	{
		if (item.GetDisplayName(sigdn, out var ppszName) != 0 || ppszName == 0)
		{
			return null;
		}
		try
		{
			return Marshal.PtrToStringUni(ppszName);
		}
		finally
		{
			Marshal.FreeCoTaskMem(ppszName);
		}
	}

	private unsafe static DroppedFiles? TryVirtualFiles(IDataObject data)
	{
		FORMATETC format = Format(CfFileDescriptorW);
		if (data.QueryGetData(ref format) != 0)
		{
			return null;
		}
		data.GetData(ref format, out var medium);
		try
		{
			byte* ptr = (byte*)Kernel32.GlobalLock(medium.unionmember);
			if (ptr == null)
			{
				return null;
			}
			try
			{
				uint num = *(uint*)ptr;
				List<string> list = new List<string>((int)num);
				for (uint num2 = 0u; num2 < num; num2++)
				{
					char* ptr2 = (char*)(ptr + 4 + num2 * 592) + 36;
					list.Add(new string(ptr2, 0, NameLength(ptr2, 260)));
				}
				return new DroppedFiles(Array.Empty<string>(), list, "FileGroupDescriptorW");
			}
			finally
			{
				Kernel32.GlobalUnlock(medium.unionmember);
			}
		}
		finally
		{
			Ole32.ReleaseStgMedium(ref medium);
		}
	}

	private unsafe static int NameLength(char* s, int max)
	{
		int i;
		for (i = 0; i < max && s[i] != '\0'; i++)
		{
		}
		return i;
	}

	public static void MarkPerformedEffect(IDataObject data, uint effect)
	{
		WriteDword(data, CfPerformedDropEffect, effect);
		WriteDword(data, CfLogicalPerformedDropEffect, effect);
	}

	public unsafe static uint? ReadDword(IDataObject data, string formatName)
	{
		FORMATETC format = Format((short)User32.RegisterClipboardFormatW(formatName));
		if (data.QueryGetData(ref format) != 0)
		{
			return null;
		}
		data.GetData(ref format, out var medium);
		try
		{
			nint num = Kernel32.GlobalLock(medium.unionmember);
			if (num == 0)
			{
				return null;
			}
			try
			{
				return *(uint*)num;
			}
			finally
			{
				Kernel32.GlobalUnlock(medium.unionmember);
			}
		}
		finally
		{
			Ole32.ReleaseStgMedium(ref medium);
		}
	}

	private unsafe static void WriteDword(IDataObject data, short cf, uint value)
	{
		nint num = Kernel32.GlobalAlloc(2u, 4u);
		if (num == 0)
		{
			return;
		}
		*(uint*)Kernel32.GlobalLock(num) = value;
		Kernel32.GlobalUnlock(num);
		FORMATETC formatIn = Format(cf);
		STGMEDIUM medium = new STGMEDIUM
		{
			tymed = TYMED.TYMED_HGLOBAL,
			unionmember = num,
			pUnkForRelease = null
		};
		try
		{
			data.SetData(ref formatIn, ref medium, release: true);
		}
		catch
		{
			Kernel32.GlobalFree(num);
		}
	}
}
