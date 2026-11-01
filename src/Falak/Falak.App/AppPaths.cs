using System;
using System.IO;

namespace Falak.App;

internal static class AppPaths
{
	public static string Data { get; }

	public static string Logs => Path.Combine(Data, "logs");

	public static string Icon
	{
		get
		{
			string text = Path.Combine(AppContext.BaseDirectory, "Assets", "falak.ico");
			if (File.Exists(text))
			{
				return text;
			}
			string text2 = Path.Combine(Data, "falak-" + (typeof(AppPaths).Assembly.GetName().Version?.ToString() ?? "0") + ".ico");
			if (!File.Exists(text2))
			{
				Directory.CreateDirectory(Data);
                var asset = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/assets/falak.ico"));
                if (asset?.Stream is Stream stream)
                {
                    using (stream)
                    using (var output = File.Create(text2)) stream.CopyTo(output);
                }
                else IconExport.Write(text2);
			}
			return text2;
		}
	}

	static AppPaths()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("FALAK_DATA_DIR");
		Data = ((environmentVariable != null && environmentVariable.Length > 0) ? environmentVariable : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Falak"));
	}
}
