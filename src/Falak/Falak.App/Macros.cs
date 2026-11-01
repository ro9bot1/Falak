using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Falak.App;

// Deliberately local and user-operated. No remote control, hidden recording or telemetry.
internal sealed class MacroStep
{
    public string Kind { get; set; } = ""; // key, mouse, wheel, move
    public int Code { get; set; } // VK for keyboard; WM_* for mouse
    public int X { get; set; }
    public int Y { get; set; }
    public int Data { get; set; }
    public bool Extended { get; set; }
    public int DelayMs { get; set; }
}


internal sealed class MacroDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<MacroStep> Steps { get; set; } = new();
    public bool UseRecordedDelay { get; set; } = true;
    public string PlaybackMode { get; set; } = "once"; // once, repeat, loopuntilesc
    public int RepeatCount { get; set; } = 1;
    public string? Shortcut { get; set; }
    public bool RestrictToApplication { get; set; }
    public string? TargetExecutable { get; set; }
    public string? TargetAppName { get; set; }
    public override string ToString() => string.IsNullOrEmpty(Shortcut) ? Name : Name + "  ·  " + MacroHotkeys.Describe(Shortcut);

}


internal sealed class MacroLibrary
{
    private sealed class MacroFile
    {
        public int Version { get; set; } = 1;
        public List<MacroDefinition> Recordings { get; set; } = new();
    }
    private readonly List<MacroDefinition> _all = new();
    private static string PathOnDisk => Path.Combine(AppPaths.Data, "macros.json");
    public IReadOnlyList<MacroDefinition> All => _all;
    public MacroDefinition? Find(string id) => _all.FirstOrDefault(x => x.Id == id);
    public void Load()
    {
        _all.Clear();
        try
        {
            if (!File.Exists(PathOnDisk)) return;
            var file = JsonSerializer.Deserialize<MacroFile>(File.ReadAllText(PathOnDisk));
            if (file?.Version != 1 || file.Recordings == null) return;
            foreach (var m in file.Recordings.Take(200))
            {
                if (!Guid.TryParseExact(m.Id, "N", out _) || string.IsNullOrWhiteSpace(m.Name) ||
                    m.Name.Length > 60 || m.Steps == null || m.Steps.Count > 3000 ||
                    m.Steps.Any(s => s.DelayMs < 0 || s.DelayMs > 30000 ||
                        s.Kind is not ("key" or "mouse" or "wheel" or "move"))) continue;
                m.PlaybackMode = ValidatePlaybackMode(m.PlaybackMode);
                m.RepeatCount = ValidateRepeatCount(m.RepeatCount);
                if (!MacroHotkeys.IsValid(m.Shortcut)) m.Shortcut = null;
                // Keep shortcuts scoped to different executables even when identical.
                if (m.RestrictToApplication && (MacroApplicationScope.Normalize(m.TargetExecutable) is not string executable ||
                    !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                {
                    // Fail closed: never silently convert a broken app-only shortcut into a global one.
                    m.Shortcut = null;
                }
                if (MacroApplicationScope.Conflicts(_all, m, m.Shortcut, m.RestrictToApplication, m.TargetExecutable))
                    m.Shortcut = null;
                _all.Add(m);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Write("Macro library could not load: " + e.Message);
        }
    }
    private void Persist()
    {
        Directory.CreateDirectory(AppPaths.Data);
        var json = JsonSerializer.Serialize(new MacroFile { Recordings = _all }, new JsonSerializerOptions { WriteIndented = true });
        var temp = PathOnDisk + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, PathOnDisk, true);
        App.Host.RefreshMacroWheel();
    }
public void Add(string name, IReadOnlyList<MacroStep> steps, bool useRecordedDelay, string playbackMode, int repeatCount)
{
    if (_all.Count >= 200) throw new InvalidOperationException("Maximum 200 recordings.");
    var m = new MacroDefinition
    {
        Name = ValidateName(name),
        Steps = steps.ToList(),
        UseRecordedDelay = useRecordedDelay,
        PlaybackMode = ValidatePlaybackMode(playbackMode),
        RepeatCount = ValidateRepeatCount(repeatCount)
    };
    if (m.Steps.Count == 0) throw new InvalidOperationException("No input was recorded.");
    _all.Add(m);
    try { Persist(); }
    catch { _all.Remove(m); throw; }
}
public void Update(MacroDefinition m, string name, bool useRecordedDelay, string playbackMode, int repeatCount)
{
    var oldName = m.Name;
    var oldDelay = m.UseRecordedDelay;
    var oldMode = m.PlaybackMode;
    var oldCount = m.RepeatCount;
    m.Name = ValidateName(name);
    m.UseRecordedDelay = useRecordedDelay;
    m.PlaybackMode = ValidatePlaybackMode(playbackMode);
    m.RepeatCount = ValidateRepeatCount(repeatCount);
    try { Persist(); }
    catch
    {
        m.Name = oldName;
        m.UseRecordedDelay = oldDelay;
        m.PlaybackMode = oldMode;
        m.RepeatCount = oldCount;
        throw;
    }
}
public void SetShortcut(MacroDefinition m, string? shortcut)
{
    if (!MacroHotkeys.IsValid(shortcut)) throw new InvalidOperationException("Invalid keyboard or mouse shortcut.");
    if (MacroApplicationScope.Conflicts(_all, m, shortcut, m.RestrictToApplication, m.TargetExecutable))
        throw new InvalidOperationException("This shortcut is already used in this application scope.");
    var old = m.Shortcut;
    m.Shortcut = shortcut;
    try { Persist(); }
    catch { m.Shortcut = old; throw; }
}
public void SetScope(MacroDefinition m, bool enabled, string? exe, string? appName)
{
    var fullPath = enabled ? MacroApplicationScope.Normalize(exe) : null;
    if (enabled && (fullPath == null || !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)))
        throw new InvalidOperationException("Select an installed application executable (.exe) first.");
    if (MacroApplicationScope.Conflicts(_all, m, m.Shortcut, enabled, fullPath))
        throw new InvalidOperationException("Another macro uses this shortcut for the same application.");
    var oldEnabled = m.RestrictToApplication;
    var oldPath = m.TargetExecutable;
    var oldName = m.TargetAppName;
    m.RestrictToApplication = enabled;
    m.TargetExecutable = fullPath;
    m.TargetAppName = enabled ? (appName ?? Path.GetFileNameWithoutExtension(fullPath!)) : null;
    try { Persist(); }
    catch { m.RestrictToApplication = oldEnabled; m.TargetExecutable = oldPath; m.TargetAppName = oldName; throw; }
}
public void SetStepDelay(MacroDefinition m, int index, int milliseconds)
{
    if (index < 0 || index >= m.Steps.Count) throw new ArgumentOutOfRangeException(nameof(index));
    if (milliseconds < 0 || milliseconds > 30000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
    int old = m.Steps[index].DelayMs;
    m.Steps[index].DelayMs = milliseconds;
    try { Persist(); }
    catch { m.Steps[index].DelayMs = old; throw; }
}
public void Delete(MacroDefinition m)
{
    var index = _all.IndexOf(m);
    if (index < 0) return;
    _all.RemoveAt(index);
    try { Persist(); }
    catch { _all.Insert(index, m); throw; }
}
private static string ValidateName(string name)
{
    name = name.Trim();
    if (name.Length is < 1 or > 60) throw new InvalidOperationException("Use a name of 1–60 characters.");
    return name;
}
private static string ValidatePlaybackMode(string mode) =>
    mode is "repeat" or "loopuntilesc" ? mode : "once";
private static int ValidateRepeatCount(int count) => Math.Clamp(count, 1, 999);

}

// Hook lifetime is strictly bounded by an explicitly visible recording session.
internal static class MacroCapture
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT
    { public uint VkCode, ScanCode, Flags, Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT
    { public POINT Pt; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? module);
    private static readonly HookProc KeyCallback = OnKey;
    private static readonly HookProc MouseCallback = OnMouse;
    private static IntPtr _keyboard, _mouse;
    private static readonly Stopwatch Clock = new();
    private static readonly List<MacroStep> Recorded = new();
    private static long _lastTick, _lastMovement;
    private static int _lastX, _lastY;
    private static bool _keys, _clicks, _moves, _wheel;
    // Only the explicit --qa-macro test enables injected-event capture. Ordinary
    // recordings must ignore our own SendInput playback to prevent recursion.
    internal static bool AcceptInjectedForDiagnostics { get; set; }
    private static readonly int[] Categories = new int[4];
    public static int[] CategoryCounts => (int[])Categories.Clone();
    public static MacroStep[] Snapshot() => Recorded.ToArray();
    public static bool IsRecording { get; private set; }
    public static int Count => Recorded.Count;
    public static bool LimitReached => Clock.Elapsed > TimeSpan.FromMinutes(5) || Count >= 3000;
    public static void Start(bool keys, bool clicks, bool moves, bool wheel)
    {
        if (!(keys || clicks || moves || wheel)) throw new InvalidOperationException("Choose at least one input category.");
        _keys = keys; _clicks = clicks; _moves = moves; _wheel = wheel;
        if (IsRecording) throw new InvalidOperationException("Recording is already active.");
        Recorded.Clear(); Array.Clear(Categories, 0, Categories.Length); _lastTick = 0; _lastMovement = 0;
        _keyboard = SetWindowsHookEx(13, KeyCallback, GetModuleHandle(null), 0);
        _mouse = SetWindowsHookEx(14, MouseCallback, GetModuleHandle(null), 0);
        if (_keyboard == IntPtr.Zero || _mouse == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            StopSilently();
            throw new Win32Exception(error, "Unable to start keyboard/mouse recording");
        }
        Clock.Restart(); IsRecording = true;
    }
    public static List<MacroStep> Stop()
    {
        StopSilently();
        return Recorded.ToList();
    }
    public static void StopSilently()
    {
        IsRecording = false;
        if (_keyboard != IntPtr.Zero) { UnhookWindowsHookEx(_keyboard); _keyboard = IntPtr.Zero; }
        if (_mouse != IntPtr.Zero) { UnhookWindowsHookEx(_mouse); _mouse = IntPtr.Zero; }
        Clock.Stop();
    }
    private static bool IsOwnWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }
    private static void Append(MacroStep s)
    {
        if (!IsRecording || LimitReached) return;
        long now = Clock.ElapsedMilliseconds;
        s.DelayMs = (int)Math.Clamp(now - _lastTick, 0, 30000);
        Recorded.Add(s);
        switch (s.Kind)
        {
            case "key": Categories[0]++; break;
            case "mouse": Categories[1]++; break;
            case "move": Categories[2]++; break;
            case "wheel": Categories[3]++; break;
        }
        _lastTick = now;
    }
    private static IntPtr OnKey(int nCode, IntPtr w, IntPtr l)
    {
        if (nCode >= 0 && IsRecording && !LimitReached)
        {
            int msg = unchecked((int)w.ToInt64());
            if (msg is 0x100 or 0x101 or 0x104 or 0x105)
            {
                var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(l);
                if (_keys && ((k.Flags & 0x10) == 0 || AcceptInjectedForDiagnostics)) // tests may inject; normal playback is excluded
                    Append(new MacroStep { Kind = "key", Code = (int)k.VkCode,
                        Extended = (k.Flags & 1) != 0, Data = msg is 0x101 or 0x105 ? 1 : 0 });
            }
        }
        return CallNextHookEx(_keyboard, nCode, w, l);
    }
    private static IntPtr OnMouse(int nCode, IntPtr w, IntPtr l)
    {
        if (nCode >= 0 && IsRecording && !LimitReached)
        {
            int msg = unchecked((int)w.ToInt64());
            if ((msg == 0x200 && _moves) ||
                ((msg is 0x20A or 0x20E) && _wheel) ||
                ((msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20B or 0x20C) && _clicks))
            {
                var m = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(l);
                if ((m.Flags & 1) == 0 || AcceptInjectedForDiagnostics)
                {
                    if (msg == 0x200)
                    {
                        var now = Clock.ElapsedMilliseconds;
                        if (now - _lastMovement < 40 && Math.Abs(m.Pt.X - _lastX) < 5 && Math.Abs(m.Pt.Y - _lastY) < 5)
                            return CallNextHookEx(_mouse, nCode, w, l);
                        _lastMovement = now; _lastX = m.Pt.X; _lastY = m.Pt.Y;
                    }
                    Append(new MacroStep { Kind = msg == 0x200 ? "move" : msg is 0x20A or 0x20E ? "wheel" : "mouse",
                        Code = msg, X = m.Pt.X, Y = m.Pt.Y, Data = unchecked((int)m.MouseData) });
                }
            }
        }
        return CallNextHookEx(_mouse, nCode, w, l);
    }
}

// Listens for assigned shortcuts only. No keystrokes are saved or sent anywhere.
// Hooks are removed when Falak exits. Mouse side keys X1 / X2 are supported.
internal static class MacroHotkeys
{
    private delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KBD { public uint VkCode, ScanCode, Flags, Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MS { public POINT Pt; public uint MouseData, Flags, Time; public IntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int code);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    private static readonly HookProc KeyboardCallback = OnKeyboard;
    private static readonly HookProc MouseCallback = OnMouse;
    private static readonly HashSet<int> Down = new();
    private static readonly HashSet<int> SuppressedKeys = new();
    private static string? _suppressedMouse;
    private static IntPtr _keyboard, _mouse;
    private static Action<string?>? _assign;
    public static bool IsAssigning => _assign != null;
    public static bool IsReady => _keyboard != IntPtr.Zero && _mouse != IntPtr.Zero;

    public static bool IsValid(string? shortcut)
    {
        if (shortcut == null) return true;
        if (shortcut == "X:1" || shortcut == "X:2") return true;
        var parts = shortcut.Split(':');
        return parts.Length == 3 && parts[0] == "K" && int.TryParse(parts[1], out int modifiers) &&
               modifiers is >= 0 and <= 15 && int.TryParse(parts[2], out int key) &&
               key is >= 8 and <= 254 && !IsModifier(key) && key != 27;
    }

    public static string Describe(string? shortcut)
    {
        if (shortcut is null) return "—";
        if (shortcut is "X:1" or "X:2") return shortcut == "X:1" ? "Mouse X1" : "Mouse X2";
        var bits = shortcut.Split(':');
        if (bits.Length != 3 || !int.TryParse(bits[1], out int mod) || !int.TryParse(bits[2], out int key)) return "—";
        var parts = new List<string>();
        if ((mod & 1) != 0) parts.Add("Ctrl");
        if ((mod & 2) != 0) parts.Add("Shift");
        if ((mod & 4) != 0) parts.Add("Alt");
        if ((mod & 8) != 0) parts.Add("Win");
        parts.Add(System.Windows.Input.KeyInterop.KeyFromVirtualKey(key).ToString());
        return string.Join(" + ", parts);
    }

    public static void Start()
    {
        if (_keyboard != IntPtr.Zero || _mouse != IntPtr.Zero) return;
        _keyboard = SetWindowsHookEx(13, KeyboardCallback, GetModuleHandle(null), 0);
        _mouse = SetWindowsHookEx(14, MouseCallback, GetModuleHandle(null), 0);
        if (_keyboard == IntPtr.Zero || _mouse == IntPtr.Zero)
        {
            Stop();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Macro shortcut hooks unavailable");
        }
    }
    public static void Stop()
    {
        _assign = null;
        Down.Clear();
        SuppressedKeys.Clear();
        _suppressedMouse = null;
        if (_keyboard != IntPtr.Zero) { UnhookWindowsHookEx(_keyboard); _keyboard = IntPtr.Zero; }
        if (_mouse != IntPtr.Zero) { UnhookWindowsHookEx(_mouse); _mouse = IntPtr.Zero; }
    }
    public static void BeginAssign(Action<string?> done) { _assign = done; Down.Clear(); }
    public static void CancelAssign() { _assign = null; }
    private static bool IsModifier(int key) => key is 16 or 17 or 18 or 91 or 92 or 160 or 161 or 162 or 163 or 164 or 165;
    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static int Modifiers() => (Held(17) ? 1 : 0) | (Held(16) ? 2 : 0) | (Held(18) ? 4 : 0) | ((Held(91) || Held(92)) ? 8 : 0);

    private static IntPtr OnKeyboard(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0)
        {
            int message = unchecked((int)w.ToInt64());
            if (message is 0x100 or 0x104 or 0x101 or 0x105)
            {
                var info = Marshal.PtrToStructure<KBD>(l);
                if ((info.Flags & 0x10) == 0)
                {
                    int key = (int)info.VkCode;
                    // Emergency stop works even when another application has focus.
                    // Swallow the matching Escape up event after cancelling playback.
                    if (key == 27 && (App.Host.IsMacroPlaying || SuppressedKeys.Contains(27)))
                    {
                        if (message is 0x100 or 0x104)
                        {
                            Down.Add(key);
                            SuppressedKeys.Add(key);
                            App.Host.CancelMacroPlayback();
                            return new IntPtr(1);
                        }
                        Down.Remove(key);
                        SuppressedKeys.Remove(key);
                        return new IntPtr(1);
                    }
                    if (message is 0x101 or 0x105)
                    {
                        Down.Remove(key);
                        if (SuppressedKeys.Remove(key)) return new IntPtr(1);
                    }
                    else
                    {
                        bool firstDown = Down.Add(key);
                        if (SuppressedKeys.Contains(key)) return new IntPtr(1);
                        if (firstDown && !MacroCapture.IsRecording)
                        {
                            if (_assign != null)
                            {
                                if (key == 27) CompleteAssign(null);
                                else if (key == 8) CompleteAssign(""); // Backspace clears shortcut
                                else if (!IsModifier(key)) CompleteAssign($"K:{Modifiers()}:{key}");
                            }
                            else if (!IsModifier(key) && key != 27 && Trigger($"K:{Modifiers()}:{key}"))
                            {
                                SuppressedKeys.Add(key);
                                return new IntPtr(1); // do not type the trigger character into the target app
                            }
                        }
                    }
                }
            }
        }
        return CallNextHookEx(_keyboard, code, w, l);
    }
    private static IntPtr OnMouse(int code, IntPtr w, IntPtr l)
    {
        int message = unchecked((int)w.ToInt64());
        if (code >= 0 && (message is 0x20B or 0x20C))
        {
            var info = Marshal.PtrToStructure<MS>(l);
            if ((info.Flags & 1) == 0)
            {
                string shortcut = ((info.MouseData >> 16) & 0xffff) == 2 ? "X:2" : "X:1";
                if (message == 0x20C && shortcut == _suppressedMouse)
                {
                    _suppressedMouse = null;
                    return new IntPtr(1);
                }
                if (message == 0x20B && !MacroCapture.IsRecording)
                {
                    if (_assign != null) CompleteAssign(shortcut);
                    else if (Trigger(shortcut))
                    {
                        _suppressedMouse = shortcut;
                        return new IntPtr(1); // suppress X1/X2 browser-back/forward action
                    }
                }
            }
        }
        return CallNextHookEx(_mouse, code, w, l);
    }
    private static void CompleteAssign(string? shortcut)
    {
        var callback = _assign;
        _assign = null;
        // Dispatch after the low-level hook callback completes.
        if (callback != null) App.Host.Dispatcher.BeginInvoke(new Action(() => callback(shortcut)));
    }
    private static bool Trigger(string shortcut)
    {
        if (App.Host.IsExiting || _assign != null) return false;
        // Never fire shortcuts while the user edits text in Falak's own windows.
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero)
        {
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid == (uint)Environment.ProcessId) return false;
        }
        if (!App.Host.Macros.All.Any(m => m.Shortcut == shortcut)) return false;
        var activeExe = MacroApplicationScope.ForegroundExecutable();
        var macro = MacroApplicationScope.Resolve(App.Host.Macros.All, shortcut, activeExe);
        if (macro == null) return false;
        App.Host.Dispatcher.BeginInvoke(new Action(() => _ = App.Host.PlayMacroAsync(macro)));
        return true;
    }
}

