using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Falak.Engines;

namespace Falak.App;

// Actual interactive WPF rectangle selection, using Falak's existing CropBox.
// The original file is only decoded; no image/video file is overwritten.
internal static class VisualRegionDialog
{
    public static string? Choose(string input, bool video)
    {
        string? temporary = null;
        try
        {
            string image = input;
            if (video)
            {
                string? ffmpeg = LocalToolEngine.Find("ffmpeg.exe");
                if (ffmpeg == null) throw new IOException("FFmpeg is not installed locally.");
                temporary = Path.Combine(Path.GetTempPath(), "FalakPreview_" + Guid.NewGuid().ToString("N") + ".png");
                var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true };
                foreach (var arg in new[] {"-hide_banner","-loglevel","error","-nostdin","-ss","0","-i",input,
                           "-frames:v","1","-update","1","-y",temporary}) psi.ArgumentList.Add(arg);
                using var proc = Process.Start(psi) ?? throw new IOException("FFmpeg could not start.");
                string errors = proc.StandardError.ReadToEnd();
                proc.StandardOutput.ReadToEnd();
                if (!proc.WaitForExit(20000)) { proc.Kill(entireProcessTree:true); throw new IOException("Video preview timed out."); }
                if (proc.ExitCode != 0 || !File.Exists(temporary)) throw new IOException("Unable to extract a video preview: " + errors);
                image = temporary;
            }
            var bitmap = Previews.LoadImage(image);
            if (bitmap == null) throw new IOException("Could not decode preview.");
            bool ar = UiText.IsArabic;
            var selector = new CropBox { Source = bitmap, Crop = new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight),
                MinWidth = 420, MinHeight = 300 };
            var win = new Window { Title = ar ? "فَلَك · تحديد المنطقة" : "Falak · Select region",
                Width=940, Height=720, MinWidth=540, MinHeight=420, WindowStartupLocation=WindowStartupLocation.CenterScreen,
                FlowDirection=ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                Background = new SolidColorBrush(Color.FromRgb(36,39,38)), Foreground=Brushes.White,
                ShowInTaskbar=true, Topmost=true };
            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var instruction = new TextBlock { Text = ar ? "اسحب الحواف لتحديد الجزء المطلوب ثم اضغط تطبيق" :
                "Drag the rectangle edges to select a region, then choose Apply", Margin = new Thickness(5,5,5,12) };
            Grid.SetRow(instruction,0); grid.Children.Add(instruction);
            Grid.SetRow(selector,1); grid.Children.Add(selector);
            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right,
                Margin = new Thickness(0,12,0,0) };
            var cancel = new Button { Content = ar ? "إلغاء" : "Cancel", Width=100, Height=38, Margin=new Thickness(6), IsCancel=true };
            var apply = new Button { Content = ar ? "تطبيق" : "Apply", Width=110, Height=38, Margin=new Thickness(6), IsDefault=true };
            cancel.Click += (_,_) => win.DialogResult = false;
            apply.Click += (_,_) => win.DialogResult = true;
            selector.Committed += () => win.DialogResult = true;
            controls.Children.Add(cancel); controls.Children.Add(apply);
            Grid.SetRow(controls,2);grid.Children.Add(controls);
            win.Content = grid;
            if (win.ShowDialog() != true) return null;
            Rect region = selector.Crop;
            int x = Math.Clamp((int)Math.Round(region.X), 0, Math.Max(0,bitmap.PixelWidth-1));
            int y = Math.Clamp((int)Math.Round(region.Y), 0, Math.Max(0,bitmap.PixelHeight-1));
            int width = Math.Clamp((int)Math.Round(region.Width),1,bitmap.PixelWidth-x);
            int height = Math.Clamp((int)Math.Round(region.Height),1,bitmap.PixelHeight-y);
            return FormattableString.Invariant($"{x},{y},{width},{height}");
        }
        finally { if (temporary != null) { try { File.Delete(temporary); } catch (IOException) {} } }
    }
}
