using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Falak.App;

// Isolated opt-in diagnostics. This never saves macro contents or installs a startup task.
// Synthetic SendInput is captured ONLY while this harness enables the diagnostics switch.
internal static class MacroQaHarness
{
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT
    { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT
    { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION
    { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT
    { public uint type; public INPUTUNION U; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int cbSize);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    private static readonly List<string> Results = new();

    public static void Run(string reportPath)
    {
        var window = new Window
        {
            Title = "Falak Macro QA — input test window",
            Width = 500, Height = 220,
            Left = 40, Top = 40,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new TextBlock { Text = "Automatic Windows input tests in progress. Do not use your mouse or keyboard." },
                    new TextBox { Name = "QaTextBox", Text = "Test target: safe synthetic input only" }
                }
            }
        };
        Application.Current.MainWindow = window;
        window.Show();
        window.Dispatcher.BeginInvoke(new Action(async () =>
        {
            int failed = 0;
            try
            {
                await Task.Delay(400);
                await RunCase("all four input categories", true, true, true, true, new[] { 2, 4, 1, 1 });
                await RunCase("keyboard only", true, false, false, false, new[] { 2, 0, 0, 0 });
                await RunCase("clicks only", false, true, false, false, new[] { 0, 4, 0, 0 });
                await RunCase("pointer only", false, false, true, false, new[] { 0, 0, 1, 0 });
                await RunCase("wheel only", false, false, false, true, new[] { 0, 0, 0, 1 });
                await VerifyPlaybackCancellation();
                VerifyApplicationRoutingAndStepTiming();
            }
            catch (Exception ex)
            {
                Results.Add("FATAL: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                MacroCapture.StopSilently();
                MacroCapture.AcceptInjectedForDiagnostics = false;
                failed = Results.Count(r => r.StartsWith("FAIL", StringComparison.Ordinal) || r.StartsWith("FATAL", StringComparison.Ordinal));
                Results.Add($"SUMMARY: {(failed == 0 ? "PASS" : "FAIL")} ({failed} failed cases)");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                File.WriteAllLines(reportPath, Results);
                MessageBox.Show(string.Join("\n", Results) + "\n\nReport: " + reportPath,
                    "Falak Macro QA", MessageBoxButton.OK,
                    failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Error);
                Application.Current.Shutdown(failed == 0 ? 0 : 1);
            }
        }), DispatcherPriority.Background);
    }

    private static async Task RunCase(string name, bool keys, bool clicks, bool moves, bool wheel, int[] min)
    {
        MacroCapture.AcceptInjectedForDiagnostics = true;
        MacroCapture.Start(keys, clicks, moves, wheel);
        try
        {
            await Task.Delay(100);
            if (keys) { Key(0x41, false); Key(0x41, true); }
            if (moves) Move(160, 130);
            if (clicks)
            {
                Mouse(0x0002); Mouse(0x0004); // left down/up
                Mouse(0x0008); Mouse(0x0010); // right down/up
            }
            if (wheel) Mouse(0x0800, 120);
            await Task.Delay(200);
        }
        finally
        {
            MacroCapture.Stop();
        }
        var counts = MacroCapture.CategoryCounts;
        // For unselected categories, fail if anything was inadvertently recorded.
        bool pass = counts.Select((v, i) => v >= min[i] && (min[i] != 0 || v == 0)).All(x => x);
        Results.Add($"{(pass ? "PASS" : "FAIL")}: {name}; key={counts[0]}, click={counts[1]}, move={counts[2]}, wheel={counts[3]}");
    }

    private static async Task VerifyPlaybackCancellation()
    {
        // No real key is sent: the first playback step waits five seconds.
        // Cancelling it must unblock promptly, even in the repeating playback mode.
        var macro = new MacroDefinition
        {
            Name = "QA cancellation only",
            UseRecordedDelay = true,
            PlaybackMode = "loopuntilesc",
            Steps = new List<MacroStep>
            {
                new() { Kind = "key", Code = 0x41, Data = 0, DelayMs = 5000 }
            }
        };
        using var stop = new System.Threading.CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();
        var task = MacroPlayback.PlayAsync(macro, stop.Token);
        await Task.Delay(120);
        stop.Cancel();
        try
        {
            await task;
            Results.Add("FAIL: playback cancellation did not throw");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            bool pass = stopwatch.ElapsedMilliseconds < 1200;
            Results.Add($"{(pass ? "PASS" : "FAIL")}: repeat playback cancellation; elapsed={stopwatch.ElapsedMilliseconds} ms");
        }
    }

    private static void VerifyApplicationRoutingAndStepTiming()
    {
        // Pure logical checks: no user settings, recordings or shortcuts are modified.
        const string browserExe = @"C:\Tools\Browser\browser.exe";
        const string chatExe = @"C:\Tools\Chat\chat.exe";
        var global = new MacroDefinition { Name = "All apps", Shortcut = "K:0:66" };
        var browser = new MacroDefinition
        {
            Name = "Browser only", Shortcut = "K:0:66", RestrictToApplication = true,
            TargetExecutable = browserExe
        };
        var chat = new MacroDefinition
        {
            Name = "Chat only", Shortcut = "K:0:66", RestrictToApplication = true,
            TargetExecutable = chatExe
        };
        var examples = new[] { global, browser, chat };
        bool routing = ReferenceEquals(MacroApplicationScope.Resolve(examples, "K:0:66", browserExe), browser) &&
            ReferenceEquals(MacroApplicationScope.Resolve(examples, "K:0:66", chatExe), chat) &&
            ReferenceEquals(MacroApplicationScope.Resolve(examples, "K:0:66", @"C:\Other\other.exe"), global) &&
            ReferenceEquals(MacroApplicationScope.Resolve(examples, "K:0:66", null), global) &&
            MacroApplicationScope.Resolve(new[] { browser, chat }, "K:0:66", @"C:\Other\other.exe") == null;
        Results.Add((routing ? "PASS" : "FAIL") + ": scoped shortcut wins, global fallback works");

        bool collision = MacroApplicationScope.Conflicts(examples, new MacroDefinition(), "K:0:66", true, chatExe) &&
            !MacroApplicationScope.Conflicts(examples, browser, "K:0:66", true, browserExe) &&
            !MacroApplicationScope.Conflicts(examples, new MacroDefinition(), "K:0:66", true, @"C:\Other\other.exe") &&
            MacroApplicationScope.Conflicts(examples, new MacroDefinition(), "K:0:66", false, null);
        Results.Add((collision ? "PASS" : "FAIL") + ": per-app uniqueness and cross-app reuse");

        var timing = new MacroDefinition { Name = "Timing sample", Steps = new List<MacroStep>
        {
            new() { Kind = "key", Code = 65, DelayMs = 1 },
            new() { Kind = "mouse", Code = 0x201, DelayMs = 347 },
        }};
        timing.Steps[1].DelayMs = 1234;
        var roundtrip = System.Text.Json.JsonSerializer.Deserialize<MacroDefinition>(
            System.Text.Json.JsonSerializer.Serialize(timing));
        bool persistedDelay = roundtrip != null && roundtrip.Steps.Count == 2 &&
            roundtrip.Steps[0].DelayMs == 1 && roundtrip.Steps[1].DelayMs == 1234;
        Results.Add((persistedDelay ? "PASS" : "FAIL") + ": per-step delay survives serialization");
        var restoredScope = System.Text.Json.JsonSerializer.Deserialize<MacroDefinition>(
            System.Text.Json.JsonSerializer.Serialize(browser));
        bool scopeSaved = restoredScope?.RestrictToApplication == true &&
            string.Equals(restoredScope.TargetExecutable, browserExe, StringComparison.Ordinal) &&
            restoredScope.Shortcut == "K:0:66";
        Results.Add((scopeSaved ? "PASS" : "FAIL") + ": app scope and shortcut survive serialization");
    }

    private static void Send(INPUT input)
    {
        if (SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) != 1)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Synthetic QA input failed");
    }
    private static void Key(ushort virtualKey, bool up) =>
        Send(new INPUT { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = virtualKey, dwFlags = up ? 2u : 0u } } });
    private static void Mouse(uint flags, uint data = 0) =>
        Send(new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } } });
    private static void Move(int x, int y)
    {
        int left = GetSystemMetrics(76), top = GetSystemMetrics(77);
        int width = Math.Max(1, GetSystemMetrics(78) - 1), height = Math.Max(1, GetSystemMetrics(79) - 1);
        int dx = (int)Math.Clamp((long)(x - left) * 65535 / width, 0, 65535);
        int dy = (int)Math.Clamp((long)(y - top) * 65535 / height, 0, 65535);
        Send(new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = 0x8000 | 0x4000 | 0x0001 } } });
    }
}
