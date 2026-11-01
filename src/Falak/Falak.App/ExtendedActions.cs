using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Falak.Core;
using Falak.Engines;

namespace Falak.App;

// Only new dialogs; does NOT modify any pre-existing XAML/layout/wheel geometry.
internal static class ExtendedActions
{
    private static bool Ar => UiText.IsArabic;
    private static readonly Dictionary<string,(string Ar,string En,string Default)> Parameters = new()
    {
        ["tool.video-speed"] = ("السرعة (0.5 إلى 2، مثل 1.25)","Playback speed (0.5 to 2, e.g. 1.25)","1.25"),
        ["tool.video-trim"] = ("البداية والمدة بالثواني: 5,20","Start and duration in seconds: 5,20","0,30"),
        ["tool.audio-trim"] = ("البداية والمدة بالثواني: 5,20","Start and duration in seconds: 5,20","0,30"),
        ["tool.audio-bleep"] = ("بداية نغمة الحجب ومدتها بالثواني: 5,2","Bleep tone start and duration in seconds: 5,2","0,2"),
        ["tool.pdf-organize"] = ("ترتيب الصفحات مثل 3,1,2 أو reverse", "Page order e.g. 3,1,2 or reverse", "reverse"),
        ["tool.audio-channels"] = ("القنوات: 1 لل mono، أو 2 لل stereo","Channels: 1 mono or 2 stereo","2"),
        ["tool.video-split"] = ("مدة كل جزء بالثواني","Segment length in seconds","30"),
        ["tool.video-crop"] = ("X,Y,العرض,الارتفاع بالبكسل","X,Y,width,height in pixels","0,0,640,360"),
        ["tool.video-redact"] = ("X,Y,العرض,الارتفاع لمنطقة الحجب","X,Y,width,height to redact","0,0,120,80"),
        ["tool.image-redact"] = ("X,Y,العرض,الارتفاع لمنطقة الحجب","X,Y,width,height to redact","0,0,120,80"),
        ["tool.image-crop"] = ("X,Y,العرض,الارتفاع بالبكسل","X,Y,width,height in pixels","0,0,640,360")
    };
    public static bool TryRun(JobRunner jobs,WheelAction action,IReadOnlyList<string> paths)
    {
        if (action.Id.StartsWith("menu.more.",StringComparison.Ordinal))
        {
            string[] bits=action.Id.Split('.');
            if(bits.Length!=4 || !Enum.TryParse<WheelMode>(bits[2],out var mode) || !Enum.TryParse<FileKind>(bits[3],out var kind))return true;
            string? ext=paths.Select(Path.GetExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1?Path.GetExtension(paths[0]):null;
            var eng=new FileEngines();
            var extras=SliceCatalog.ExtraActions(mode,kind,ext,id => id.StartsWith("macro.",StringComparison.Ordinal) || eng.IsAvailable(id,paths),paths.Count);
            if(extras.Length==0) return true;
            var chosen=Pick(extras);
            if(chosen!=null) Dispatch(jobs,chosen,paths);
            return true;
        }
        if(action.Id == "tool.image-crop")
        {
            ShowEditor(paths[0],jobs,startCrop:true);
            return true;
        }
        if(action.Id is "tool.image-edit" or "tool.image-annotate")
        {
            ShowEditor(paths[0],jobs);
            return true;
        }
        if(action.Id=="action.float")
        {
            FloatShelf.ShowFiles(paths);
            return true;
        }
        if(action.Id=="action.share-android")
        {
            QuickShareIntegration.Open(jobs, action, paths);
            return true;
        }
        if(action.Id is "tool.video-crop" or "tool.video-redact" or "tool.image-redact")
        {
            try
            {
                var region = VisualRegionDialog.Choose(paths[0], action.Id.StartsWith("tool.video-",StringComparison.Ordinal));
                if (region != null) jobs.Run(action, paths, region);
            }
            catch (Exception ex)
            {
                MacroDialog.Notice(Application.Current.MainWindow, "Falak",
                    (Ar ? "تعذر عرض منطقة التحديد: " : "Could not open the region selector: ") + ex.Message);
            }
            return true;
        }
        if (action.Id is "tool.video-trim" or "tool.audio-trim")
        {
            string? range = TrimRangeDialog(paths[0]);
            if (range != null) jobs.Run(action, paths, range);
            return true;
        }
        if(Parameters.TryGetValue(action.Id,out var setting))
        {
            string? val=Input(setting);
            if(val!=null) jobs.Run(action,paths,val);
            return true;
        }
        return false;
    }
    internal static void ShowEditor(string path, JobRunner jobs, bool startCrop = false)
    {
        var editor = new DesignerWindow(path, jobs, startCrop)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true,
            Topmost = true
        };
        editor.Loaded += (_, _) =>
        {
            editor.Dispatcher.BeginInvoke(new Action(() => {
                editor.WindowState = WindowState.Normal;
                editor.Activate();
                editor.Focus();
                editor.Topmost = true; // keep tool window above Explorer and the native wheel
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        };
        editor.Show();
        editor.Activate();
    }
    private static void Dispatch(JobRunner jobs, WheelAction action, IReadOnlyList<string> paths)
    {
        if(App.Host.TryRunMacroAction(action)) return;
        if(!TryRun(jobs,action,paths))
        {
            if(FileEngines.IsAppAction(action.Id))AppActions.Run(jobs,action,paths);
            else jobs.Run(action,paths);
        }
    }
    private static Window Modal(string title,double width=380,double height=240)
    {
        var window = new Window
        {
            Title=title,Width=width,Height=height,SizeToContent=SizeToContent.Manual,
            WindowStartupLocation=WindowStartupLocation.CenterScreen,
            FlowDirection=Ar?FlowDirection.RightToLeft:FlowDirection.LeftToRight,
            ResizeMode=ResizeMode.CanResizeWithGrip, ShowInTaskbar=false, Topmost=true
        };
        // All utility modals match the rest of Falak and remain legible in Glass mode.
        window.SetResourceReference(Control.BackgroundProperty, "PopupSurface");
        window.SetResourceReference(Control.ForegroundProperty, "Ink");
        window.SetResourceReference(Control.FontFamilyProperty, "UiFont");
        return window;
    }
    private static WheelAction? Pick(WheelAction[] items)
    {
        var window=Modal(Ar?"المزيد من الخيارات":"More options",410,430);
        var layout=new DockPanel{Margin=new Thickness(15)};
        var list=new ListBox{Margin=new Thickness(0,0,0,10)};
        list.SetResourceReference(Control.ForegroundProperty, "Ink");
        list.SetResourceReference(Control.BackgroundProperty, "PopupSurface");
        list.SetResourceReference(Control.BorderBrushProperty, "PopupBorder");
        foreach(var item in items) list.Items.Add(new ListBoxItem{Content=Ar?item.LabelAr:item.LabelEn,Tag=item,Padding=new Thickness(8)});
        var ok=new Button{Content=Ar?"تنفيذ":"Run",Height=35,IsDefault=true,
            Style=Application.Current.TryFindResource("PrimaryButton") as Style};
        ok.Click+=(s,e)=>{if(list.SelectedItem!=null) window.DialogResult=true;};
        DockPanel.SetDock(ok,Dock.Bottom);layout.Children.Add(ok);layout.Children.Add(list);
        list.MouseDoubleClick+=(s,e)=>{if(list.SelectedItem!=null)window.DialogResult=true;};
        window.Content=layout;
        return window.ShowDialog()==true ? (list.SelectedItem as ListBoxItem)?.Tag as WheelAction : null;
    }
    // Two editable time fields (start -> end), with 0.1-second +/- controls.
    // The engine's internal contract is still start,DURATION, so do the
    // subtraction here. Users never have to calculate a duration themselves.
    internal static string? TrimRangeDialog(string source)
    {
        double clipLength = ProbeDuration(source);
        var window = Modal(Ar ? "قص الفترة الزمنية" : "Trim time range", 470, 345);
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock {
            Text = Ar ? "حدد من وإلى بالثواني. يمكنك كتابة 0.5 أو تغيير القيمة بالأزرار." :
                "Choose start and end in seconds. Type 0.5 or use the step buttons.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
        TextBox MakeField(string label, double value)
        {
            panel.Children.Add(new TextBlock { Text=label, Margin=new Thickness(0,4,0,5), FontSize=14 });
            var row = new StackPanel { Orientation=Orientation.Horizontal, FlowDirection=FlowDirection.LeftToRight };
            var box = new TextBox { Text=value.ToString("0.###",CultureInfo.InvariantCulture), Width=224,
                Height=38, FontSize=17, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center };
            var minus = new Button { Content="− 0.1", Width=77, Height=38, Margin=new Thickness(5,0,0,0),
                Style=Application.Current.TryFindResource("SecondaryButton") as Style };
            var plus = new Button { Content="+ 0.1", Width=77, Height=38, Margin=new Thickness(5,0,0,0),
                Style=Application.Current.TryFindResource("SecondaryButton") as Style };
            void Step(double delta)
            {
                if (!double.TryParse(box.Text.Replace('٫','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var current)) current=0;
                box.Text=Math.Max(0,Math.Round(current+delta,3)).ToString("0.###",CultureInfo.InvariantCulture);
                box.CaretIndex=box.Text.Length;
            }
            minus.Click += (_,_) => Step(-.1);
            plus.Click += (_,_) => Step(.1);
            box.PreviewKeyDown += (_,e) => {
                if(e.Key==Key.Up || e.Key==Key.Down) { Step(e.Key==Key.Up ? .1 : -.1); e.Handled=true; }
            };
            row.Children.Add(box); row.Children.Add(minus); row.Children.Add(plus);
            panel.Children.Add(row); return box;
        }
        var from = MakeField(Ar ? "من (ثانية)" : "From (seconds)",0);
        var to = MakeField(Ar ? "إلى (ثانية)" : "To (seconds)", Math.Round(clipLength>0?clipLength:10,3));
        var error = new TextBlock { Foreground=Brushes.OrangeRed, Margin=new Thickness(0,10,0,4), TextWrapping=TextWrapping.Wrap };
        panel.Children.Add(error);
        var execute = new Button { Content=Ar?"قص الفيديو / الصوت":"Trim selection", Height=40, IsDefault=true,
            Style=Application.Current.TryFindResource("PrimaryButton") as Style };
        double start=0,end=0;
        execute.Click += (_,_) => {
            bool ok1=double.TryParse(from.Text.Trim().Replace('٫','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out start);
            bool ok2=double.TryParse(to.Text.Trim().Replace('٫','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out end);
            if (!ok1 || !ok2 || !double.IsFinite(start) || !double.IsFinite(end) || start<0 || end<=start ||
                (clipLength>0 && end>clipLength+.01))
            {
                error.Text=Ar?"أدخل بداية ونهاية صحيحتين؛ النهاية أكبر من البداية ولا تتجاوز طول المقطع.":
                    "Enter valid times: end must be after start and within the media duration.";
                return;
            }
            window.DialogResult=true;
        };
        panel.Children.Add(execute); window.Content=panel;
        return window.ShowDialog()==true ? FormattableString.Invariant($"{start:0.###},{end-start:0.###}") : null;
    }
    private static double ProbeDuration(string input)
    {
        try
        {
            string? ffprobe=LocalToolEngine.Find("ffprobe.exe");
            if(ffprobe==null) return 0;
            var psi=new ProcessStartInfo(ffprobe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var arg in new[]{"-v","error","-show_entries","format=duration","-of","default=noprint_wrappers=1:nokey=1",input}) psi.ArgumentList.Add(arg);
            using var proc=Process.Start(psi);
            if(proc==null) return 0;
            var result=proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            if(!proc.WaitForExit(4000)){proc.Kill(entireProcessTree:true);return 0;}
            return double.TryParse(result.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)
                && double.IsFinite(seconds) && seconds>0 ? seconds:0;
        }
        catch { return 0; }
    }
    private static string? Input((string Ar,string En,string Default) s)
    {
        var w=Modal(Ar?"خيارات المعالجة":"Processing options");
        var panel=new StackPanel{Margin=new Thickness(20)};
        panel.Children.Add(new TextBlock{Text=Ar?s.Ar:s.En,TextWrapping=TextWrapping.Wrap,FontSize=15,Margin=new Thickness(0,0,0,12)});
        var input=new TextBox{Text=s.Default,Height=34,FontSize=15,Margin=new Thickness(0,0,0,20),FlowDirection=FlowDirection.LeftToRight};
        panel.Children.Add(input);
        var button=new Button{Content=Ar?"متابعة":"Continue",Height=35,IsDefault=true,
            Style=Application.Current.TryFindResource("PrimaryButton") as Style};
        button.Click+=(a,b)=>{if(!string.IsNullOrWhiteSpace(input.Text))w.DialogResult=true;};
        panel.Children.Add(button); w.Content=panel;
        return w.ShowDialog()==true?input.Text.Trim():null;
    }
}

internal sealed class FloatShelf:Window
{
    private static FloatShelf? _shelf;
    private readonly ListBox _files=new(){Foreground=Brushes.Black};
    private FloatShelf()
    {
        Title=UiText.IsArabic?"رف الملفات":"Floating file shelf";
        Width=320;Height=320;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Topmost=true;Background=new SolidColorBrush(Color.FromRgb(36,39,38));
        Content=_files;
        _files.PreviewMouseMove+=(s,e)=>{
            if(e.LeftButton!=MouseButtonState.Pressed || _files.SelectedItem is not string path)return;
            var collection=new StringCollection();collection.Add(path);
            var data=new DataObject();data.SetFileDropList(collection);
            DragDrop.DoDragDrop(_files,data,DragDropEffects.Copy);
        };
        Closed+=(s,e)=>{_shelf=null;};
    }
    public static void ShowFiles(IReadOnlyList<string> paths)
    {
        if(_shelf==null){_shelf=new FloatShelf();_shelf.Show();}
        foreach(var p in paths) if(!_shelf._files.Items.Contains(p))_shelf._files.Items.Add(p);
        _shelf.Activate();
    }
}
