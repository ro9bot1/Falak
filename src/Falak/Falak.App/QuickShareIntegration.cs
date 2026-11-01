using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

// Space > Quick Share: never show the old Falak choice window.
// Prefer the officially installed Explorer "Send with Quick Share" shell verb,
// because it passes the actual selected file to Quick Share without guessing
// an undocumented command-line protocol.
internal static class QuickShareIntegration
{
    private const string OfficialInstallUrl = "https://www.android.com/quick-share/with-windows-pc/";

    internal static void Open(JobRunner jobs, WheelAction action, IReadOnlyList<string> paths)
    {
        var job = jobs.Track(action, paths);
        try
        {
            string[] valid = paths.Where(p => File.Exists(p) || Directory.Exists(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (valid.Length == 0)
            {
                job.Fail(UiText.IsArabic ? "لم يتم العثور على الملف المحدد." : "Selected file not found.");
                return;
            }

            // Google documents this Explorer command for Windows. It is the
            // only supported way here to preselect the exact file automatically.
            if (valid.Length == 1 && TryInvokeExplorerQuickShare(valid[0]))
            {
                job.Complete(new JobResult(Array.Empty<string>(), null,
                    UiText.IsArabic
                        ? "فُتحت المشاركة بالملف المحدد. اختر هاتفك ثم أكّد الإرسال في Quick Share."
                        : "Quick Share opened with your file. Choose your phone and confirm the transfer."),
                    succeededWithoutFiles: true);
                return;
            }

            // A Google installation may expose a modern Explorer-only shell
            // extension (no enumerable Shell.Application verb). In that case,
            // open the genuine Quick Share application and select the file
            // using its on-screen Select files control and the Windows file
            // picker. The final recipient and Send action remain with the user.
            string? app = FindGoogleQuickShare();
            if (app is not null)
            {
                // Never silently ignore additional files or claim they were sent.
                if (valid.Length != 1)
                {
                    job.Fail(UiText.IsArabic
                        ? "اختر ملفًا واحدًا في كل مرة للمشاركة التلقائية عبر Quick Share."
                        : "Select one file at a time for automatic Quick Share attachment.");
                    return;
                }
                _ = PrepareViaAutomationAsync(app, valid[0], job);
                return;
            }

            // Not installed: open only Google's official download page. No
            // third-party site, custom selection dialog or background upload.
            Process.Start(new ProcessStartInfo(OfficialInstallUrl) { UseShellExecute = true });
            job.Complete(new JobResult(Array.Empty<string>(), null,
                UiText.IsArabic
                    ? "Quick Share غير مثبت. فُتحت صفحة Google الرسمية للتثبيت."
                    : "Quick Share is not installed. Google's official download page was opened."),
                succeededWithoutFiles: true);
        }
        catch (Exception ex)
        {
            job.Fail((UiText.IsArabic ? "تعذّر فتح Quick Share: " : "Could not open Quick Share: ") + ex.Message);
            Log.Write("Quick Share: " + ex);
        }
    }

    private static async Task PrepareViaAutomationAsync(string app, string path, JobItem job)
    {
        try
        {
            // UI Automation waits must not freeze Falak's WPF dispatcher.
            QuickShareUiAutomation.Preparation result = await Task.Run(
                () => QuickShareUiAutomation.Prepare(app, path, job.Cancellation.Token));
            if (result == QuickShareUiAutomation.Preparation.FileSelected)
            {
                job.Complete(new JobResult(Array.Empty<string>(), null,
                    UiText.IsArabic
                        ? "تم إدخال الملف في Quick Share. اختر الهاتف وأكّد الإرسال."
                        : "File selected in Quick Share. Choose the phone and confirm sending."),
                    succeededWithoutFiles: true);
            }
            else
            {
                job.Fail(result switch
                {
                    QuickShareUiAutomation.Preparation.AppWindowNotFound => UiText.IsArabic
                        ? "فُتح Quick Share لكن لم تظهر نافذته للفحص. افتحه من شريط المهام ثم حاول مجددًا."
                        : "Quick Share started, but its window could not be detected. Open it from the taskbar and try again.",
                    QuickShareUiAutomation.Preparation.FileDialogNotFound => UiText.IsArabic
                        ? "لم يظهر مربع اختيار الملفات في Quick Share. ربما تغيّرت واجهته؛ تحقق من سجل Falak."
                        : "Quick Share's file picker did not appear. The app UI may have changed; check Falak's log.",
                    QuickShareUiAutomation.Preparation.FileDialogNotCompleted => UiText.IsArabic
                        ? "لم يؤكد Quick Share اختيار الملف. لم يتم إرسال أي شيء."
                        : "Quick Share did not accept the selected file. Nothing was sent.",
                    _ => UiText.IsArabic
                        ? "لم أتمكن من العثور على زر اختيار الملفات في Quick Share. لم يتم إرسال أي شيء."
                        : "Could not find Quick Share's Select files control. Nothing was sent."
                });
            }
        }
        catch (OperationCanceledException)
        {
            job.MarkCancelled();
        }
        catch (Exception ex)
        {
            Log.Write("Quick Share UI automation error: " + ex);
            job.Fail((UiText.IsArabic ? "تعذر تجهيز الملف في Quick Share: "
                : "Unable to prepare the file in Quick Share: ") + ex.Message);
        }
    }

    private static bool TryInvokeExplorerQuickShare(string path)
    {
        try
        {
            Type? type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return false;
            dynamic shell = Activator.CreateInstance(type)!;
            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string? parent = Path.GetDirectoryName(full);
            if (string.IsNullOrWhiteSpace(parent)) return false;
            dynamic folder = shell.NameSpace(parent);
            if (folder == null) return false;
            dynamic item = folder.ParseName(Path.GetFileName(full));
            if (item == null) return false;
            foreach (dynamic verb in item.Verbs())
            {
                string label = ((string?)verb.Name ?? string.Empty)
                    .Replace("&", string.Empty).Replace("…", string.Empty).Trim();
                if (!IsQuickShareVerb(label)) continue;
                verb.DoIt();
                Log.Write("Quick Share: invoked registered Explorer shell verb for selected file.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Write("Quick Share Explorer verb unavailable: " + ex.Message);
        }
        return false;
    }

    private static bool IsQuickShareVerb(string label) =>
        label.Contains("Quick Share", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("Nearby Share", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("كويك شير", StringComparison.Ordinal) ||
        label.Contains("المشاركة السريعة", StringComparison.Ordinal);

    private static string? FindGoogleQuickShare()
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        string[] relatives =
        {
            @"Google\NearbyShare\nearby_share.exe",
            @"Google\NearbyShare\nearby_share_launcher.exe",
            @"Google\NearbyShare\QuickShare.exe",
            @"Google\Quick Share\nearby_share.exe",
            @"Google\Quick Share\QuickShare.exe",
            @"Google\QuickShare\QuickShare.exe",
            @"Google\Nearby Share\nearby_share.exe"
        };
        foreach (string root in roots)
        {
            if (string.IsNullOrEmpty(root)) continue;
            foreach (string relative in relatives)
            {
                string candidate = Path.Combine(root, relative);
                if (File.Exists(candidate)) return candidate;
            }
        }
        // Official installations can register a Start Menu shortcut even
        // when the executable is placed in an uncommon installation folder.
        string[] menus =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        };
        foreach (string menu in menus)
        {
            if (string.IsNullOrEmpty(menu)) continue;
            foreach (string filename in new[] { "Quick Share.lnk", "Quick Share from Google.lnk" })
            {
                string shortcut = Path.Combine(menu, filename);
                if (File.Exists(shortcut)) return shortcut;
            }
        }
        return null;
    }
}
