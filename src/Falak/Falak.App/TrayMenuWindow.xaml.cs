using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;

namespace Falak.App;

public partial class TrayMenuWindow : Window, IComponentConnector
{
	private bool _closing;

	public TrayMenuWindow(bool paused)
	{
		InitializeComponent();
		Loaded += (_, _) => UiText.Apply(this, App.Host.Settings.Language);
		Mark.Source = Art.Mark(44);
		StateLine.Text = (paused ? "متوقف مؤقتا\u064b" : "يعمل");
		PauseText.Text = (paused ? "استئناف" : "إيقاف مؤقت");
		PauseGlyph.Text = (paused ? "\ue768" : "\ue769");
		Closing += (object? _, CancelEventArgs _) =>
		{
			_closing = true;
		};
		Deactivated += (object? _, EventArgs _) =>
		{
			SafeClose();
		};
	}

	public void ShowAt(double x, double y)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		Show();
		UpdateLayout();
		Rect workArea = SystemParameters.WorkArea;
		Left = Math.Clamp(x - ActualWidth / 2.0, workArea.Left, workArea.Right - ActualWidth);
		Top = Math.Clamp(y - ActualHeight, workArea.Top, workArea.Bottom - ActualHeight);
		Activate();
	}

	private void SafeClose()
	{
		if (!_closing)
		{
			_closing = true;
			Close();
		}
	}

	private void Open_Click(object sender, RoutedEventArgs e)
	{
		SafeClose();
		App.Host.ShowHome();
	}

	private void Pause_Click(object sender, RoutedEventArgs e)
	{
		SafeClose();
		App.Host.SetPaused(!App.Host.Settings.Paused);
	}

	private void Samples_Click(object sender, RoutedEventArgs e)
	{
		SafeClose();
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + SampleFiles.Create() + "\"")
		{
			UseShellExecute = true
		});
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		SafeClose();
		App.Host.ExitApp();
	}
}
