using System;
using System.IO;
using System.Text;

namespace Falak.App;

internal static class Log
{
	private static readonly object Gate = new object();

	public static void Write(string line)
	{
		try
		{
			lock (Gate)
			{
				Directory.CreateDirectory(AppPaths.Logs);
				File.AppendAllText(Path.Combine(AppPaths.Logs, $"falak-{DateTime.Now:yyyyMMdd}.txt"), $"{DateTime.Now:HH:mm:ss.fff}  {line}{Environment.NewLine}", Encoding.UTF8);
			}
		}
		catch (IOException)
		{
		}
	}
}