internal static class MacroPlayback
{
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT
    { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT
    { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION
    { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT
    { public uint type; public INPUTUNION U; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] input, int size);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

    private static void Send(INPUT input)
    {
        if (SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows blocked the recorded input. Check target privilege level.");
    }
    private static void Mouse(MacroStep s, uint flags, uint data = 0)
    {
        Send(new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags, mouseData = data } } });
    }
    private static void MoveTo(int x, int y)
    {
        var left = GetSystemMetrics(76); var top = GetSystemMetrics(77);
        var width = Math.Max(1, GetSystemMetrics(78) - 1);
        var height = Math.Max(1, GetSystemMetrics(79) - 1);
        var dx = (int)Math.Clamp((long)(x - left) * 65535 / width, 0, 65535);
        var dy = (int)Math.Clamp((long)(y - top) * 65535 / height, 0, 65535);
        Send(new INPUT { type = 0, U = new INPUTUNION { mi = new MOUSEINPUT {
            dx = dx, dy = dy, dwFlags = 0x8000 | 0x4000 | 0x0001 } } });
    }
    
    private static async Task DelayOrEscapeAsync(int milliseconds, CancellationToken cancellation)
    {
        // Hook cancellation is immediate; this 40ms check is a fallback if hooks are unavailable.
        int remaining = Math.Clamp(milliseconds, 0, 30000);
        do
        {
            cancellation.ThrowIfCancellationRequested();
            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
                throw new OperationCanceledException(cancellation);
            if (remaining == 0) break;
            int tick = Math.Min(40, remaining);
            await Task.Delay(tick, cancellation);
            remaining -= tick;
        } while (remaining > 0);
    }

