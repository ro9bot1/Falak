using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace Falak.App;

public sealed class ToastManager
{
	private const double ShadowOverlap = 18.0;

	private readonly List<ToastWindow> _open = new List<ToastWindow>();

	public void Show(JobItem job)
	{
		ToastWindow toastWindow = new ToastWindow(job);
		toastWindow.Dismissed += (ToastWindow t) =>
		{
			_open.Remove(t);
			Layout();
		};
		toastWindow.SizeChanged += (object _, SizeChangedEventArgs _) =>
		{
			Layout();
		};
		_open.Add(toastWindow);
		toastWindow.Show();
		Layout();
		while (_open.Count > 4)
		{
			_open[0].FadeOut();
		}
	}

	private void Layout()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		Rect workArea = SystemParameters.WorkArea;
		bool isRightToLeft = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
		double num = workArea.Bottom - 4.0;
		for (int num2 = _open.Count - 1; num2 >= 0; num2--)
		{
			ToastWindow toastWindow = _open[num2];
			double num3 = ((toastWindow.ActualHeight > 0.0) ? toastWindow.ActualHeight : 140.0);
			toastWindow.Left = (isRightToLeft ? (workArea.Left + 4.0) : (workArea.Right - toastWindow.Width - 4.0));
			toastWindow.Top = num - num3;
			num = toastWindow.Top + 18.0;
		}
	}
}
