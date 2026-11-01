using System;
using System.IO;
using System.Threading.Tasks;

namespace Falak.Engines;

public static class OutputPaths
{
	public static string FallbackFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Falak");

	public static string FolderFor(string source)
	{
		string directoryName = Path.GetDirectoryName(source);
		if (directoryName != null && IsWritable(directoryName))
		{
			return directoryName;
		}
		Directory.CreateDirectory(FallbackFolder);
		return FallbackFolder;
	}

	public static string Unique(string folder, string baseName, string extension)
	{
		string text = Path.Combine(folder, baseName + extension);
		int num = 2;
		while (File.Exists(text) || Directory.Exists(text))
		{
			text = Path.Combine(folder, $"{baseName} ({num}){extension}");
			num++;
		}
		return text;
	}

	public static string NextTo(string source, string extension, string? suffix = null)
	{
		string baseName = Path.GetFileNameWithoutExtension(source) + ((suffix == null) ? "" : (" - " + suffix));
		return Unique(FolderFor(source), baseName, extension);
	}

	public static string NewFolderNextTo(string source, string suffix)
	{
		string text = Unique(FolderFor(source), Path.GetFileNameWithoutExtension(source) + " - " + suffix, "");
		Directory.CreateDirectory(text);
		return text;
	}

	public static void WriteAtomically(string destination, Action<string> write)
	{
		string text = Path.Combine(Path.GetDirectoryName(destination), $".falak-{Guid.NewGuid():N}{Path.GetExtension(destination)}");
		try
		{
			write(text);
			File.Move(text, destination);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	public static async Task WriteAtomicallyAsync(string destination, Func<string, Task> write)
	{
		string temp = Path.Combine(Path.GetDirectoryName(destination), $".falak-{Guid.NewGuid():N}{Path.GetExtension(destination)}");
		try
		{
			await write(temp);
			File.Move(temp, destination);
		}
		finally
		{
			if (File.Exists(temp))
			{
				File.Delete(temp);
			}
		}
	}

	private static bool IsWritable(string dir)
	{
		try
		{
			using (File.Create(Path.Combine(dir, $".falak-probe-{Guid.NewGuid():N}"), 1, FileOptions.DeleteOnClose))
			{
			}
			return true;
		}
		catch (Exception ex) when ((ex is UnauthorizedAccessException || ex is IOException) ? true : false)
		{
			return false;
		}
	}
}