    public static async Task PlayAsync(MacroDefinition macro, CancellationToken cancel)
    {
        if (MacroCapture.IsRecording) return;
        // Release held keys/mouse buttons even after error or emergency Escape cancellation.
        var pressedKeys = new HashSet<int>();
        var mouseHeld = new HashSet<uint>();
        async Task PlayOnePassAsync()
        {
            int index = 0;
            foreach (var s in macro.Steps.Take(3000))
            {
                await DelayOrEscapeAsync(macro.UseRecordedDelay ? s.DelayMs : 0, cancel);
                if ((++index % 16) == 0) await Task.Yield(); // allow hooks to process Esc when timing is disabled
                switch (s.Kind)
                {
                    case "key":
                        Send(new INPUT { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT {
                            wVk = (ushort)s.Code, dwFlags = (uint)((s.Data == 1 ? 2 : 0) | (s.Extended ? 1 : 0)) } } });
                        if (s.Data == 1) pressedKeys.Remove(s.Code);
                        else pressedKeys.Add(s.Code);
                        break;
                    case "move": MoveTo(s.X, s.Y); break;
                    case "wheel":
                        MoveTo(s.X, s.Y);
                        Mouse(s, s.Code == 0x20E ? 0x1000u : 0x0800u, unchecked((uint)(short)(s.Data >> 16)));
                        break;
                    case "mouse":
                        MoveTo(s.X, s.Y);
                        uint f = s.Code switch {
                            0x201 => 0x0002, 0x202 => 0x0004,
                            0x204 => 0x0008, 0x205 => 0x0010,
                            0x207 => 0x0020, 0x208 => 0x0040,
                            0x20B => 0x0080, 0x20C => 0x0100, _ => 0 };
                        if (f != 0)
                        {
                            Mouse(s, f, s.Code is 0x20B or 0x20C ? (uint)s.Data >> 16 : 0);
                            if (f is 0x0002 or 0x0008 or 0x0020 or 0x0080) mouseHeld.Add(f);
                            else mouseHeld.Remove(f == 0x0100 ? 0x0080u : f >> 1);
                        }
                        break;
                }
            }
        }

        try
        {
            switch (macro.PlaybackMode)
            {
                case "repeat":
                    for (int i = 0; i < Math.Max(1, macro.RepeatCount); i++)
                        await PlayOnePassAsync();
                    break;
                case "loopuntilesc":
                    while (true)
                    {
                        await PlayOnePassAsync();
                        // Yield between passes even for macros with zero recorded delay.
                        await Task.Delay(1, cancel);
                        await DelayOrEscapeAsync(0, cancel);
                    }
                default:
                    await PlayOnePassAsync();
                    break;
            }
        }
        finally
        {
            foreach (int key in pressedKeys)
            {
                try { Send(new INPUT { type = 1, U = new INPUTUNION { ki = new KEYBDINPUT { wVk = (ushort)key, dwFlags = 2 } } }); }
                catch { /* best effort cleanup */ }
            }
            foreach (uint flag in mouseHeld)
            {
                try { Mouse(new MacroStep(), flag << 1, flag == 0x0080 ? 1u : 0u); }
                catch { /* best effort cleanup */ }
            }
        }
    }

}



internal sealed class MacroWindow : Window
{
    private readonly MacroLibrary _library;
    private readonly bool _ar;
    private readonly ListBox _list = new();
    private readonly ListBox _steps = new();
    private readonly ListBox _applications = new();
    private readonly TextBox _appSearch = new();
    private readonly List<MacroApplicationScope.Entry> _allApplications = new();
    private string? _chosenAppPath;
    private bool _filteringApplications;
    private readonly CheckBox _appOnly = new();
    private readonly TextBlock _scopeStatus = new();
    private Button _scopeSave = null!, _appRefresh = null!, _appBrowse = null!;
    private readonly TextBox _stepDelay = new();
    private Button _delayApply = null!, _delayMinus = null!, _delayPlus = null!;
    private bool _refreshingSteps;
    private bool _loadingScope;
    private readonly TextBox _name = new();
    private readonly TextBlock _state = new();
    private readonly TextBlock _summary = new();
    // These controls are created inside panel-builder methods (after constructor entry),
    // so the fields must not be readonly; CS0191 blocked the Windows build.
    private Button _record = null!, _save = null!, _apply = null!,
        _delete = null!, _play = null!, _shortcutAssign = null!, _shortcutRemove = null!;
    private readonly CheckBox _recordKeys = new();
    private readonly CheckBox _recordClicks = new();
    private readonly CheckBox _recordMoves = new();
    private readonly CheckBox _recordWheel = new();
    private readonly TextBlock _shortcutStatus = new();
    private readonly CheckBox _useDelay = new();
    private readonly RadioButton _runOnce = new();
    private readonly RadioButton _runRepeat = new();
    private readonly RadioButton _runLoop = new();
    private readonly TextBox _repeatCount = new();
    private readonly DispatcherTimer _timer;
    private List<MacroStep> _draft = new();
    private int _countdown;
    private int _lastVisibleCount = -1;
    private DateTime _startAt;
    private bool _suppressSelect;

