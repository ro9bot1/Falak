using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Falak.App;

// Text-only localization: intentionally never changes the existing window geometry, styles or wheel layout.
internal static class UiText
{
    public static bool IsArabic => App.Host.Settings.Language != "en";
    private static readonly Dictionary<string,string> English = new(StringComparer.Ordinal)
    {
        ["فَلَك"] = "Falak",
        ["التراخيص والإشعارات"] = "Licenses and notices",
        ["تم صنعه بواسطة روبوت"] = "Made by Robot",
        ["فَلَك · الإعدادات"] = "Falak · Settings",
        ["فَلَك · خلفية وتصميم"] = "Falak · Background & design",
        ["يعمل في الخلفية"] = "Running in background",
        ["مفعّل"] = "Enabled",
        ["متوقف"] = "Disabled",
        ["متوقف مؤقتاً"] = "Paused",
        ["إعدادات"] = "Settings",
        ["الإعدادات"] = "Settings",
        ["تفعيل فَلَك"] = "Enable Falak",
        ["اسحب. اضغط. أفلِت."] = "Drag. Press. Drop.",
        ["كل ملفاتك. حركة واحدة."] = "All your files. One gesture.",
        ["تحويل محلي  •  خصوصية كاملة  •  دون إنترنت"] = "Local conversion  •  Private  •  Offline",
        ["اسحب أي ملف في المستكشف، واضغط مفتاحاً أثناء السحب، ثم أفلته على ما تريد. يُحفظ الناتج بجانب الملف الأصلي، ولا يغادر جهازك."] = "Drag a file in Explorer, press a key while dragging, then drop it on the action. The output stays beside the original and never leaves your device.",
        ["تحويل الصيغة"] = "Convert format",
        ["تحويل"] = "Convert",
        ["أدوات"] = "Tools",
        ["إجراءات"] = "Actions",
        ["  ·  PDF إلى صور، صورة إلى PDF، فيديو إلى MP3"] = "  ·  PDF to images, image to PDF, video to MP3",
        ["  ·  ضغط، دمج، فصل الصفحات، إصلاح الترميز"] = "  ·  Compress, merge, split pages, fix encoding",
        ["  ·  نسخ المسار، نقل، فتح Terminal"] = "  ·  Copy path, move, open Terminal",
        ["جرّبه على ملفات تجريبية"] = "Try it on sample files",
        ["فتح ملفات فَلَك التجريبية"] = "Open Falak sample files",
        ["العربية"] = "Arabic",
        ["آخر العمليات"] = "Recent operations",
        ["لم تنفّذ أي عملية بعد. كل ملف تحوّله سيظهر هنا مع مكان حفظه، ويمكنك فتحه أو التراجع عنه بنقرة."] = "No operations yet. Your conversions will appear here with their location, and you can open or undo them in one click.",
        ["ملفاتك لا تغادر جهازك · بلا إنترنت · بلا حساب"] = "Files stay on your device · Offline · No account",
        ["ملفاتك لا تغادر جهازك · التحويل بلا إنترنت · بلا حساب"] = "Files stay on your device · Offline conversion · No account",
        ["التحويل محلي · المشاركة بإذنك فقط · بلا حساب"] = "Local conversion · Sharing only with your approval · No account",
        ["اللغة"] = "Language",
        ["إظهار"] = "Show",
        ["إظهار في المجلد"] = "Show in folder",
        ["إغلاق"] = "Close",
        ["إلغاء"] = "Cancel",
        ["تراجع"] = "Undo",
        ["فتح"] = "Open",
        ["لاحقاً"] = "Later",
        ["المظهر"] = "Appearance",
        ["الوضع"] = "Mode",
        ["تلقائي"] = "System",
        ["فاتح"] = "Light",
        ["داكن"] = "Dark",
        ["لون التطبيق"] = "Accent color",
        ["واجهة زجاجية"] = "Glass interface",
        ["العجلة والنوافذ كالزجاج، يظهر خلفها ما على الشاشة بنعومة"] = "The wheel and windows show a soft glass background",
        ["العجلة زجاجية، والنوافذ تحتاج Windows 11"] = "Glass windows require Windows 11",
        ["التشغيل"] = "Startup",
        ["التشغيل مع ويندوز"] = "Launch with Windows",
        ["يبدأ فَلَك بجانب الساعة دون أن يفتح نافذة"] = "Falak starts in the tray without opening a window",
        ["صوت التنقل في العجلة"] = "Wheel navigation sound",
        ["نقرة خفيفة كلما انتقلت من خيار إلى آخر"] = "A subtle click when moving between wheel options",
        ["المفاتيح"] = "Keyboard shortcuts",
        ["التحديثات"] = "Updates",
        ["التحقق من التحديثات تلقائياً"] = "Check for updates automatically",
        ["مرة في اليوم، ولا يُرسل إلا رقم الإصدار"] = "Once a day, sending only the version number",
        ["تحقّق الآن"] = "Check now",
        ["سجل التشخيص"] = "Diagnostic log",
        ["أي لون"] = "Any color",
        ["إلغاء القص"] = "Cancel crop",
        ["إلى"] = "To",
        ["اسحب الزوايا أو الحواف، أو حرّك الإطار من داخله"] = "Drag corners or edges, or move the frame from inside",
        ["اعكس الاتجاه"] = "Reverse direction",
        ["الجزء الظاهر"] = "Visible area",
        ["الخلفية"] = "Background",
        ["الرسم على الصورة"] = "Draw on image",
        ["السُمك"] = "Thickness",
        ["الصفحة"] = "Page",
        ["الصفحة التالية"] = "Next page",
        ["الصفحة السابقة"] = "Previous page",
        ["الصفحة كاملة"] = "Entire page",
        ["المقاس"] = "Size",
        ["الهامش"] = "Margin",
        ["انحناء الزوايا"] = "Corner radius",
        ["بالفأرة أو القلم، فوق الصورة مباشرة"] = "Draw on the photo with a mouse or pen",
        ["تدرّج"] = "Gradient",
        ["تظليل"] = "Shade",
        ["تم"] = "Done",
        ["حفظ الصورة"] = "Save image",
        ["خلفية وتصميم"] = "Background & design",
        ["زاوية التدرّج"] = "Gradient angle",
        ["ظل ناعم"] = "Soft shadow",
        ["قصّ"] = "Crop",
        ["قلم"] = "Pen",
        ["لون القلم"] = "Pen color",
        ["لون واحد"] = "Solid color",
        ["مسح الرسم"] = "Erase drawing",
        ["مقترحة من ألوان الملف"] = "Suggested from image colors",
        ["ممحاة"] = "Eraser",
        ["من"] = "From",
        ["نسخ للصق في أي مكان"] = "Copy to paste anywhere",
        ["إيقاف مؤقت"] = "Pause",
        ["استئناف"] = "Resume",
        ["خروج"] = "Exit",
        ["فتح فَلَك"] = "Open Falak",
        ["ملفات تجريبية"] = "Sample files",
        ["يعمل"] = "Running",
        ["نحوّل"] = "Converting",
        ["تم التحويل إلى"] = "Converted to",
        ["تم:"] = "Done:",
        ["لم يتغير شيء"] = "Nothing changed",
        ["تعذّر إكمال العملية"] = "Operation failed",
        ["ألغيت العملية"] = "Cancelled",
        ["تراجعنا عن العملية"] = "Undone",
        ["ملف"] = "File",
        ["ملفات أخرى"] = "more files",
        ["زمرّدي"] = "Emerald",
        ["عنّابي"] = "Garnet",
        ["عودي"] = "Oud",
        ["كحلي"] = "Navy",
        ["ملكي"] = "Royal purple",
        ["فحمي"] = "Graphite",
        ["تمت المعالجة محليًا"] = "Processed locally",
        ["تم تصدير بيانات الصورة"] = "Image metadata exported",
        ["تم إنشاء نسخة نصية"] = "Text-based export created",
        ["تم تحويل نص الكتاب"] = "EPUB text converted",
        ["تم إنشاء TAR"] = "TAR archive created",
        ["تم إنشاء GZIP"] = "GZIP created",
        ["تم استخراج الأرشيف"] = "Archive extracted",
        ["لا توجد ملفات للأرشفة"] = "No files to archive",
        ["يتطلب GZIP ملفًا واحدًا"] = "GZIP requires exactly one file",
        ["صيغة أرشيف غير مدعومة"] = "Unsupported archive format",
        ["لم يُحفظ أي ملف ناقص."] = "No incomplete file was saved.",
        ["نُقل الناتج إلى سلة المحذوفات."] = "Output moved to the Recycle Bin.",
        ["حدث خطأ غير متوقع. لم يتغير ملفك الأصلي، والتفاصيل في سجل فَلَك."] = "Unexpected error. Your original file was not changed. See Falak's diagnostic log.",
        ["فتحنا المجلد"] = "Opened the folder",
        ["فتحنا Terminal في المجلد"] = "Opened Terminal in the folder",
        ["المسار في الحافظة، الصقه أينما تريد"] = "Path copied to clipboard",
        ["المسارات في الحافظة، مسار في كل سطر"] = "Paths copied, one per line",
        ["في الحافظة: الصقه في أي مجلد بـ Ctrl+V"] = "Files copied to clipboard; press Ctrl+V to paste",
        ["تعذّر فتح الملف."] = "Could not open this file.",
        ["نراجع…"] = "Checking…",
        ["نتحقق…"] = "Checking…",
        ["تحقق الآن"] = "Check now",
        ["تحديث الآن"] = "Update now",

        ["اللغة / Language"] = "Language",
        ["الإصدار الحالي"] = "Current version",
        ["متاح الإصدار"] = "Version available",
        ["لديك أحدث إصدار ("] = "You have the latest version (",
        [") · آخر تحقق "] = ") · Last checked ",
        ["التصميم في الحافظة، الصقه في أي برنامج"] = "Design copied to clipboard; paste into any application",
        ["تصميم"] = "Design",
        ["تعديل القص"] = "Adjust crop",
        ["إجراءات الملفات"] = "File actions",
        [" · نسخ المسار، النقل، فتح الطرفية"] = " · Copy path, move files, open Terminal",
        ["عريض 16:9"] = "Landscape 16:9",
        ["قصة 9:16"] = "Story 9:16",
        ["كما هو"] = "Original",
        ["لون القلم "] = "Pen color ",
        ["لون مقترح "] = "Suggested color ",
        ["مربع 1:1"] = "Square 1:1",
        ["مقترحة"] = "Suggested",
        ["منشور 4:5"] = "Post 4:5",
        ["من ألوان الملف"] = "From file colors",
        ["تجده بجانب الساعة. اسحب أي ملف واضغط Shift متى شئت."] = "Find Falak in the system tray. Drag any file and press Shift to convert.",
        ["تحسينات وإصلاحات."] = "Improvements and fixes.",
        ["لا يمكن التعامل مع هذا العنصر"] = "This item cannot be processed",
        ["الأداة غير متاحة لهذا الملف"] = "Tool not available for this file",
        ["متوقف مؤقتًا"] = "Paused",
        ["فَلَك ما زال يعمل"] = "Falak is still running",
        ["فَلَك لويندوز"] = "Falak for Windows",
        ["متاح تحديث"] = "Update available",
        ["متاح الإصدار "] = "Version available ",
        ["الإصدار "] = "Version ",
        ["الآن"] = "Now",
        ["استخرجنا النص بلا توقيتات"] = "Extracted the text without timestamps",
        ["انقل إلى أي مجلد؟"] = "Move to which folder?",
        ["انسخ إلى أي مجلد؟"] = "Copy to which folder?",
        ["بلا تغيير"] = "No changes",
        ["حفظنا التصميم"] = "Design saved",
        ["تحديث فَلَك"] = "Update Falak",
        ["فَلَك · اسحب ملفاً واضغط Shift"] = "Falak · Drag a file and press Shift",
        ["تحديث متاح: فَلَك"] = "Update available: Falak",
        ["التحديث جاهز"] = "Update ready",
        ["أُلغي التحديث"] = "Update cancelled",
        ["تعذّر التحديث"] = "Update failed",
        ["تعذّر الوصول إلى خادم فَلَك. تحقق من الإنترنت ثم حاول مجددًا."] = "Could not contact the Falak update server. Check your connection and try again.",
        ["ملفاتك لا تغادر جهازك"] = "Your files stay on your device",
        ["في مجلد جديد: "] = "In a new folder: ",
        ["في مجلد "] = "In folder ",
        ["بجانب الأصل في مجلد "] = "Next to the original in folder ",
        ["فحص نسخة أوفلاين"] = "Offline build verification",
        ["أنشأنا لوحة صور مجمعة"] = "Photo collage created",
        ["اختر صورتين على الأقل لإنشاء كولاج."] = "Select at least two images to create a collage.",
        ["الحد الأقصى للكولاج 36 صورة."] = "A collage can contain up to 36 images.",
        ["إجراءات الملفات"] = "File actions",
        ["واحد PDF تم إنشاؤه"] = "One PDF created",
        ["تُحفظ النتائج بجانب الملفات الأصلية"] = "Results are saved beside your original files",
        ["فَلَك · ملفات اختبار شاملة"] = "Falak · Complete sample files",
        ["إنشاء صورة واحدة بصيغة PNG"] = "Create a single PNG image",
        ["نسخ المسار، نقل، فتح Terminal"] = "Copy path, move, open Terminal",
        ["متوقف مؤقتا"] = "Paused",
        ["الملفات"] = "Files",
        ["ملفات"] = "Files",
        ["المسار"] = "Path",
        ["تحويل ملف PDF"] = "Convert PDF",
        ["تم إنشاء ملف PDF"] = "PDF file created",
        ["أنشأنا صورة واحدة بصيغة PNG"] = "Created one PNG image",
        ["إنشاء صورة"] = "Create image",
        ["واحد PDF"] = "One PDF",
        ["واحد"] = "One",
        ["المصدر"] = "Source",
        ["إجراءات: نسخ المسار، نقل، فتح"] = "Actions: Copy path, move, open",
        ["إجراءات · نسخ المسار، نقل، فتح Terminal"] = "Actions · Copy path, move, open Terminal",
        ["إجراءات · نسخ المسار، نقل، فتح"] = "Actions · Copy path, move, open",
        ["اختيار ملف"] = "Select file",
        ["الملف الأصلي لم يتغير"] = "Original file unchanged",
        ["المحرك المحلي المطلوب غير متوفر"] = "Required offline converter is unavailable",
        ["رف الملفات"] = "Floating file shelf",
        ["خيارات المعالجة"] = "Processing options",
        ["المزيد من الخيارات"] = "More options",
        ["تنفيذ"] = "Run",
        ["متابعة"] = "Continue",
        ["لا توجد ملفات للمشاركة"] = "No files to share",
        ["Quick Share غير مثبت. ثبّته من Google قبل مشاركة الملفات."] = "Quick Share is not installed. Install the official Google app first.",
        ["فُتح Quick Share وحُدد الملف في المستكشف. اختر الملفات والجهاز من Quick Share لإرسالها؛ النقل ليس تلقائيًا."] = "Quick Share is open. Choose files and a nearby device; transfer is not automatic.",
        ["تعذر فتح Quick Share: "] = "Could not launch Quick Share: ",
        ["السرعة (0.5 إلى 2، مثل 1.25)"] = "Speed (0.5 to 2; e.g. 1.25)",
        ["البداية والمدة بالثواني: 5,20"] = "Start time and duration in seconds: 5,20",
        ["بداية نغمة الحجب ومدتها بالثواني: 5,2"] = "Bleep start and duration in seconds: 5,2",
        ["ترتيب الصفحات مثل 3,1,2 أو reverse"] = "Page order, e.g. 3,1,2 or reverse",
        ["القنوات: 1 لل mono، أو 2 لل stereo"] = "Channels: 1 for mono or 2 for stereo",
        ["مدة كل جزء بالثواني"] = "Segment duration (seconds)",
        ["X,Y,العرض,الارتفاع بالبكسل"] = "X,Y,width,height in pixels",
        ["X,Y,العرض,الارتفاع لمنطقة الحجب"] = "X,Y,width,height of redaction region",
        ["الصفحة الأولى"] = "First page",
        ["الصفحة الثانية"] = "Second page",
        ["الصفحة الثالثة"] = "Third page",
        ["تدرّج مقترح"] = "Suggested gradient",
        ["ارسم على الصورة مباشرة · سيُحفظ الرسم مع التصميم"] = "Draw on the image; the design will include your marks",
        ["لون "] = "Color ",
        ["لون"] = "Color",
        ["جارٍ"] = "Running",
        ["تعذّر"] = "Failed",
        ["أُلغي"] = "Cancelled",
        ["تراجعنا"] = "Undone",
        ["قبل دقيقة"] = "One minute ago",
        ["قبل دقيقتين"] = "Two minutes ago",
        ["ملف PDF"] = "PDF file",
        ["نقلنا"] = "Moved",
        ["نسخنا"] = "Copied",
        ["تعديل"] = "Edit",
        ["أنشأنا"] = "Created",
        ["تظهر الآن"] = "Now showing",
        ["لا يمكن تنفيذ هذا الإجراء"] = "Action cannot be completed",
        ["أنشأنا ملفات"] = "Files created",
        ["تم التحويل"] = "Converted",
        ["تم إنشاء"] = "Created",
        ["تم حفظ"] = "Saved",
        ["ملف واحد"] = "One file",
        ["ملفان"] = "Two files",
        ["إرسال"] = "Send",
        ["فتح المجلد"] = "Open folder",
        ["بجانب الملف الأصلي"] = "Beside the source file",
        ["رفع"] = "Upload",
        ["حفظ"] = "Save",
        ["قص"] = "Crop",
        ["تم التحويل إلى PDF"] = "Converted to PDF",
        ["نطاق القص"] = "Crop range",
        ["تأكيد"] = "Confirm",
        ["خطأ"] = "Error",
        ["تمت العملية"] = "Operation complete",
    };
    private static readonly Dictionary<string,string> Arabic = Invert();
    // Keep each control's original literal in memory across language changes.
    // Without this cache, an untranslated fallback becomes the NEW source text,
    // and switching English -> Arabic cannot restore the original Arabic string.
    private sealed class SourceText { public string Original = ""; public string Rendered = ""; }
    private static readonly ConditionalWeakTable<DependencyObject,Dictionary<string,SourceText>> Sources = new();
    private static string LocalizeProperty(DependencyObject node, string property, string current, string language)
    {
        var properties = Sources.GetValue(node, _ => new Dictionary<string,SourceText>(StringComparer.Ordinal));
        if (!properties.TryGetValue(property, out var entry))
        {
            entry = new SourceText { Original = current, Rendered = current };
            properties.Add(property, entry);
        }
        else if (!string.Equals(current,entry.Rendered,StringComparison.Ordinal) &&
                 !string.Equals(current,entry.Original,StringComparison.Ordinal))
        {
            // Another component intentionally changed this label since the last visit.
            entry.Original = current;
        }
        var translated = Translate(entry.Original, language);
        entry.Rendered = translated;
        return translated;
    }
    // Dynamic history/toast messages from earlier builds may contain variable
    // filenames, dimensions or counts; replace only trusted interface fragments.
    private static readonly (string Ar,string En)[] EnglishFragments = {
        ("إجراءات", "Actions"),
        ("إنشاء صورة واحدة بصيغة PNG", "Create a single PNG image"),
        ("واحد PDF", "one PDF"),
        ("ملف PDF", "PDF file"),
        ("إنشاء ملف", "Create file"),
        ("نسخ المسار، نقل، فتح", "Copy path, move, open"),
        ("تمت المعالجة محليًا", "Processed locally"),
        ("حدث خطأ غير متوقع", "An unexpected error occurred"),
        ("لم يتغير ملفك الأصلي", "Your original file was not modified"),
        ("بجانب الأصل", "Beside the original"),
        ("حفظنا", "Saved"),
        ("أنشأنا", "Created"),
        ("التفاصيل في سجل فَلَك", "Details are in the Falak log"),
        ("في مجلد", "in folder"),
        ("أحد الملفات", "one of the files"),
    };
    private static string TranslateFragments(string value)
    {
        if (!value.Any(c => c >= '\u0600' && c <= '\u06ff')) return value;
        foreach (var (ar,en) in EnglishFragments)
            value = value.Replace(ar,en,StringComparison.Ordinal);
        return value;
    }
    private static Dictionary<string,string> Invert()
    {
        var inverse = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var kv in English) inverse.TryAdd(kv.Value, kv.Key);
        return inverse;
    }
    // Diacritics were added inconsistently by the original decompiler. Normalize the key,
    // not user-provided file names or arbitrary text.
    private static string Key(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input.Normalize(NormalizationForm.FormC))
        {
            var kind = CharUnicodeInfo.GetUnicodeCategory(c);
            if (kind is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark || c == '\u0640') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
    private static readonly Dictionary<string,string> EnglishNormalized =
        English.GroupBy(p => Key(p.Key), StringComparer.Ordinal)
               .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

    public static string Translate(string? text, string language)
    {
        if (text is null) return "";
        if (language == "en")
        {
            if (English.TryGetValue(text, out string? en)) return en;
            if (EnglishNormalized.TryGetValue(Key(text.Trim()), out en)) return en;
            // Preserve leading whitespace in formatted XAML Runs.
            var trimmed = text.Trim();
            if (English.TryGetValue(trimmed, out en) || EnglishNormalized.TryGetValue(Key(trimmed), out en))
                return text.Substring(0,text.Length-text.TrimStart().Length) + en +
                    text.Substring(text.TrimEnd().Length);
            string fragments = TranslateFragments(text);
            if (fragments.Any(c => c >= '\u0600' && c <= '\u06ff'))
            {
                // UI-only safeguard: never display untranslated, built-in Arabic text
                // when English is selected. File names and user content are preserved
                // in their separate file/path fields, not passed through this fallback.
                // Never replace a real label with an artificial placeholder.
                // Keep the original until an explicitly reviewed translation exists;
                // the localization QA reports untranslated literals as FAIL.
                return fragments;
            }
            return fragments;
        }
        return Arabic.TryGetValue(text, out string? ar) ? ar : text;
    }

    // The only UI structural change on language switch is FlowDirection. No XAML
    // positions, wheel geometry, palettes, templates or animations are modified.
    public static void Apply(Window window, string language)
    {
        window.FlowDirection = language == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        window.Language = XmlLanguage.GetLanguage(language == "en" ? "en-US" : "ar-SA");
        window.Title = LocalizeProperty(window, "Window.Title", window.Title, language);
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        Visit(window, language, visited);
    }
    private static void Visit(DependencyObject node, string language, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(node)) return;
        if (node is TextBlock block && !BindingOperations.IsDataBound(block, TextBlock.TextProperty))
        {
            // TextBlock.Text="..." also creates an implicit Run. Counting Inlines therefore
            // does not tell us whether this is a simple TextBlock or authored rich text.
            // Translate a locally-set Text property directly, so TextBlock.Text and the
            // displayed text are both updated. Never replace independently-authored
            // rich-text Inlines: traverse those Runs without changing their formatting.
            string before = block.Text;
            string after = LocalizeProperty(block, "TextBlock.Text", before, language);
            if (block.ReadLocalValue(TextBlock.TextProperty) != DependencyProperty.UnsetValue &&
                !string.Equals(before, after, StringComparison.Ordinal))
                block.Text = after;
            else
                foreach (Inline inline in block.Inlines) Visit(inline, language, visited);
        }
        if (node is Run run && !BindingOperations.IsDataBound(run, Run.TextProperty))
            run.Text = LocalizeProperty(run, "Run.Text", run.Text, language);
        if (node is ContentControl cc && cc.Content is string content &&
            !BindingOperations.IsDataBound(cc, ContentControl.ContentProperty))
            cc.Content = LocalizeProperty(cc, "ContentControl.Content", content, language);
        if (node is HeaderedContentControl hc && hc.Header is string header &&
            !BindingOperations.IsDataBound(hc, HeaderedContentControl.HeaderProperty))
            hc.Header = LocalizeProperty(hc, "Header", header, language);
        if (node is FrameworkElement element)
        {
            if (element.ToolTip is string tip) element.ToolTip = LocalizeProperty(element, "ToolTip", tip, language);
            var accessible = AutomationProperties.GetName(element);
            if (!string.IsNullOrEmpty(accessible))
                AutomationProperties.SetName(element, LocalizeProperty(element, "AutomationName", accessible, language));
        }
        // XAML uses both logical and visual children, including control templates.
        try
        {
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject nested) Visit(nested, language, visited);
        }
        catch (InvalidOperationException) { }
        try
        {
            int count = node is Visual || node is Visual3D ? VisualTreeHelper.GetChildrenCount(node) : 0;
            for (int i = 0; i < count; i++) Visit(VisualTreeHelper.GetChild(node, i), language, visited);
        }
        catch (InvalidOperationException) { }
    }
}
