using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Falak.Interop;

namespace Falak.App;

// Fallback only for the *installed* Google Quick Share Windows desktop app.
// Do not emulate Google's transfer protocol, click an Android device, or press Send.
// Window automation is version/language-dependent and intentionally fails closed.
internal static class QuickShareUiAutomation
{
    internal enum Preparation
    {
        FileSelected,
        AppWindowNotFound,
        SelectFilesNotFound,
        FileDialogNotFound,
        FileDialogNotCompleted
    }

    private static readonly string[] ShareWindowNames = { "Quick Share", "Nearby Share", "المشاركة السريعة" };
    private static readonly string[] SelectFileNames =
    {
        "Select files", "Choose files", "Browse files", "Add files", "اختيار ملفات",
        "اختر ملفات", "تحديد ملفات", "اختيار ملف", "اختر ملفًا", "ملفات auswählen"
    };

    // A user must be able to cancel a pending preparation via the job UI.
    internal static Preparation Prepare(string appPath, string source, CancellationToken ct)
    {
        if (!File.Exists(source)) throw new FileNotFoundException("Source file no longer exists", source);
        if (Directory.Exists(source)) throw new NotSupportedException("Select a file, not a folder.");
        ct.ThrowIfCancellationRequested();
        Log.Write("Quick Share: opening installed app and attempting UI file selection: " + Path.GetFileName(source));
        using Process? launch = Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
        var appWindow = WaitForShareWindow(TimeSpan.FromSeconds(18), ct);
        if (appWindow == null) return Preparation.AppWindowNotFound;
        int sharePid = SafePid(appWindow);
        try { appWindow.SetFocus(); } catch (Exception ex) { Log.Write("Quick Share focus: " + ex.Message); }

        // Trigger ONLY the file-picker control of Quick Share. Avoid generic or
        // positional clicks that might touch Send or an unintended recipient.
        if (!TryOpenSelectFiles(appWindow)) return Preparation.SelectFilesNotFound;
        var picker = WaitForPicker(sharePid, appWindow, TimeSpan.FromSeconds(12), ct);
        if (picker == null) return Preparation.FileDialogNotFound;
        Log.Write("Quick Share: Windows file picker found; choosing original source file.");
        if (!TrySetFileName(picker, source)) return Preparation.FileDialogNotCompleted;
        if (!TryAcceptFilePicker(picker)) return Preparation.FileDialogNotCompleted;

        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(12))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsPickerAlive(picker))
            {
                Log.Write("Quick Share: file picker closed after Open. User should verify file is listed before selecting a phone.");
                return Preparation.FileSelected;
            }
            Thread.Sleep(300);
        }
        return Preparation.FileDialogNotCompleted;
    }

    private static AutomationElement? WaitForShareWindow(TimeSpan limit, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < limit)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var all = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                foreach (AutomationElement candidate in all)
                {
                    if (!IsShareWindow(candidate)) continue;
                    if (!candidate.Current.IsOffscreen) return candidate;
                }
            }
            catch (Exception ex) { Log.Write("Quick Share window search: " + ex.Message); }
            Thread.Sleep(350);
        }
        return null;
    }

    private static bool IsShareWindow(AutomationElement candidate)
    {
        try
        {
            // A browser tab or an unrelated window can have a "Quick Share"
            // title too. Never automate it: require Google's app process name.
            int pid = candidate.Current.ProcessId;
            if (!IsSharePid(pid)) return false;
            var name = candidate.Current.Name ?? string.Empty;
            return ContainsAny(name, ShareWindowNames) || !candidate.Current.IsOffscreen;
        }
        catch { return false; }
    }

    private static bool TryOpenSelectFiles(AutomationElement app)
    {
        try
        {
            var elements = app.FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
            foreach (AutomationElement el in elements)
            {
                string name;
                try { name = el.Current.Name ?? string.Empty; } catch { continue; }
                if (!ContainsAny(name, SelectFileNames)) continue;
                if (TryInvokeControl(el))
                {
                    Log.Write("Quick Share: invoked Select files control: " + name);
                    return true;
                }
                // A clickable parent sometimes owns the accessible text label.
                AutomationElement? parent = TreeWalker.ControlViewWalker.GetParent(el);
                if (parent != null && SafePid(parent) == SafePid(app) && TryInvokeControl(parent))
                {
                    Log.Write("Quick Share: invoked parent of Select files control: " + name);
                    return true;
                }
            }
        }
        catch (Exception ex) { Log.Write("Quick Share Select files search: " + ex); }
        return false;
    }

    private static AutomationElement? WaitForPicker(int sharePid, AutomationElement owner, TimeSpan limit, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < limit)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                foreach (AutomationElement window in windows)
                {
                    int pid = SafePid(window);
                    if (pid <= 0 || (pid != sharePid && !IsSharePid(pid))) continue;
                    string klass = window.Current.ClassName ?? string.Empty;
                    string title = window.Current.Name ?? string.Empty;
                    if (klass == "#32770" || title.Equals("Open", StringComparison.OrdinalIgnoreCase)
                        || title.Equals("فتح", StringComparison.OrdinalIgnoreCase)
                        || title.Equals("Select File", StringComparison.OrdinalIgnoreCase)
                        || title.Equals("Datei öffnen", StringComparison.OrdinalIgnoreCase))
                        return window;
                }
                // Some versions expose the picker as an owned window in the tree.
                var nested = owner.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
                foreach (AutomationElement el in nested)
                    if ((el.Current.ClassName ?? "") == "#32770") return el;
            }
            catch (Exception ex) { Log.Write("Quick Share picker search: " + ex.Message); }
            Thread.Sleep(300);
        }
        return null;
    }

    private static bool IsSharePid(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            string name = proc.ProcessName;
            return name.Contains("nearby_share", StringComparison.OrdinalIgnoreCase)
                || name.Contains("quickshare", StringComparison.OrdinalIgnoreCase)
                || name.Contains("quick share", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool TrySetFileName(AutomationElement picker, string absoluteFile)
    {
        try
        {
            var edits = picker.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            AutomationElement? selected = null;
            foreach (AutomationElement el in edits)
            {
                string name = el.Current.Name ?? string.Empty;
                if (ContainsAny(name, new[] { "File name", "Filename", "اسم الملف", "Dateiname", "Nome file" }))
                { selected = el; break; }
            }
            if (selected == null)
            {
                // In classic common dialogs, the filename edit is usually the
                // last editable control; do not pick a Search or Address field.
                foreach (AutomationElement el in edits.Cast<AutomationElement>().Reverse())
                {
                    string name = el.Current.Name ?? string.Empty;
                    if (ContainsAny(name, new[] { "Search", "بحث", "Address", "العنوان" })) continue;
                    if (!el.TryGetCurrentPattern(ValuePattern.Pattern, out object _)) continue;
                    selected = el; break;
                }
            }
            if (selected == null) return false;
            if (!selected.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePattern)) return false;
            var value = (ValuePattern)valuePattern;
            if (value.Current.IsReadOnly) return false;
            value.SetValue(absoluteFile);
            Log.Write("Quick Share: Windows picker filename entered using UI Automation.");
            return true;
        }
        catch (Exception ex) { Log.Write("Quick Share filename entry failed: " + ex); return false; }
    }

    private static bool TryAcceptFilePicker(AutomationElement picker)
    {
        try
        {
            var buttons = picker.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement button in buttons)
            {
                string name = (button.Current.Name ?? string.Empty).Replace("&", "").Trim();
                if (name.Equals("Open", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("فتح", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Öffnen", StringComparison.OrdinalIgnoreCase))
                    return TryInvokeControl(button);
            }
        }
        catch (Exception ex) { Log.Write("Quick Share picker Open button: " + ex); }
        return false;
    }

    private static bool IsPickerAlive(AutomationElement picker)
    {
        try { return !picker.Current.IsOffscreen && picker.Current.NativeWindowHandle != 0; }
        catch (ElementNotAvailableException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (COMException) { return false; }
    }

    private static bool TryInvokeControl(AutomationElement el)
    {
        try
        {
            if (el.TryGetCurrentPattern(InvokePattern.Pattern, out object value))
            { ((InvokePattern)value).Invoke(); return true; }
            // .NET 10 WPF does not expose LegacyIAccessiblePattern. Do not
            // fall back to mouse coordinates or arbitrary keyboard clicks:
            // only invoke a recognized UI Automation action, or fail closed.
        }
        catch (Exception ex) { Log.Write("Quick Share invoking UI control: " + ex.Message); }
        return false;
    }

    private static int SafePid(AutomationElement element)
    {
        try { return element.Current.ProcessId; } catch { return -1; }
    }

    private static bool ContainsAny(string input, string[] terms) =>
        terms.Any(term => input.Contains(term, StringComparison.OrdinalIgnoreCase));
}