    public MacroWindow(MacroLibrary library)
    {
        _library = library;
        _ar = App.Host.Settings.Language != "en";
        Title = _ar ? "فَلَك | تسجيل الماكرو" : "Falak | Macro recorder";
        Width = 1335; Height = 675; MinWidth = 1150; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FlowDirection = _ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        SetResourceReference(BackgroundProperty, "Bg");
        SetResourceReference(ForegroundProperty, "Ink");

        var root = new Grid { Margin = new Thickness(24) };
        // With RightToLeft layout, column 0 is the physical right edge. Place the
        // applications panel there, immediately to the right of Macro list (col 1).
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = BuildSavedMacrosPanel();
        Grid.SetColumn(left, 1);
        root.Children.Add(left);

        var applications = BuildApplicationsPanel();
        Grid.SetColumn(applications, 0);
        root.Children.Add(applications);

        var middle = BuildStepsPanel();
        Grid.SetColumn(middle, 2);
        root.Children.Add(middle);

        var right = BuildControlPanel();
        Grid.SetColumn(right, 3);
        root.Children.Add(right);

        Content = root;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _timer.Tick += (_, _) => Tick();
        Closing += OnClosing;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && App.Host.IsMacroPlaying)
            {
                App.Host.CancelMacroPlayback();
                e.Handled = true;
            }
        };
        RefreshApplications();
        Refresh();
        NewMacro(clearDraft: true);
        UpdateButtons();
    }

    private UIElement BuildSavedMacrosPanel()
    {
        var card = Card();
        var stack = new DockPanel();
        card.Child = stack;

        var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        var shortcutRow = new StackPanel { Orientation = Orientation.Vertical };
        _shortcutAssign = ActionButton(_ar ? "⌨ إضافة اختصار" : "⌨ Set shortcut", AssignShortcut, "SecondaryButton");
        _shortcutRemove = ActionButton(_ar ? "إلغاء الاختصار" : "Clear shortcut", ClearShortcut, "GhostButton");
        _shortcutAssign.MinWidth = 0;
        _shortcutRemove.MinWidth = 0;
        _shortcutAssign.Width = _shortcutRemove.Width = 188;
        _shortcutRemove.Margin = new Thickness(0,5,0,0);
        shortcutRow.Children.Add(_shortcutAssign);
        shortcutRow.Children.Add(_shortcutRemove);
        footer.Children.Add(shortcutRow);
        _shortcutStatus.Style = Application.Current.TryFindResource("Caption") as Style;
        _shortcutStatus.Margin = new Thickness(0,7,0,7);
        _shortcutStatus.TextWrapping = TextWrapping.Wrap;
        footer.Children.Add(_shortcutStatus);
        _delete = ActionButton(_ar ? "⌫ حذف الماكرو المحدد" : "⌫ Delete selected macro", DeleteSelected, "GhostButton");
        _delete.ToolTip = _ar ? "يحذف الماكرو بالكامل بعد التأكيد" : "Delete the selected macro after confirmation";
        footer.Children.Add(_delete);
        DockPanel.SetDock(footer, Dock.Bottom);
        stack.Children.Add(footer);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(Heading(_ar ? "قائمة الماكرو" : "Macro list", 18));
        header.Children.Add(BodyMuted(_ar ? "التسجيلات المحفوظة التي ستظهر داخل Space." : "Saved recordings that appear inside Space."));
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);

        ConfigureListBox(_list, 190);
        _list.SelectionChanged += (_, _) => SelectMacro();
        var menu = new ContextMenu();
        // Prevent see-through context menus while the app's main UI uses Glass.
        menu.SetResourceReference(Control.BackgroundProperty, "PopupSurface");
        menu.SetResourceReference(Control.ForegroundProperty, "Ink");
        menu.SetResourceReference(Control.BorderBrushProperty, "PopupBorder");
        var deleteMenu = new MenuItem { Header = _ar ? "⌫ حذف الماكرو المحدد" : "⌫ Delete selected macro" };
        deleteMenu.Click += (_, _) => DeleteSelected();
        menu.Items.Add(deleteMenu);
        var clearMenu = new MenuItem { Header = _ar ? "إلغاء اختصار الماكرو" : "Clear macro shortcut" };
        clearMenu.Click += (_, _) => ClearShortcut();
        menu.Items.Add(clearMenu);
        _list.ContextMenu = menu;
        stack.Children.Add(_list);
        return card;
    }

    private UIElement BuildApplicationsPanel()
    {
        var card = Card();
        // Separate Auto / Star / Auto rows give the ListBox a finite viewport;
        // unlike a content-sized panel, this enables its internal ScrollViewer.
        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 0 });
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        card.Child = stack;
        var controls = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        _scopeSave = ActionButton(_ar ? "حفظ تخصيص التطبيق" : "Save app restriction", SaveApplicationScope, "SecondaryButton");
        _scopeSave.MinWidth = 0;
        controls.Children.Add(_scopeSave);
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        _appRefresh = ActionButton(_ar ? "تحديث" : "Refresh", RefreshApplications, "GhostButton");
        _appBrowse = ActionButton(_ar ? "اختيار EXE" : "Browse EXE", BrowseApplication, "GhostButton");
        _appRefresh.MinWidth = _appBrowse.MinWidth = 0;
        _appRefresh.Padding = _appBrowse.Padding = new Thickness(7, 3, 7, 3);
        choices.Children.Add(_appRefresh);
        choices.Children.Add(_appBrowse);
        controls.Children.Add(choices);
        _scopeStatus.Style = Application.Current.TryFindResource("Caption") as Style;
        _scopeStatus.TextWrapping = TextWrapping.Wrap;
        _scopeStatus.Margin = new Thickness(0, 6, 0, 0);
        controls.Children.Add(_scopeStatus);
        Grid.SetRow(controls, 2);
        stack.Children.Add(controls);

        var top = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(Heading(_ar ? "قائمة التطبيقات" : "Applications", 18));
        top.Children.Add(BodyMuted(_ar ? "اختر ماكرو أولًا، ثم التطبيق الذي يعمل فيه الاختصار." : "Select a macro, then the application for its shortcut."));
        _appOnly.Content = _ar ? "لهذا التطبيق فقط" : "Only in this application";
        _appOnly.Style = Application.Current.TryFindResource("Switch") as Style;
        _appOnly.Margin = new Thickness(0, 8, 0, 0);
        _appOnly.Checked += (_, _) => { if (!_loadingScope) UpdateButtons(); };
        _appOnly.Unchecked += (_, _) => { if (!_loadingScope) UpdateButtons(); };
        top.Children.Add(_appOnly);
        var searchLabel = Heading(_ar ? "بحث عن تطبيق" : "Search applications", 12);
        searchLabel.Margin = new Thickness(0, 10, 0, 3);
        top.Children.Add(searchLabel);
        ConfigureTextBox(_appSearch, double.NaN);
        _appSearch.Height = 36;
        _appSearch.ToolTip = _ar ? "اكتب اسم التطبيق لتصفية القائمة" : "Filter apps by name";
        _appSearch.TextChanged += (_, _) => FilterApplications();
        top.Children.Add(_appSearch);
        Grid.SetRow(top, 0);
        stack.Children.Add(top);

        ConfigureListBox(_applications, 0);
        _applications.VerticalAlignment = VerticalAlignment.Stretch;
        // Explicit scrollbars: users can drag the thumb as well as use the wheel.
        ScrollViewer.SetVerticalScrollBarVisibility(_applications, ScrollBarVisibility.Visible);
        ScrollViewer.SetHorizontalScrollBarVisibility(_applications, ScrollBarVisibility.Disabled);
        _applications.ItemTemplate = ApplicationItemTemplate();
        _applications.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VirtualizingStackPanel.SetIsVirtualizing(_applications, true);
        VirtualizingStackPanel.SetVirtualizationMode(_applications, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(_applications, true);
        // Some icon/list templates swallow the routed MouseWheel event. Route
        // wheel movement to the ListBox's actual ScrollViewer without changing selection.
        _applications.PreviewMouseWheel += ApplicationsPreviewMouseWheel;
        _applications.SelectionChanged += (_, _) =>
        {
            if (!_filteringApplications)
                _chosenAppPath = (_applications.SelectedItem as MacroApplicationScope.Entry)?.Path;
            UpdateButtons();
        };
        Grid.SetRow(_applications, 1);
        stack.Children.Add(_applications);
        return card;
    }

    private void ApplicationsPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = FindScrollViewer(_applications);
        if (viewer == null || viewer.ScrollableHeight <= 0 || e.Delta == 0) return;

        // Logical scrolling is enabled (recycling stays on), so the offset is
        // measured in app rows. Scroll three rows per standard wheel notch.
        var newOffset = Math.Clamp(viewer.VerticalOffset - (e.Delta / 120.0) * 3.0,
            0.0, viewer.ScrollableHeight);
        if (Math.Abs(viewer.VerticalOffset - newOffset) < 0.0001) return;
        viewer.ScrollToVerticalOffset(newOffset);
        e.Handled = true;
    }

    // Invoked ONLY by --qa-app-scroll in a separate Falak process.
    // Uses synthetic entries and an in-memory library; never modifies the user's macros.
    internal async Task<string> RunApplicationsScrollQaAsync()
    {
        var result = new List<string>();
        int failures = 0;
        void Check(string title, bool passed, string detail = "")
        {
            result.Add((passed ? "PASS: " : "FAIL: ") + title +
                (detail.Length > 0 ? " — " + detail : ""));
            if (!passed) failures++;
        }

        _allApplications.Clear();
        for (int i = 0; i < 90; i++)
            _allApplications.Add(new MacroApplicationScope.Entry(
                $"Falak QA App {i:000}", System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Falak_QA_Application_{i:000}.exe")));
        _chosenAppPath = null;
        _appSearch.Clear();
        _appOnly.IsChecked = false;
        FilterApplications();
        UpdateButtons();
        // Give the WPF template and VirtualizingStackPanel time to measure the viewport.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        UpdateLayout();
        _applications.UpdateLayout();
        var viewer = FindScrollViewer(_applications);
        Check("List has 90 synthetic applications", _applications.Items.Count == 90,
            "Items=" + _applications.Items.Count);
        Check("List is enabled with no macro or scope selected", _applications.IsEnabled &&
            _list.SelectedItem == null && _appOnly.IsChecked != true);
        Check("Embedded ScrollViewer found", viewer != null);
        if (viewer != null)
        {
            viewer.UpdateLayout();
            Check("Content is vertically scrollable", viewer.ScrollableHeight > 0.5,
                $"Extent={viewer.ExtentHeight:0.##}, Viewport={viewer.ViewportHeight:0.##}, Range={viewer.ScrollableHeight:0.##}");
            viewer.ScrollToTop();
            viewer.UpdateLayout();
            var fromTop = viewer.VerticalOffset;
            // Same routed event path that a physical wheel uses on the applications ListBox.
            var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
            {
                RoutedEvent = UIElement.PreviewMouseWheelEvent
            };
            _applications.RaiseEvent(wheel);
            viewer.UpdateLayout();
            Check("Mouse wheel event changes list offset", viewer.VerticalOffset > fromTop,
                $"From={fromTop:0.##}, To={viewer.VerticalOffset:0.##}");

            viewer.ScrollToTop();
            viewer.UpdateLayout();
            viewer.LineDown();
            viewer.UpdateLayout();
            Check("Scrollbar down/LineDown changes offset", viewer.VerticalOffset > 0,
                "Offset=" + viewer.VerticalOffset.ToString("0.##"));
            viewer.ScrollToBottom();
            viewer.UpdateLayout();
            Check("Can reach bottom of list", viewer.ScrollableHeight > 0 &&
                viewer.VerticalOffset >= viewer.ScrollableHeight - 1.01,
                $"Offset={viewer.VerticalOffset:0.##}, Range={viewer.ScrollableHeight:0.##}");
            viewer.ScrollToTop();
        }

        // Search must remain usable AND preserve a chosen app after clearing the search.
        _applications.SelectedItem = _allApplications[73];
        _appSearch.Text = "Falak QA App 073";
        Check("Search filters entries", _applications.Items.Count == 1,
            "Filtered items=" + _applications.Items.Count);
        _appSearch.Clear();
        Check("Clear search restores all entries", _applications.Items.Count == 90);
        Check("Chosen app retained after search", _applications.SelectedItem is MacroApplicationScope.Entry selected &&
            selected.Path == _allApplications[73].Path);

        result.Add("SUMMARY: " + (failures == 0 ? "PASS" : "FAIL") +
            " (" + failures + " failed)");
        return string.Join(Environment.NewLine, result) + Environment.NewLine;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var match = FindScrollViewer(child);
            if (match != null) return match;
        }
        return null;
    }

    // WPF's item template deliberately shows only the icon and friendly name;
    // executable paths remain available internally to keep app scoping precise.
    private static DataTemplate ApplicationItemTemplate()
    {
        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 3, 3, 3));

        var icon = new FrameworkElementFactory(typeof(Image));
        icon.SetValue(FrameworkElement.WidthProperty, 23.0);
        icon.SetValue(FrameworkElement.HeightProperty, 23.0);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        icon.SetValue(Image.StretchProperty, Stretch.Uniform);
        icon.SetBinding(Image.SourceProperty, new Binding(nameof(MacroApplicationScope.Entry.Icon)));
        row.AppendChild(icon);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetValue(TextBlock.FontSizeProperty, 13.0);
        label.SetValue(FrameworkElement.MaxWidthProperty, 165.0);
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(MacroApplicationScope.Entry.Name)));
        row.AppendChild(label);
        return new DataTemplate { VisualTree = row };
    }

    private void FilterApplications()
    {
        // Do not lose a chosen executable when search filters it out temporarily.
        var query = _appSearch.Text.Trim();
        var visible = string.IsNullOrEmpty(query) ? _allApplications.ToArray() :
            _allApplications.Where(app =>
                app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                Path.GetFileNameWithoutExtension(app.Path).Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
        _filteringApplications = true;
        try
        {
            _applications.ItemsSource = visible;
            _applications.SelectedItem = visible.FirstOrDefault(app =>
                string.Equals(app.Path, _chosenAppPath, StringComparison.OrdinalIgnoreCase));
        }
        finally { _filteringApplications = false; }
        UpdateButtons();
    }

    private void RefreshApplications()
    {
        var previousPath = _chosenAppPath ?? (_list.SelectedItem as MacroDefinition)?.TargetExecutable;
        var items = MacroApplicationScope.Enumerate();
        if (previousPath != null && !items.Any(app =>
                string.Equals(app.Path, previousPath, StringComparison.OrdinalIgnoreCase)))
            items.Add(new MacroApplicationScope.Entry(Path.GetFileNameWithoutExtension(previousPath), previousPath));
        _allApplications.Clear();
        _allApplications.AddRange(items.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase));
        _chosenAppPath = previousPath;
        FilterApplications();
    }

    private void BrowseApplication()
    {
        var dialog = new OpenFileDialog
        {
            Title = _ar ? "حدد الملف التنفيذي للتطبيق" : "Choose application executable",
            Filter = "Windows applications (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        var path = MacroApplicationScope.Normalize(dialog.FileName);
        if (path == null) return;
        if (!_allApplications.Any(app => string.Equals(app.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            _allApplications.Add(new MacroApplicationScope.Entry(Path.GetFileNameWithoutExtension(path), path));
            _allApplications.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
        }
        _chosenAppPath = path;
        _appSearch.Clear(); // reveal and select the manually chosen EXE
        FilterApplications();
        _appOnly.IsChecked = true;
        UpdateButtons();
    }

    private void LoadApplicationScope()
    {
        _loadingScope = true;
        try
        {
            if (_list.SelectedItem is MacroDefinition m)
            {
                _appOnly.IsChecked = m.RestrictToApplication;
                if (m.TargetExecutable != null && !_allApplications.Any(app =>
                    string.Equals(app.Path, m.TargetExecutable, StringComparison.OrdinalIgnoreCase)))
                {
                    _allApplications.Add(new MacroApplicationScope.Entry(
                        m.TargetAppName ?? Path.GetFileNameWithoutExtension(m.TargetExecutable), m.TargetExecutable));
                    _allApplications.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
                }
                _chosenAppPath = m.TargetExecutable;
                _scopeStatus.Text = m.RestrictToApplication
                    ? (_ar ? "المخصص: " : "Only: ") + (m.TargetAppName ?? Path.GetFileName(m.TargetExecutable))
                    : (_ar ? "يعمل في جميع التطبيقات" : "Available in all applications");
            }
            else
            {
                _appOnly.IsChecked = false;
                _chosenAppPath = null;
                _scopeStatus.Text = _ar ? "اختر ماكرو محفوظًا أولًا." : "Select a saved macro first.";
            }
            _appSearch.Clear();
            FilterApplications();
        }
        finally { _loadingScope = false; }
        UpdateButtons();
    }

    private void SaveApplicationScope()
    {
        if (_list.SelectedItem is not MacroDefinition macro) return;
        bool restricted = _appOnly.IsChecked == true;
        var app = _applications.SelectedItem as MacroApplicationScope.Entry;
        bool saved = false;
        Safe(() => { _library.SetScope(macro, restricted, app?.Path, app?.Name); saved = true; });
        if (saved) { Refresh(); LoadApplicationScope(); }
    }

    private UIElement BuildStepsPanel()
    {
        var card = Card();
        var stack = new DockPanel();
        card.Child = stack;

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        _play = ActionButton(_ar ? "▶ تشغيل" : "▶ Play", TogglePlayback, "PrimaryButton");
        _apply = ActionButton(_ar ? "حفظ التعديلات" : "Apply", ApplySelectedChanges, "SecondaryButton");
        _play.Width = 96; _apply.Width = 118; _apply.Margin = new Thickness(10, 0, 0, 0);
        footer.Children.Add(_play);
        footer.Children.Add(_apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        stack.Children.Add(footer);

        var latencyPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        latencyPanel.Children.Add(Heading(_ar ? "زمن الخطوة المحددة (ms)" : "Selected step delay (ms)", 13));
        var latencyRow = new StackPanel { Orientation = Orientation.Horizontal };
        _delayMinus = ActionButton("−", () => AdjustLatency(-10), "GhostButton");
        _delayPlus = ActionButton("+", () => AdjustLatency(10), "GhostButton");
        _delayMinus.MinWidth = _delayPlus.MinWidth = 28;
        _delayMinus.Width = _delayPlus.Width = 29;
        _delayMinus.Padding = _delayPlus.Padding = new Thickness(2);
        ConfigureTextBox(_stepDelay, 74);
        _stepDelay.Text = "0";
        _stepDelay.MaxLength = 5;
        _stepDelay.HorizontalContentAlignment = HorizontalAlignment.Center;
        _delayApply = ActionButton(_ar ? "تطبيق ms" : "Apply ms", ApplyStepDelay, "SecondaryButton");
        _delayApply.MinWidth = 0;
        _delayApply.Padding = new Thickness(8, 2, 8, 2);
        latencyRow.Children.Add(_delayMinus);
        latencyRow.Children.Add(_stepDelay);
        latencyRow.Children.Add(_delayPlus);
        latencyRow.Children.Add(_delayApply);
        latencyPanel.Children.Add(latencyRow);
        DockPanel.SetDock(latencyPanel, Dock.Bottom);
        stack.Children.Add(latencyPanel);

        var footerInfo = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        _summary.Style = Application.Current.TryFindResource("Caption") as Style;
        footerInfo.Children.Add(_summary);
        DockPanel.SetDock(footerInfo, Dock.Bottom);
        stack.Children.Add(footerInfo);

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(Heading(_ar ? "قائمة الخطوات" : "Key / step list", 18));
        header.Children.Add(BodyMuted(_ar ? "تفاصيل ضغطات الماكرو المحدد أو التسجيل الحالي." : "Recorded event details for the selected or current macro."));
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);

        ConfigureListBox(_steps, 210);
        _steps.SelectionChanged += (_, _) => SelectedStepChanged();
        stack.Children.Add(_steps);
        return card;
    }

    private UIElement BuildControlPanel()
    {
        var card = Card();
        // Compact, always-visible controls; scrolling belongs to the recording lists only.
        var stack = new StackPanel();
        card.Child = stack;
        stack.Children.Add(Heading(_ar ? "التحكم" : "Control", 18));

        _record = ActionButton(_ar ? "● بدء التسجيل" : "● Start record", ToggleRecording, "PrimaryButton");
        _record.Height = 38;
        _record.Margin = new Thickness(0, 3, 0, 8);
        stack.Children.Add(_record);

        stack.Children.Add(Heading(_ar ? "ماذا تريد تسجيله؟" : "Record these inputs", 14));
        var categories = new Grid { Margin = new Thickness(0, 1, 0, 6) };
        categories.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        categories.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        categories.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        categories.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        CheckBox[] choices = { _recordKeys, _recordClicks, _recordMoves, _recordWheel };
        string[] captions = _ar ?
            new[] { "لوحة المفاتيح", "نقرات الفأرة", "حركة المؤشر", "عجلة التمرير" } :
            new[] { "Keyboard", "Mouse clicks", "Pointer move", "Mouse wheel" };
        for (int i = 0; i < choices.Length; i++)
        {
            ConfigureRecordChoice(choices[i], captions[i]);
            Grid.SetRow(choices[i], i / 2);
            Grid.SetColumn(choices[i], i % 2);
            categories.Children.Add(choices[i]);
        }
        stack.Children.Add(categories);
        _recordKeys.IsChecked = _recordClicks.IsChecked = _recordMoves.IsChecked = _recordWheel.IsChecked = true;

        _useDelay.Content = _ar ? "استخدام التوقيت التلقائي بين الضغطات" : "Automatic recording time delay";
        _useDelay.IsChecked = true;
        _useDelay.Style = Application.Current.TryFindResource("Switch") as Style;
        _useDelay.Margin = new Thickness(0, 2, 0, 8);
        stack.Children.Add(_useDelay);

        stack.Children.Add(Heading(_ar ? "نمط التشغيل" : "Playback mode", 14));

        _runOnce.Content = _ar ? "تشغيل مرة واحدة" : "Play once";
        _runRepeat.Content = _ar ? "تكرار عدد مرات" : "Cycle times";
        _runLoop.Content = _ar ? "تكرار حتى ضغط Esc" : "Cycle until Esc";
        foreach (var radio in new[] { _runOnce, _runRepeat, _runLoop })
        {
            radio.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(radio);
        }
        _runOnce.IsChecked = true;

        var repeatRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 3, 0, 8) };
        repeatRow.Children.Add(new TextBlock {
            Text = _ar ? "العدد" : "Count",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Style = Application.Current.TryFindResource("Caption") as Style
        });
        ConfigureTextBox(_repeatCount, 70);
        _repeatCount.Text = "2";
        repeatRow.Children.Add(_repeatCount);
        stack.Children.Add(repeatRow);

        stack.Children.Add(Heading(_ar ? "اسم الماكرو" : "Macro name", 14));
        ConfigureTextBox(_name, double.NaN);
        _name.MaxLength = 60;
        _name.Text = _ar ? "تسجيل جديد" : "New macro";
        _name.Margin = new Thickness(0, 3, 0, 8);
        stack.Children.Add(_name);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        _save = ActionButton(_ar ? "حفظ الماكرو" : "Save macro", Save, "SecondaryButton");
        _save.Width = 145;
        row.Children.Add(_save);
        stack.Children.Add(row);
        // All buttons exist before RadioButton.Checked is permitted to refresh state.
        foreach (var radio in new[] { _runOnce, _runRepeat, _runLoop })
            radio.Checked += (_, _) => UpdateButtons();

        _state.FontWeight = FontWeights.SemiBold;
        _state.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        _state.TextWrapping = TextWrapping.Wrap;
        _state.Margin = new Thickness(0, 3, 0, 0);
        _state.FontSize = 12;
        stack.Children.Add(_state);

        return card;
    }

    private static void ConfigureRecordChoice(CheckBox choice, string title)
    {
        choice.Content = title;
        choice.Margin = new Thickness(0, 4, 4, 4);
        choice.Style = Application.Current.TryFindResource("Switch") as Style;
    }

    private static Border Card()
    {
        var border = new Border();
        border.Style = Application.Current.TryFindResource("Card") as Style;
        return border;
    }

    private static TextBlock Heading(string content, double size) => new()
    {
        Text = content,
        FontWeight = FontWeights.Bold,
        FontSize = size,
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static TextBlock BodyMuted(string content)
    {
        var t = new TextBlock { Text = content };
        t.Style = Application.Current.TryFindResource("BodyMuted") as Style;
        return t;
    }

    private static Button ActionButton(string caption, Action callback, string styleKey) =>
        new()
        {
            Content = caption,
            MinWidth = 92,
            Height = 36,
            Padding = new Thickness(12, 0, 12, 0),
            Command = new RelayCommand(callback),
            Style = Application.Current.TryFindResource(styleKey) as Style
        };

    private static void ConfigureListBox(ListBox list, double minHeight)
    {
        list.MinHeight = minHeight;
        list.BorderThickness = new Thickness(1);
        list.Padding = new Thickness(8);
        list.SetResourceReference(Control.BackgroundProperty, "SurfaceRaised");
        list.SetResourceReference(Control.ForegroundProperty, "Ink");
        list.SetResourceReference(Control.BorderBrushProperty, "Line");
    }

    private static void ConfigureTextBox(TextBox box, double width)
    {
        if (!double.IsNaN(width)) box.Width = width;
        box.Height = 34;
        box.Padding = new Thickness(10, 4, 10, 4);
        box.SetResourceReference(Control.BackgroundProperty, "SurfaceRaised");
        box.SetResourceReference(Control.ForegroundProperty, "Ink");
        box.SetResourceReference(Control.BorderBrushProperty, "Line");
    }

    private void Refresh()
    {
        var id = (_list.SelectedItem as MacroDefinition)?.Id;
        _suppressSelect = true;
        _list.ItemsSource = null;
        _list.ItemsSource = _library.All.ToArray();
        _list.SelectedItem = id == null ? null : _library.Find(id);
        _suppressSelect = false;
        RefreshStepList();
        LoadApplicationScope();
        RefreshShortcutStatus();
        UpdateButtons();
    }

    private void NewMacro(bool clearDraft)
    {
        if (MacroCapture.IsRecording || _countdown > 0) return;
        if (clearDraft) _draft.Clear();
        _suppressSelect = true;
        _list.SelectedItem = null;
        _suppressSelect = false;
        _name.Text = _ar ? "تسجيل جديد" : "New macro";
        _useDelay.IsChecked = true;
        _runOnce.IsChecked = true;
        _repeatCount.Text = "2";
        _state.Text = _ar ? "جاهز لبدء تسجيل جديد." : "Ready to record a new macro.";
        LoadApplicationScope();
        RefreshShortcutStatus();
        RefreshStepList();
        UpdateButtons();
    }

    private void SelectMacro()
    {
        if (_suppressSelect) return;
        if (_draft.Count > 0 && _list.SelectedItem is MacroDefinition)
        {
            bool discard = MacroDialog.Confirm(this,
                _ar ? "تسجيل غير محفوظ" : "Unsaved recording",
                _ar ? "لديك تسجيل لم تحفظه. هل تريد تجاهله والانتقال إلى التسجيل المحدد؟" : "Discard the current recording and open the selected macro?",
                _ar ? "تجاهل التسجيل" : "Discard");
            if (!discard)
            {
                _suppressSelect = true;
                _list.SelectedItem = null;
                _suppressSelect = false;
                RefreshStepList();
                LoadApplicationScope();
                RefreshShortcutStatus();
                UpdateButtons();
                return;
            }
            _draft.Clear();
        }

        if (_list.SelectedItem is MacroDefinition m && !MacroCapture.IsRecording)
        {
            _name.Text = m.Name;
            _useDelay.IsChecked = m.UseRecordedDelay;
            _runOnce.IsChecked = m.PlaybackMode == "once";
            _runRepeat.IsChecked = m.PlaybackMode == "repeat";
            _runLoop.IsChecked = m.PlaybackMode == "loopuntilesc";
            _repeatCount.Text = Math.Max(1, m.RepeatCount).ToString();
            _state.Text = _ar ? "يمكنك التشغيل أو تعديل الاسم وخيارات التشغيل ثم حفظ التعديلات." : "Play the macro or change its name/settings, then apply.";
        }
        RefreshStepList();
        LoadApplicationScope();
        RefreshShortcutStatus();
        UpdateButtons();
    }

    private void RefreshStepList()
    {
        IReadOnlyList<MacroStep> steps;
        if (_draft.Count > 0 && _list.SelectedItem is null) steps = _draft;
        else if (_list.SelectedItem is MacroDefinition m) steps = m.Steps;
        else steps = Array.Empty<MacroStep>();

        int selectedIndex = _steps.SelectedIndex;
        _refreshingSteps = true;
        try
        {
            _steps.ItemsSource = steps.Select((s, i) => StepCaption(s, i + 1)).ToArray();
            _steps.SelectedIndex = selectedIndex >= 0 && selectedIndex < steps.Count ? selectedIndex : -1;
            _summary.Text = SummaryCaption(steps);
        }
        finally { _refreshingSteps = false; }
        SelectedStepChanged();
    }

    private IReadOnlyList<MacroStep> VisibleSteps() =>
        _list.SelectedItem is MacroDefinition m ? m.Steps : _draft;

    private void SelectedStepChanged()
    {
        if (_refreshingSteps) return;
        var steps = VisibleSteps();
        int index = _steps.SelectedIndex;
        if (index >= 0 && index < steps.Count) _stepDelay.Text = steps[index].DelayMs.ToString();
        UpdateButtons();
    }

    private void AdjustLatency(int change)
    {
        if (!int.TryParse(_stepDelay.Text, out int current)) current = 0;
        _stepDelay.Text = Math.Clamp(current + change, 0, 30000).ToString();
    }

    private void ApplyStepDelay()
    {
        int index = _steps.SelectedIndex;
        var steps = VisibleSteps();
        if (index < 0 || index >= steps.Count || MacroCapture.IsRecording || App.Host.IsMacroPlaying) return;
        if (!int.TryParse(_stepDelay.Text, out int ms) || ms is < 0 or > 30000)
        {
            MacroDialog.Notice(this, _ar ? "زمن غير صحيح" : "Invalid delay",
                _ar ? "اكتب قيمة بين 0 و30000 ميلي ثانية." : "Enter a value from 0 to 30000 ms.");
            return;
        }
        bool saved = false;
        Safe(() =>
        {
            if (_list.SelectedItem is MacroDefinition m) _library.SetStepDelay(m, index, ms);
            else _draft[index].DelayMs = ms;
            saved = true;
        });
        if (saved)
        {
            RefreshStepList();
            _state.Text = _ar ? "تم تعديل زمن الخطوة. تأكد من تفعيل التوقيت عند التشغيل." :
                "Step delay updated. Keep recorded timing enabled for playback.";
        }
    }

    private string SummaryCaption(IReadOnlyList<MacroStep> steps)
    {
        if (steps.Count == 0) return _ar ? "0 خطوة" : "0 steps";
        int keys = steps.Count(s => s.Kind == "key");
        int mouse = steps.Count(s => s.Kind == "mouse");
        int move = steps.Count(s => s.Kind == "move");
        int wheel = steps.Count(s => s.Kind == "wheel");
        return _ar
            ? $"{steps.Count} خطوة | لوحة {keys} | نقرات {mouse} | تحريك {move} | عجلة {wheel}"
            : $"{steps.Count} steps | keys {keys} | clicks {mouse} | moves {move} | wheel {wheel}";
    }

    private string StepCaption(MacroStep s, int index)
    {
        string title = s.Kind switch
        {
            "key" => $"{(_ar ? "مفتاح" : "Key")} {(s.Data == 1 ? (_ar ? "رفع" : "Up") : (_ar ? "ضغط" : "Down"))}: {KeyLabel(s.Code)}",
            "move" => $"{(_ar ? "تحريك المؤشر" : "Move pointer")}: {s.X}, {s.Y}",
            "wheel" => $"{(_ar ? "عجلة الفأرة" : "Mouse wheel")}: {s.X}, {s.Y}",
            "mouse" => MouseLabel(s.Code, s.Data, s.X, s.Y),
            _ => s.Kind
        };
        string timing = s.DelayMs > 0 ? (_ar ? $"بعد {s.DelayMs}ms" : $"after {s.DelayMs}ms") : (_ar ? "فوري" : "instant");
        return $"{index:00}. {title} — {timing}";
    }

    private string KeyLabel(int vk)
    {
        try { return KeyInterop.KeyFromVirtualKey(vk).ToString(); }
        catch { return "VK " + vk; }
    }

    private string MouseLabel(int code, int data, int x, int y)
    {
        string label = code switch
        {
            0x201 => _ar ? "نقر يسار ↓" : "Left down",
            0x202 => _ar ? "نقر يسار ↑" : "Left up",
            0x204 => _ar ? "نقر يمين ↓" : "Right down",
            0x205 => _ar ? "نقر يمين ↑" : "Right up",
            0x207 => _ar ? "نقر وسط ↓" : "Middle down",
            0x208 => _ar ? "نقر وسط ↑" : "Middle up",
            0x20B => _ar ? "زر جانبي 1 ↓" : "X1 down",
            0x20C => _ar ? "زر جانبي 1 ↑" : "X1 up",
            _ => _ar ? "حدث فأرة" : "Mouse event"
        };
        return $"{label}: {x}, {y}";
    }

    private void TogglePlayback()
    {
        if (App.Host.IsMacroPlaying)
        {
            App.Host.CancelMacroPlayback();
            _state.Text = _ar ? "جاري إيقاف التشغيل…" : "Stopping playback…";
            return;
        }
        if (_list.SelectedItem is MacroDefinition m) _ = PlaySelectedAsync(m);
    }

    private async Task PlaySelectedAsync(MacroDefinition macro)
    {
        var playback = App.Host.PlayMacroAsync(macro);
        _state.Text = _ar ? "الماكرو يعمل. اضغط Esc لإيقافه فورًا." : "Macro playing. Press Esc to stop immediately.";
        UpdateButtons();
        try { await playback; }
        finally
        {
            _state.Text = _ar ? "توقف تشغيل الماكرو." : "Macro playback stopped.";
            UpdateButtons();
        }
    }

    private void ToggleRecording()
    {
        if (MacroCapture.IsRecording || _countdown > 0) Stop();
        else Begin();
    }

    private void Begin()
    {
        if (MacroCapture.IsRecording || _countdown > 0) return;
        if (_recordKeys.IsChecked != true && _recordClicks.IsChecked != true &&
            _recordMoves.IsChecked != true && _recordWheel.IsChecked != true)
        {
            MacroDialog.Notice(this, _ar ? "خيارات التسجيل" : "Recording inputs",
                _ar ? "حدد خيار تسجيل واحدًا على الأقل." : "Select at least one input type.");
            return;
        }
        if (_draft.Count > 0 && !MacroDialog.Confirm(this,
              _ar ? "تسجيل غير محفوظ" : "Unsaved recording",
              _ar ? "هل تريد تجاهل التسجيل السابق والبدء من جديد؟" : "Discard the unsaved recording and start again?",
              _ar ? "تسجيل جديد" : "Start new")) return;
        MacroHotkeys.CancelAssign();
        _suppressSelect = true;
        _list.SelectedItem = null;
        _suppressSelect = false;
        _draft.Clear();
        _countdown = 3;
        _startAt = DateTime.UtcNow.AddSeconds(3);
        _lastVisibleCount = -1;
        _state.Text = _ar ? "يبدأ التسجيل بعد 3 ثوانٍ؛ انتقل إلى النافذة المطلوبة." : "Recording starts in 3 seconds; switch to target app.";
        RefreshStepList();
        _timer.Start();
        UpdateButtons();
    }

    private void Tick()
    {
        if (_countdown > 0)
        {
            _countdown = Math.Max(0, (int)Math.Ceiling((_startAt - DateTime.UtcNow).TotalSeconds));
            if (_countdown == 0)
            {
                Safe(() =>
                {
                    App.Host.SetGestureSuspendedForCapture(true);
                    MacroCapture.Start(_recordKeys.IsChecked == true, _recordClicks.IsChecked == true,
                        _recordMoves.IsChecked == true, _recordWheel.IsChecked == true);
                });
                if (!MacroCapture.IsRecording)
                {
                    _timer.Stop();
                    App.Host.SetGestureSuspendedForCapture(false);
                }
            }
            else _state.Text = (_ar ? "يبدأ التسجيل بعد " : "Recording starts in ") + _countdown;
        }
        if (MacroCapture.IsRecording)
        {
            _state.Text = (_ar ? "● جاري التسجيل — " : "● RECORDING — ") + MacroCapture.Count + (_ar ? " خطوة" : " steps");
            if (_lastVisibleCount != MacroCapture.Count)
            {
                _lastVisibleCount = MacroCapture.Count;
                _draft = MacroCapture.Snapshot().ToList();
                RefreshStepList();
                if (_steps.Items.Count > 0) _steps.ScrollIntoView(_steps.Items[_steps.Items.Count - 1]);
            }
            if (MacroCapture.LimitReached) Stop();
        }
        UpdateButtons();
    }

    private void Stop()
    {
        _countdown = 0;
        _timer.Stop();
        if (MacroCapture.IsRecording)
        {
            _draft = MacroCapture.Stop();
            // Do not replay the click on Falak's own Stop Recording button.
            if (_record.IsLoaded)
            {
                var origin = _record.PointToScreen(new System.Windows.Point(0, 0));
                double right = origin.X + _record.ActualWidth;
                double bottom = origin.Y + _record.ActualHeight;
                _draft.RemoveAll(step => step.Kind == "mouse" &&
                    step.X >= origin.X && step.X <= right &&
                    step.Y >= origin.Y && step.Y <= bottom);
            }
        }
        App.Host.SetGestureSuspendedForCapture(false);
        _state.Text = _ar ? "توقف التسجيل. الخطوات باقية في القائمة؛ احفظها عند جاهزيتك." : "Stopped. Steps remain visible; save when ready.";
        RefreshStepList();
        UpdateButtons();
    }

    private void Save()
    {
        if (_draft.Count == 0) return;
        var saved = false;
        Safe(() =>
        {
            _library.Add(_name.Text, _draft, _useDelay.IsChecked != false, SelectedPlaybackMode(), ParseRepeatCount());
            saved = true;
        });
        if (!saved) return;
        _draft.Clear();
        Refresh();
        _state.Text = _ar ? "تم الحفظ. سيظهر الماكرو داخل Space." : "Saved. The macro is now available in Space.";
        _suppressSelect = true;
        _list.SelectedItem = _library.All.LastOrDefault();
        _suppressSelect = false;
        SelectMacro();
        UpdateButtons();
    }

    private void ApplySelectedChanges()
    {
        if (_list.SelectedItem is not MacroDefinition m) return;
        Safe(() => _library.Update(m, _name.Text, _useDelay.IsChecked != false, SelectedPlaybackMode(), ParseRepeatCount()));
        Refresh();
        _state.Text = _ar ? "تم حفظ التعديلات على الماكرو." : "Macro changes applied.";
    }

    private void DeleteSelected()
    {
        if (_list.SelectedItem is not MacroDefinition m) return;
        if (!MacroDialog.Confirm(this,
            _ar ? "حذف الماكرو" : "Delete macro",
            _ar ? $"هل تريد حذف «{m.Name}» نهائيًا؟ لا يمكن التراجع عن الحذف." : $"Delete '{m.Name}' permanently? This cannot be undone.",
            _ar ? "حذف الماكرو" : "Delete", danger: true)) return;
        Safe(() => _library.Delete(m));
        NewMacro(clearDraft: true);
        Refresh();
    }

    private void AssignShortcut()
    {
        if (_list.SelectedItem is not MacroDefinition selected || MacroCapture.IsRecording) return;
        if (!MacroHotkeys.IsReady)
        {
            MacroDialog.Notice(this,
                _ar ? "الاختصارات غير متاحة" : "Shortcuts unavailable",
                _ar ? "تعذّر التقاط اختصارات Windows. أعد تشغيل Falak وراجع سجل التشخيص." :
                "Windows shortcut capture is unavailable. Restart Falak and check diagnostics.");
            return;
        }
        _shortcutStatus.Text = _ar ? "اضغط اختصارًا الآن (مثل Ctrl+V أو M أو X1/X2). Esc للإلغاء، Backspace للمسح." :
            "Press Ctrl+V, M or mouse X1/X2. Esc cancels; Backspace clears.";
        string macroId = selected.Id;
        MacroHotkeys.BeginAssign(key =>
        {
            var macro = _library.Find(macroId);
            if (macro == null) return;
            if (key == null) { RefreshShortcutStatus(); return; }
            if (key == "") { Safe(() => _library.SetShortcut(macro, null)); Refresh(); RefreshShortcutStatus(); return; }
            bool plainKey = key.StartsWith("K:0:", StringComparison.Ordinal);
            bool systemShortcut = key is "K:1:86" or "K:1:67" or "K:1:88" or "K:1:90";
            if ((plainKey || systemShortcut) &&
                !MacroDialog.Confirm(this,
                  _ar ? "تأكيد الاختصار" : "Confirm shortcut",
                  _ar ? "هذا الاختصار قد يتعارض مع الكتابة والنسخ واللصق في البرامج الأخرى. هل تريد استخدامه رغم ذلك؟" :
                  "This shortcut may conflict with typing or Copy/Paste in other apps. Use it anyway?",
                  _ar ? "استخدام الاختصار" : "Use shortcut"))
            { RefreshShortcutStatus(); return; }
            Safe(() => _library.SetShortcut(macro, key));
            Refresh(); RefreshShortcutStatus();
        });
    }

    private void ClearShortcut()
    {
        MacroHotkeys.CancelAssign();
        if (_list.SelectedItem is MacroDefinition m) Safe(() => _library.SetShortcut(m, null));
        Refresh(); RefreshShortcutStatus();
    }

    private void RefreshShortcutStatus()
    {
        _shortcutStatus.Text = _list.SelectedItem is MacroDefinition m
            ? (_ar ? "الاختصار: " : "Shortcut: ") + MacroHotkeys.Describe(m.Shortcut)
            : (_ar ? "اختر ماكرو من القائمة لإضافة اختصار." : "Select a saved macro to set a shortcut.");
    }

    private string SelectedPlaybackMode() => _runLoop.IsChecked == true ? "loopuntilesc" : _runRepeat.IsChecked == true ? "repeat" : "once";
    private int ParseRepeatCount() => int.TryParse(_repeatCount.Text, out var n) ? Math.Clamp(n, 1, 999) : 2;

    private void UpdateButtons()
    {
        bool recording = MacroCapture.IsRecording || _countdown > 0;
        bool playing = App.Host.IsMacroPlaying;
        bool hasSelection = _list.SelectedItem is MacroDefinition;
        _scopeSave.IsEnabled = !recording && !playing && hasSelection &&
            (_appOnly.IsChecked != true || _applications.SelectedItem is MacroApplicationScope.Entry);
        _appOnly.IsEnabled = !recording && !playing && hasSelection;
        // Browsing is not the same as assigning an application scope.
        // Previously this was false until a macro AND "Only in this application"
        // were selected, disabling ListBox mouse wheel AND scrollbar buttons.
        // Keep browsing possible; restrict only the save action above.
        _applications.IsEnabled = true;
        _appBrowse.IsEnabled = !recording && !playing && hasSelection;
        _appRefresh.IsEnabled = !recording && !playing;
        bool selectedStep = _steps.SelectedIndex >= 0 && _steps.SelectedIndex < VisibleSteps().Count;
        _delayApply.IsEnabled = _delayMinus.IsEnabled = _delayPlus.IsEnabled = _stepDelay.IsEnabled =
            !recording && !playing && selectedStep;
        _record.IsEnabled = !playing;
        _record.Content = recording ? (_ar ? "■ إيقاف التسجيل" : "■ Stop recording") :
            (_ar ? "● بدء التسجيل" : "● Start record");
        _save.IsEnabled = !recording && !playing && _draft.Count > 0;
        _play.IsEnabled = !recording && (hasSelection || playing);
        _play.Content = playing ? (_ar ? "■ إيقاف (Esc)" : "■ Stop (Esc)") :
            (_ar ? "▶ تشغيل" : "▶ Play");
        _apply.IsEnabled = !recording && !playing && hasSelection;
        _delete.IsEnabled = !recording && !playing && hasSelection;
        _shortcutAssign.IsEnabled = !recording && !playing && hasSelection && MacroHotkeys.IsReady;
        _shortcutRemove.IsEnabled = !recording && !playing && hasSelection &&
            _list.SelectedItem is MacroDefinition selected && selected.Shortcut != null;
        _recordKeys.IsEnabled = !recording;
        _recordClicks.IsEnabled = !recording;
        _recordMoves.IsEnabled = !recording;
        _recordWheel.IsEnabled = !recording;
        _repeatCount.IsEnabled = _runRepeat.IsChecked == true && !recording;
        _useDelay.IsEnabled = !recording;
        _runOnce.IsEnabled = !recording;
        _runRepeat.IsEnabled = !recording;
        _runLoop.IsEnabled = !recording;
    }

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception e) { MacroDialog.Notice(this, _ar ? "تعذر إكمال العملية" : "Operation failed", e.Message); }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (MacroCapture.IsRecording || _countdown > 0) Stop();
        if (_draft.Count > 0)
        {
            bool discard = MacroDialog.Confirm(this,
                _ar ? "تسجيل غير محفوظ" : "Unsaved recording",
                _ar ? "لم تحفظ التسجيل الحالي. هل تريد إغلاق الصفحة والتخلي عن الخطوات؟" :
                      "You haven't saved this recording. Close and discard the steps?",
                _ar ? "إغلاق دون حفظ" : "Discard and close");
            if (!discard) e.Cancel = true;
        }
        if (!e.Cancel) { _timer.Stop(); MacroHotkeys.CancelAssign(); }
    }

    private sealed class RelayCommand : ICommand
    {
        private readonly Action _call;
        public RelayCommand(Action call) { _call = call; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _call();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

