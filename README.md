<p align="center">
  <picture>
    <source srcset="https://github.com/user-attachments/assets/ccda124d-6cbd-47fd-b8b8-4a9c83ae1073" />
    <img src="https://github.com/user-attachments/assets/ccda124d-6cbd-47fd-b8b8-4a9c83ae1073" width="112" alt="Falak — فلك" />
  </picture>
</p>

# Falak | فَلَك

https://github.com/user-attachments/assets/911687ad-ea5a-4bab-9780-e97e73185daa

**File tools, one gesture away.**  
A Windows desktop toolkit for drag-and-drop file conversion, PDF and image tools, media processing, and workflow macros.

Falak began as a Windows project inspired by [Tangerine for macOS](https://tangerineformac.com/). It brings the drag-and-drop wheel workflow to Windows, with additional tools, Arabic and English interfaces, and workflow macros. Falak is an independent project, not an official Windows release of Tangerine.

[Releases](https://github.com/ro9bot1/Falak/releases) · [Report an issue](https://github.com/ro9bot1/Falak/issues)

> Found a problem or have a suggestion? [Open an issue](https://github.com/ro9bot1/Falak/issues) so I can review it and work on a fix.

### How it works

1. Launch Falak and leave it running in the background.
2. Start dragging a file from Windows File Explorer.
3. Press **Shift**, **Ctrl**, or **Space** while dragging to open the corresponding action wheel.
4. Move onto an available action and release the file. Some tools open a dialog for additional options.

| Key | Wheel | Examples |
| --- | --- | --- |
| **Shift** | Convert | Images, PDF, audio, video, documents, spreadsheets, and subtitles |
| **Ctrl** | Tools | Compress, crop, rotate, merge, split, clean text, and more |
| **Space** | Actions | Copy paths or files, copy or move to a folder, floating shelf, Quick Share, and saved macros |

Available actions depend on the file type, selection count, installed engines, and Windows capabilities.

### Features

- **Images:** conversion, compression, resizing, rotation, metadata tools, collage, and merging images into PDF.
- **Image designer:** crop, solid or gradient backgrounds, aspect-ratio presets, margins, rounded corners, shadows, pen, highlighter, and eraser. Export the design as a new image.
- **PDF:** merge, split, compression, page ordering, metadata, image and text exports, and exports to Word, Excel, EPUB, Markdown, or editable PowerPoint slides.
- **Audio and video:** format conversion, trimming, cropping, speed changes, muting, snapshots, volume adjustment, channel selection, waveform images, and redaction tools where supported.
- **Archives:** ZIP, TAR, GZIP, extraction, and optional RAR/7Z support.
- **Text and tables:** encoding repair, Arabic text cleanup, numeral conversion, CSV/TSV/Excel tools, and subtitle conversion.
- **Macros:** keyboard and mouse recording, editable step delays, repeat modes, Esc cancellation, and shortcuts scoped to individual applications.
- **Appearance:** Arabic and English interfaces, right-to-left and left-to-right layouts, light/dark/system themes, six accent colors, optional glass effects, and wheel sounds.
- **Local sharing:** Code Share creates a temporary browser download link for selected files on the local network. Google Quick Share is a separate integration.

### Download and run

Find the Windows x64 application and source ZIP in [Releases](https://github.com/ro9bot1/Falak/releases) when a release is available.

1. Download and extract a release archive, when available.
2. Run `Falak.exe`.
3. Use the bundled sample-file action to try tools before working with your own files.

The self-contained build includes the .NET runtime. Optional conversion programs are separate dependencies.

### Requirements and optional engines

- **Platform:** Windows x64. Some appearance and integration features depend on the Windows version.
- **FFmpeg:** advanced media tools and certain image formats.
- **Microsoft Office / LibreOffice / Pandoc:** particular document and presentation conversion paths, depending on the tool.
- **7-Zip:** RAR and 7Z extraction.
- **HEIC encoder:** a working compatible encoder is required for HEIC export.
- **Google Quick Share:** its Windows application is needed for the separate Quick Share workflow.

Optional engines are not all bundled in the standalone executable. Available actions depend on the engines installed on your computer.

### Feedback and attribution

**Contact:** [@RO9BOT2 on Telegram](https://t.me/RO9BOT2).

Please [open an issue](https://github.com/ro9bot1/Falak/issues) with your Windows version, Falak version, file type, steps to reproduce, and expected/actual behavior. Use a small non-sensitive sample when possible.

**Made by Robot.**

---

**أدوات ملفاتك، بحركة واحدة.**

فَلَك تطبيق لويندوز يجمع تحويل الملفات وأدوات الصور وPDF ومعالجة الصوت والفيديو وتسجيل الماكرو في واجهة تعمل مع السحب والإفلات.

بدأ فلك كمشروع لويندوز مستوحى من فكرة [Tangerine على macOS](https://tangerineformac.com/). ينقل تجربة السحب والإفلات والعجلة إلى ويندوز، مع أدوات إضافية، وواجهة بالعربية والإنجليزية، وتسجيل الماكرو. فلك مشروع مستقل، وليس إصدار ويندوز رسميًا من Tangerine.

### طريقة الاستخدام

1. شغّل فلك واتركه يعمل في الخلفية.
2. ابدأ سحب ملف من مستكشف ملفات ويندوز.
3. اضغط **Shift** أو **Ctrl** أو **Space** أثناء السحب لفتح عجلة الخيارات المناسبة.
4. تحرك إلى الأداة المتاحة ثم أفلت الملف. تفتح بعض الأدوات نافذة لإدخال خيارات إضافية.

| المفتاح | القائمة | أمثلة |
| --- | --- | --- |
| **Shift** | التحويل | الصور وPDF والصوت والفيديو والمستندات والجداول والترجمة النصية |
| **Ctrl** | الأدوات | الضغط والقص والتدوير والدمج والفصل وتنظيف النصوص |
| **Space** | الإجراءات | نسخ المسار أو الملف، والنسخ والنقل إلى مجلد، ورف الملفات، وQuick Share، والماكرو المحفوظ |

تختلف الخيارات حسب نوع الملف وعدد الملفات المحددة والمحركات المثبتة وإمكانات ويندوز.

### المميزات

- **الصور:** التحويل والضغط وتغيير الأبعاد والتدوير وأدوات البيانات الوصفية والكولاج ودمج الصور في PDF.
- **محرر التصميم:** القص والخلفيات الموحدة والمتدرجة ونسب المقاسات والهامش والزوايا والظل والقلم والتظليل والممحاة، مع حفظ التصميم كصورة جديدة.
- **PDF:** الدمج والفصل والضغط وترتيب الصفحات والبيانات الوصفية والتصدير إلى صور ونصوص وWord وExcel وEPUB وMarkdown وشرائح PowerPoint بنصوص قابلة للتحرير.
- **الصوت والفيديو:** التحويل والقص الزمني وقص الإطار والسرعة والكتم واللقطات وضبط الصوت والقنوات والموجة الصوتية والحجب، حسب الدعم المتاح.
- **الأرشيفات:** ZIP وTAR وGZIP وفك الضغط، مع دعم اختياري لـRAR و7Z.
- **النصوص والجداول:** إصلاح الترميز وتنظيف العربية وتحويل الأرقام وأدوات CSV وTSV وExcel وتحويل ملفات الترجمة النصية.
- **الماكرو:** تسجيل لوحة المفاتيح والفأرة وتعديل تأخير الخطوات والتكرار والإيقاف بـEsc، واختصارات مخصصة لكل تطبيق.
- **المظهر:** العربية والإنجليزية واتجاها الكتابة والمظهر الفاتح والداكن والنظام وستة ألوان وتأثيرات زجاجية وصوت للتنقل.
- **المشاركة المحلية:** ينشئ Code Share رابط تنزيل مؤقتًا للملفات المختارة عبر الشبكة المحلية. تكامل Google Quick Share خيار مستقل.

### التحميل والتشغيل

تجد ملف تشغيل Windows x64 وأرشيف المصدر ZIP في صفحة [الإصدارات](https://github.com/ro9bot1/Falak/releases) عند نشر إصدار. عند توفر إصدار، نزّل الأرشيف وفك الضغط وشغّل `Falak.exe`، ثم جرّب الأدوات على الملفات التجريبية المتاحة داخل التطبيق.

تتضمن النسخة المستقلة بيئة تشغيل .NET. أما البرامج المساعدة الاختيارية فتُثبت بصورة مستقلة.

### المتطلبات والبرامج المساعدة

يتطلب التطبيق ويندوز 64 بت. قد تحتاج بعض الأدوات إلى FFmpeg أو Microsoft Office أو LibreOffice أو Pandoc أو 7-Zip أو محرك HEIC متوافق. يتطلب مسار Quick Share تطبيق Google المخصص لويندوز.

> واجهت مشكلة أو لديك اقتراح؟ افتح [بلاغًا هنا](https://github.com/ro9bot1/Falak/issues) لأراجعه وأعمل على إصلاحه.

### الملاحظات والحقوق

**للتواصل عبر تيليجرام:** [@RO9BOT2](https://t.me/RO9BOT2).

للإبلاغ عن مشكلة، افتح [بلاغًا](https://github.com/ro9bot1/Falak/issues) يوضح إصدار ويندوز والتطبيق ونوع الملف وخطوات إعادة المشكلة والنتيجة المتوقعة والفعلية، مع عينة صغيرة غير حساسة إن أمكن.

**تم صنعه بواسطة روبوت.**
