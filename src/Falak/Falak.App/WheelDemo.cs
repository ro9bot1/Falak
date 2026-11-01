using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Falak.Core;
using Falak.Interop;

namespace Falak.App;

internal static class WheelDemo
{
	public static void Run(bool dark)
	{
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Expected Obj, but got Unknown
		BrandPalette palette = BrandPalette.Build(dark, AccentChoice.ById("emerald"));
		Theme.Apply(palette);
		WheelOverlay overlay = new WheelOverlay(new WheelRenderer(palette, arabic: true, glass: true), (string _, IReadOnlyList<string> _) => true, Log.Write);
		overlay.Create();
		Rect workArea = SystemParameters.WorkArea;
		double dpiScaleX = VisualTreeHelper.GetDpi(Application.Current.MainWindow ?? new Window()).DpiScaleX;
		int x = (int)((workArea.Left + workArea.Width / 2.0) * dpiScaleX);
		int y = (int)((workArea.Top + workArea.Height / 2.0) * dpiScaleX);
		overlay.Show(WheelMode.Convert, x, y);
		overlay.SetFiles(new _003C_003Ez__ReadOnlySingleElementList<string>(Path.Combine(SampleFiles.Create(), "Images\\Falak_Image_01.png")));
		Log.Write("Wheel demo: tick sound " + (WheelSound.Tick() ? "played" : "failed") + ".");
		int step = 0;
		DispatcherTimer timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(700L)
		};
		timer.Tick += (object? _, EventArgs _) =>
		{
			int count = overlay.Menu.Slices.Count;
			if (step < count * 2)
			{
				overlay.OnHover(WheelHit.OfSlice(step % count));
			}
			else if (step == count * 2)
			{
				overlay.OnHover(WheelHit.None);
			}
			else
			{
				timer.Stop();
				overlay.Dispose();
				Application.Current.Shutdown();
			}
			step++;
		};
		timer.Start();
	}
}
