using System;
using System.IO;
using System.Windows;

namespace Falak.App;

// Opt-in Windows-only WPF UI smoke test. Does not persist any real recording or setting.
internal static class AppScrollQaHarness
{
    internal static void Run(string reportPath)
    {
        var app = Application.Current;
        try
        {
            Theme.Apply(App.Host.Settings.Palette());
            var window = new MacroWindow(new MacroLibrary())
            {
                Title = "Falak — App list scroll QA",
                ShowInTaskbar = false
            };
            bool executed = false;
            window.ContentRendered += async (_, _) =>
            {
                if (executed) return;
                executed = true;
                int exitCode = 1;
                string report;
                try
                {
                    report = await window.RunApplicationsScrollQaAsync();
                    exitCode = report.Contains("SUMMARY: PASS", StringComparison.Ordinal) ? 0 : 1;
                }
                catch (Exception error)
                {
                    report = "FAIL: WPF scrolling test threw an exception." +
                        Environment.NewLine + error + Environment.NewLine + "SUMMARY: FAIL" + Environment.NewLine;
                }
                try
                {
                    var path = Path.GetFullPath(reportPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, report);
                }
                catch (Exception error)
                {
                    exitCode = 1;
                    RuntimeDiagnostics.Report("Cannot write app scroll QA report", error);
                }
                window.Close();
                app.Shutdown(exitCode);
            };
            window.Show();
        }
        catch (Exception error)
        {
            try
            {
                var path = Path.GetFullPath(reportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "FAIL: Could not start WPF scroll QA. " + error +
                    Environment.NewLine + "SUMMARY: FAIL" + Environment.NewLine);
            }
            catch { /* missing write permission is reported via nonzero process exit */ }
            app.Shutdown(1);
        }
    }
}
