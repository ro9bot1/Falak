using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

internal static class Snapshot
{
	public static void Run(string folder)
	{
		Directory.CreateDirectory(folder);
		BrandPalette[] array = new BrandPalette[2]
		{
			BrandPalette.Light,
			BrandPalette.Dark
		};
		foreach (BrandPalette brandPalette in array)
		{
			Theme.Apply(brandPalette);
			string mode = (brandPalette.IsDark ? "dark" : "light");
			JobRunner jobRunner = SampleJobs();
			HomeWindow homeWindow = new HomeWindow(jobRunner, new Settings());
			homeWindow.Left = -20000.0;
			homeWindow.Top = 0.0;
			homeWindow.ShowActivated = false;
			homeWindow.Show();
			Save(homeWindow.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "home-" + mode + ".png"));
			homeWindow.Hide();
			HomeWindow homeWindow2 = new HomeWindow(new JobRunner(new FileEngines()), new Settings());
			homeWindow2.Left = -20000.0;
			homeWindow2.Top = 0.0;
			homeWindow2.ShowActivated = false;
			homeWindow2.Show();
			Save(homeWindow2.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "home-empty-" + mode + ".png"));
			homeWindow2.Hide();
			int num = 0;
			foreach (JobItem item2 in jobRunner.History)
			{
				ToastWindow toastWindow = new ToastWindow(item2);
				toastWindow.Left = -20000.0;
				toastWindow.Top = 0.0;
				toastWindow.Show();
				Save(Root(toastWindow), unmirror: false, Path.Combine(folder, $"toast-{++num}-{item2.State}-{mode}.png"));
				toastWindow.Close();
			}
			JobItem jobItem = Updater.NewJob("1.1.0");
			jobItem.Progress = 0.42;
			(string, JobItem)[] array2 = new (string, JobItem)[3]
			{
				("available", JobItem.Info(Updater.Spaced("تحديث متاح: فَلَك", "1.1.0"), "تحديثات تلقائية موق\u0651عة، وإصلاح فتح النافذة.", "تحديث الآن", () =>
				{
				})),
				("downloading", jobItem),
				("installed", JobItem.Info(Updater.Spaced("ح\u064fد\u0651ث فَلَك إلى", "1.1.0"), "تحديثات تلقائية موق\u0651عة، وإصلاح فتح النافذة."))
			};
			for (int num2 = 0; num2 < array2.Length; num2++)
			{
				(string, JobItem) tuple = array2[num2];
				string item = tuple.Item1;
				ToastWindow toastWindow2 = new ToastWindow(tuple.Item2);
				toastWindow2.Left = -20000.0;
				toastWindow2.Top = 0.0;
				toastWindow2.Show();
				Save(Root(toastWindow2), unmirror: false, Path.Combine(folder, $"toast-update-{item}-{mode}.png"));
				toastWindow2.Close();
			}
			SettingsWindow settingsWindow = new SettingsWindow(new Settings());
			settingsWindow.Left = -20000.0;
			settingsWindow.Top = 0.0;
			settingsWindow.Height = 1180.0;
			settingsWindow.ShowActivated = false;
			settingsWindow.Show();
			Save(settingsWindow.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "settings-" + mode + ".png"));
			settingsWindow.Hide();
			DesignerWindow designerWindow = new DesignerWindow(Path.Combine(SampleFiles.Create(), "Images\\Falak_Image_01.png"), new JobRunner(new FileEngines()))
			{
				Left = -20000.0,
				Top = 0.0,
				ShowActivated = false
			};
			designerWindow.Show();
			Pump();
			Save(designerWindow.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "designer-" + mode + ".png"));
			designerWindow.ShowDrawingSample();
			Pump();
			Save(designerWindow.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "designer-draw-" + mode + ".png"));
			File.WriteAllBytes(Path.Combine(folder, "designer-export-" + mode + ".png"), IconExport.Png(designerWindow.ExportForReview()));
			designerWindow.ShowCropSample();
			Pump();
			Save(designerWindow.Content as FrameworkElement, unmirror: true, Path.Combine(folder, "designer-crop-" + mode + ".png"));
			designerWindow.Hide();
			ColorPicker colorPicker = new ColorPicker();
			colorPicker.SetColor(new Rgb(14, 107, 83));
			colorPicker.SetDocumentColors(new _003C_003Ez__ReadOnlyArray<Rgb>(new Rgb[3]
			{
				new Rgb(200, 30, 40),
				new Rgb(20, 40, 110),
				new Rgb(210, 170, 70)
			}));
			Border border = new Border
			{
				Padding = new Thickness(16.0),
				CornerRadius = new CornerRadius(14.0),
				Child = colorPicker,
				Background = (Brush)Application.Current.Resources["PopupSurface"]
			};
			Window window = new Window();
			window.Content = border;
			window.SizeToContent = SizeToContent.WidthAndHeight;
			window.WindowStyle = WindowStyle.None;
			window.Left = -20000.0;
			window.Top = 0.0;
			window.ShowActivated = false;
			window.ShowInTaskbar = false;
			window.Show();
			Save(border, unmirror: false, Path.Combine(folder, "color-picker-" + mode + ".png"));
			window.Close();
			TrayMenuWindow trayMenuWindow = new TrayMenuWindow(paused: false);
			trayMenuWindow.Left = -20000.0;
			trayMenuWindow.Top = 0.0;
			trayMenuWindow.Show();
			Save(Root(trayMenuWindow), unmirror: false, Path.Combine(folder, "tray-menu-" + mode + ".png"));
			trayMenuWindow.Close();
			WheelRenderer renderer = new WheelRenderer(brandPalette, arabic: true, glass: true);
			Wheel("image-convert", WheelMode.Convert, FileKind.Image, ".heic", WheelHit.OfSlice(1));
			Wheel("pdf-convert", WheelMode.Convert, FileKind.Pdf, ".pdf", WheelHit.OfSlice(0));
			Wheel("nothing", WheelMode.Convert, FileKind.Slides, ".pptx", WheelHit.None);
			Wheel("video-tools", WheelMode.Tools, FileKind.Video, ".mov", WheelHit.OfSlice(0));
			Wheel("text-tools", WheelMode.Tools, FileKind.Text, ".txt", WheelHit.None);
			Wheel("actions", WheelMode.Actions, FileKind.Pdf, ".pdf", WheelHit.OfSlice(2));
			Wheel("pending", WheelMode.Convert, FileKind.Pending, "", WheelHit.None);
			Wheel("pdf-tools", WheelMode.Tools, FileKind.Pdf, ".pdf", WheelHit.OfSlice(0));
			WheelMenu wheelMenu = SliceCatalog.For(WheelMode.Convert, FileKind.Image, ".heic");
			File.WriteAllBytes(Path.Combine(folder, "wheel-hero-" + mode + ".png"), IconExport.Png(renderer.RenderBitmap(wheelMenu, WheelMode.Convert, WheelHit.OfSlice(0), new WheelGeometry(wheelMenu.Slices.Count, 3.0))));
			WheelMenu wheelMenu2 = SliceCatalog.For(WheelMode.Convert, FileKind.Mixed, null, null, 3);
			File.WriteAllBytes(Path.Combine(folder, "wheel-merge-" + mode + ".png"), IconExport.Png(renderer.RenderBitmap(wheelMenu2, WheelMode.Convert, WheelHit.OfSlice(0), new WheelGeometry(wheelMenu2.Slices.Count, 1.5))));
			foreach (AccentChoice item3 in AccentChoice.All.Skip(1))
			{
				WheelRenderer wheelRenderer = new WheelRenderer(BrandPalette.Build(brandPalette.IsDark, item3));
				WheelMenu wheelMenu3 = SliceCatalog.For(WheelMode.Convert, FileKind.Image, ".heic");
				File.WriteAllBytes(Path.Combine(folder, $"accent-{item3.Id}-{mode}.png"), IconExport.Png(wheelRenderer.RenderBitmap(wheelMenu3, WheelMode.Convert, WheelHit.OfSlice(0), new WheelGeometry(wheelMenu3.Slices.Count))));
			}
			void Wheel(string name, WheelMode wm, FileKind kind, string ext, WheelHit hit)
			{
				WheelMenu wheelMenu4 = SliceCatalog.For(wm, kind, ext);
				File.WriteAllBytes(Path.Combine(folder, $"wheel-{name}-{mode}.png"), IconExport.Png(renderer.RenderBitmap(wheelMenu4, wm, hit, new WheelGeometry(wheelMenu4.Slices.Count, 1.5))));
			}
		}
		File.WriteAllBytes(Path.Combine(folder, "icon-256.png"), IconExport.Png(Art.Mark(256)));
		File.WriteAllBytes(Path.Combine(folder, "icon-32.png"), IconExport.Png(Art.Mark(32)));
		File.WriteAllBytes(Path.Combine(folder, "icon-16.png"), IconExport.Png(Art.Mark(16)));
	}

	private static void Pump()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected Obj, but got Unknown
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected Obj, but got Unknown
		DispatcherFrame frame = new DispatcherFrame();
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(600L)
		};
		timer.Tick += (object? _, EventArgs _) =>
		{
			timer.Stop();
			frame.Continue = false;
		};
		timer.Start();
		Dispatcher.PushFrame(frame);
	}

	private static JobRunner SampleJobs()
	{
		JobRunner jobRunner = new JobRunner(new FileEngines());
		string text = "C:\\Users\\me\\Documents\\عقود";
		jobRunner.Track(SliceCatalog.Action("convert.jpg"), new _003C_003Ez__ReadOnlySingleElementList<string>(text + "\\عقد_الإيجار.pdf")).Complete(new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(text + "\\عقد_الإيجار - صفحات\\صفحة 01.jpg"), text + "\\عقد_الإيجار - صفحات", "أنشأنا 3 صور بدقة 200 نقطة/بوصة"));
		jobRunner.Track(SliceCatalog.Action("convert.mp4"), new _003C_003Ez__ReadOnlySingleElementList<string>("C:\\Users\\me\\Videos\\جولة_الموقع.mov")).Fail("ويندوز لا يملك الترميز اللازم لقراءة «جولة_الموقع.mov». إن كان الفيديو HEVC فثب\u0651ت «HEVC Video Extensions» من متجر مايكروسوفت.");
		jobRunner.Track(SliceCatalog.Action("tool.fix-encoding"), new _003C_003Ez__ReadOnlySingleElementList<string>("C:\\Users\\me\\Downloads\\ترجمة_المحاضرة.srt")).Complete(new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>("C:\\Users\\me\\Downloads\\ترجمة_المحاضرة - م\u064fصلح.srt"), null, "أصلحنا الترميز: كان Windows-1256 وصار UTF-8 وي\u064fقرأ في كل مكان"));
		jobRunner.Track(SliceCatalog.Action("tool.messaging"), new _003C_003Ez__ReadOnlySingleElementList<string>("C:\\Users\\me\\Videos\\افتتاح_المعرض.mp4")).Progress = 0.42;
		return jobRunner;
	}

	private static FrameworkElement? Root(Window window)
	{
		if (VisualTreeHelper.GetChildrenCount((DependencyObject)(object)window) <= 0)
		{
			return null;
		}
		return VisualTreeHelper.GetChild((DependencyObject)(object)window, 0) as FrameworkElement;
	}

	private static void Save(FrameworkElement? element, bool unmirror, string path)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		if (element != null)
		{
			element.UpdateLayout();
			int pixelWidth = (int)Math.Ceiling(element.ActualWidth * 1.5);
			int pixelHeight = (int)Math.Ceiling(element.ActualHeight * 1.5);
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 144.0, 144.0, PixelFormats.Pbgra32);
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle((Brush)Application.Current.Resources["Bg"], null, new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
			}
			renderTargetBitmap.Render(drawingVisual);
			renderTargetBitmap.Render(element);
			BitmapSource image = ((unmirror && element.FlowDirection == FlowDirection.RightToLeft) ? ((BitmapSource)new TransformedBitmap(renderTargetBitmap, new ScaleTransform(-1.0, 1.0))) : ((BitmapSource)renderTargetBitmap));
			File.WriteAllBytes(path, IconExport.Png(image));
		}
	}
}
