using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;
using Microsoft.Win32;

namespace Falak.App;

internal static class AppActions
{
	public static void Run(JobRunner jobs, WheelAction action, IReadOnlyList<string> paths)
	{
		if (ExtendedActions.TryRun(jobs, action, paths)) return;
		JobItem jobItem = jobs.Track(action, paths);
		try
		{
			switch (action.Id)
			{
			case "action.copy-path":
				Clipboard.SetText(string.Join(Environment.NewLine, paths));
				jobItem.Complete(new JobResult(Array.Empty<string>(), null, (paths.Count == 1) ? "المسار في الحافظة، الصقه أينما تريد" : "المسارات في الحافظة، مسار في كل سطر"), succeededWithoutFiles: true);
				break;
			case "action.copy-files":
			{
				StringCollection stringCollection = new StringCollection();
				stringCollection.AddRange(paths.ToArray());
				Clipboard.SetFileDropList(stringCollection);
				jobItem.Complete(new JobResult(Array.Empty<string>(), null, "في الحافظة: الصقه في أي مجلد بـ Ctrl+V"), succeededWithoutFiles: true);
				break;
			}
			case "action.copy-to":
			case "action.move-to":
				CopyOrMove(jobItem, paths, action.Id == "action.move-to");
				break;
			case "action.terminal":
				OpenTerminal(Directory.Exists(paths[0]) ? paths[0] : Path.GetDirectoryName(paths[0]));
				jobItem.Complete(new JobResult(Array.Empty<string>(), null, "فتحنا Terminal في المجلد"), succeededWithoutFiles: true);
				break;
			case "action.show":
				ShellServices.Reveal(paths[0]);
				jobItem.Complete(new JobResult(Array.Empty<string>(), null, "فتحنا المجلد"), succeededWithoutFiles: true);
				break;
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ExternalException) ? true : false)
		{
			jobItem.Fail((UiText.IsArabic ? "تعذ\u0651ر إكمال الإجراء: " : "Could not complete the action: ") + ex.Message);
			Log.Write($"Action {action.Id} failed: {ex}");
		}
	}

	private static void CopyOrMove(JobItem job, IReadOnlyList<string> paths, bool move)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = UiText.IsArabic ? (move ? "انقل إلى أي مجلد؟" : "انسخ إلى أي مجلد؟") : (move ? "Move to which folder?" : "Copy to which folder?")
		};
		if (openFolderDialog.ShowDialog() != true)
		{
			job.MarkCancelled();
			return;
		}
		List<(string From, string To)> done = new List<(string, string)>();
		foreach (string path in paths)
		{
			string fileName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
			string text = OutputPaths.Unique(openFolderDialog.FolderName, Path.GetFileNameWithoutExtension(fileName), Path.GetExtension(fileName));
			if (Directory.Exists(path))
			{
				if (move)
				{
					Directory.Move(path, text);
				}
				else
				{
					CopyDirectory(path, text);
				}
			}
			else if (move)
			{
				File.Move(path, text);
			}
			else
			{
				File.Copy(path, text);
			}
			done.Add((path, text));
		}
		if (move)
		{
			job.UndoOverride = () =>
			{
				foreach (var (text3, text4) in done)
				{
					if (Directory.Exists(text4))
					{
						Directory.Move(text4, text3);
					}
					else if (File.Exists(text4))
					{
						File.Move(text4, text3);
					}
				}
				return true;
			};
		}
		string text2 = UiText.IsArabic ? (move ? "نقلنا" : "نسخنا") : (move ? "Moved" : "Copied");
		job.Complete(new JobResult(done.Select(((string From, string To) d) => d.To).ToList(), null, text2 + (UiText.IsArabic ? " إلى " : " to ") + Path.GetFileName(openFolderDialog.FolderName)));
	}

	private static void CopyDirectory(string source, string destination)
	{
		Directory.CreateDirectory(destination);
		foreach (string item in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
		{
			Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, item)));
		}
		foreach (string item2 in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			File.Copy(item2, Path.Combine(destination, Path.GetRelativePath(source, item2)));
		}
	}

	private static void OpenTerminal(string folder)
	{
		try
		{
			Process.Start(new ProcessStartInfo("wt.exe", "-d \"" + folder + "\"")
			{
				UseShellExecute = true
			});
		}
		catch (Win32Exception)
		{
			Process.Start(new ProcessStartInfo("powershell.exe")
			{
				WorkingDirectory = folder,
				UseShellExecute = true
			});
		}
	}
}
