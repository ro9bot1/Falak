using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.App;

// Independent of WPF resources and the external conversion engines.
internal static class RuntimeDiagnostics
{
    private static int _installed;
    private static readonly object Gate = new object();

    public static string LogPath
    {
        get
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Falak", "logs");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "startup-diagnostics.txt");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "Falak-startup-diagnostics.txt");
            }
        }
    }

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0) return;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Report("AppDomain.UnhandledException (terminating=" + args.IsTerminating + ")",
                args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
            Report("TaskScheduler.UnobservedTaskException", args.Exception);
    }

    public static void Report(string stage, Exception? ex)
    {
        Write(stage + Environment.NewLine + ex);
    }

    public static void Write(string stage)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogPath,
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz") +
                    " PID=" + Environment.ProcessId + " " + stage + Environment.NewLine,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Error reporting must never crash the program.
        }
    }
}
