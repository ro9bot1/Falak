using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Falak.App;

// A macro can be global or restricted to the executable of the ACTIVE foreground window.
// No process is started by choosing an app, and the app's window is never activated here.
internal static class MacroApplicationScope
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder buffer, ref int chars);

    internal static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.GetFullPath(path.Trim().Trim('"')); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    internal static string? ForegroundExecutable()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;
        GetWindowThreadProcessId(window, out var pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return null;
        var process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process == IntPtr.Zero) return null; // fail closed if the executable cannot be verified
        try
        {
            var buffer = new StringBuilder(32768);
            var length = buffer.Capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref length) ? Normalize(buffer.ToString()) : null;
        }
        finally { CloseHandle(process); }
    }

    // Scoped macro wins over global macro sharing the same shortcut.
    // Unknown foreground must NEVER trigger an app-scoped action.
    internal static MacroDefinition? Resolve(IEnumerable<MacroDefinition> macros, string shortcut, string? foregroundExe)
    {
        var matching = macros.Where(m => string.Equals(m.Shortcut, shortcut, StringComparison.Ordinal)).ToArray();
        var foreground = Normalize(foregroundExe);
        if (foreground != null)
        {
            var specific = matching.FirstOrDefault(m => m.RestrictToApplication &&
                string.Equals(Normalize(m.TargetExecutable), foreground, StringComparison.OrdinalIgnoreCase));
            if (specific != null) return specific;
        }
        return matching.FirstOrDefault(m => !m.RestrictToApplication);
    }

    internal static bool Conflicts(IEnumerable<MacroDefinition> macros, MacroDefinition current, string? shortcut,
        bool restricted, string? targetExecutable)
    {
        if (shortcut == null) return false;
        var target = Normalize(targetExecutable);
        return macros.Any(m => !ReferenceEquals(m, current) && m.Shortcut == shortcut &&
            (restricted ? m.RestrictToApplication && string.Equals(Normalize(m.TargetExecutable), target, StringComparison.OrdinalIgnoreCase)
                        : !m.RestrictToApplication));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr IconHandle;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string filename, uint attributes, ref ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    // Shell icon lookup can involve third-party shell extensions. Perform the
    // lookup off the UI thread, then marshal WPF image creation to the dispatcher.
    private static IntPtr ExtractExecutableIcon(string path)
    {
        var info = new ShellFileInfo();
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100) == IntPtr.Zero)
            return IntPtr.Zero;
        return info.IconHandle;
    }

    internal sealed class Entry : INotifyPropertyChanged
    {
        private bool _iconRequested;
        private ImageSource? _icon;
        public string Name { get; }
        public string Path { get; }
        public Entry(string name, string path)
        {
            Name = string.IsNullOrWhiteSpace(name) ? System.IO.Path.GetFileNameWithoutExtension(path) : name.Trim();
            Path = path;
        }
        public ImageSource? Icon
        {
            get
            {
                if (!_iconRequested)
                {
                    _iconRequested = true;
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        _ = Task.Run(() =>
                        {
                            IntPtr iconHandle = IntPtr.Zero;
                            try
                            {
                                iconHandle = ExtractExecutableIcon(Path);
                                if (iconHandle == IntPtr.Zero || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                                    return;
                                // WPF image objects are created on the UI thread. The native
                                // icon handle is always freed even if the window has closed.
                                dispatcher.Invoke(() =>
                                {
                                    var image = Imaging.CreateBitmapSourceFromHIcon(
                                        iconHandle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                    image.Freeze();
                                    _icon = image;
                                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
                                });
                            }
                            catch { /* unavailable icon: leave the app name visible */ }
                            finally { if (iconHandle != IntPtr.Zero) DestroyIcon(iconHandle); }
                        });
                    }
                }
                return _icon;
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => Name;
    }

    internal static List<Entry> Enumerate()
    {
        var choices = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, string? path)
        {
            var full = Normalize(path);
            if (full == null || !full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return;
            if (!choices.ContainsKey(full)) choices[full] = new Entry(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(full) : name!, full);
        }
        // Running desktop apps can be listed even when their Start Menu entry is absent.
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    Add(p.ProcessName, p.MainModule?.FileName);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            finally { p.Dispose(); }
        }
        // Desktop programs installed in Start Menu. Some Store/UWP apps do not expose
        // an .exe shortcut, so the UI also offers selection from an EXE file directly.
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                                       Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                foreach (var link in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories).Take(700))
                    Add(Path.GetFileNameWithoutExtension(link), ShortcutTarget(link));
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException) { }
        }
        return choices.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string? ShortcutTarget(string filename)
    {
        object? shell = null, shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { filename });
            return shortcut?.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null) as string;
        }
        catch (Exception e) when (e is COMException or MissingMethodException or TargetInvocationException or UnauthorizedAccessException)
        { return null; }
        finally
        {
            if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
