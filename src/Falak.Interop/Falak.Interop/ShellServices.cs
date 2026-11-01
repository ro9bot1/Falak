using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Falak.Interop.Native;

namespace Falak.Interop;

public static class ShellServices
{
	public static bool Recycle(IEnumerable<string> paths)
	{
		List<string> list = paths.Where((string p) => File.Exists(p) || Directory.Exists(p)).ToList();
		if (list.Count == 0)
		{
			return true;
		}
		SHFILEOPSTRUCTW lpFileOp = new SHFILEOPSTRUCTW
		{
			wFunc = 3u,
			pFrom = string.Join('\0', list) + "\0\0",
			fFlags = 1108
		};
		if (ShellNative.SHFileOperationW(ref lpFileOp) == 0)
		{
			return !lpFileOp.fAnyOperationsAborted;
		}
		return false;
	}

	public static void Reveal(string path)
	{
		Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
		{
			UseShellExecute = true
		});
	}

	public static void Open(string path)
	{
		Process.Start(new ProcessStartInfo(path)
		{
			UseShellExecute = true
		});
	}

	public static Task<IReadOnlyList<string>> ExplorerSelectionAsync(nint explorerHwnd, TimeSpan timeout)
	{
		TaskCompletionSource<IReadOnlyList<string>> result = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread(() =>
		{
			try
			{
				result.TrySetResult(ReadSelection(explorerHwnd));
			}
			catch
			{
				result.TrySetResult(Array.Empty<string>());
			}
		});
		thread.IsBackground = true;
		thread.Name = "Falak.Selection";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return result.Task.WaitAsync(timeout).ContinueWith((Task<IReadOnlyList<string>> t) => (!t.IsCompletedSuccessfully) ? Array.Empty<string>() : t.Result, TaskScheduler.Default);
	}

	private static IReadOnlyList<string> ReadSelection(nint explorerHwnd)
	{
		Type typeFromProgID = Type.GetTypeFromProgID("Shell.Application");
		if ((object)typeFromProgID == null)
		{
			return Array.Empty<string>();
		}
		dynamic val = Activator.CreateInstance(typeFromProgID);
		List<string> list = new List<string>();
		foreach (dynamic item in val.Windows())
		{
			try
			{
				if ((nint)(long)item.HWND != explorerHwnd)
				{
					continue;
				}
				List<string> list2 = new List<string>();
				foreach (dynamic item2 in item.Document.SelectedItems())
				{
					list2.Add((string)item2.Path);
				}
				if (list2.Count > list.Count)
				{
					list = list2;
				}
			}
			catch
			{
			}
		}
		return list;
	}

	public static double ScaleAt(int x, int y)
	{
		if (Shcore.GetDpiForMonitor(User32.MonitorFromPoint(new POINT(x, y), 2u), 0, out var dpiX, out var _) != 0)
		{
			return 1.0;
		}
		return (double)dpiX / 96.0;
	}

	public static nint ForegroundWindow()
	{
		return User32.GetForegroundWindow();
	}
}
