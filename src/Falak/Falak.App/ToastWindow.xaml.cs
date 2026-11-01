using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

public partial class ToastWindow : Window, IComponentConnector
{
	private readonly JobItem _job;

	private readonly DispatcherTimer _autoClose;

	public event Action<ToastWindow>? Dismissed;

	public ToastWindow(JobItem job)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected Obj, but got Unknown
		ToastWindow toastWindow = this;
		InitializeComponent();
		Loaded += (_, _) => UiText.Apply(this, App.Host.Settings.Language);
		_job = job;
		DataContext = job;
		_autoClose = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(job.LingerSeconds)
		};
		_autoClose.Tick += (object? _, EventArgs _) =>
		{
			if (!toastWindow.IsMouseOver)
			{
				toastWindow.FadeOut();
			}
		};
		job.PropertyChanged += OnJobChanged;
		Loaded += (object _, RoutedEventArgs _) =>
		{
			toastWindow.ApplyState();
			toastWindow.FadeIn();
		};
		SourceInitialized += (object? _, EventArgs _) =>
		{
			toastWindow.NoActivate();
		};
		Closed += (object? _, EventArgs _) =>
		{
			job.PropertyChanged -= toastWindow.OnJobChanged;
		};
	}

	private void OnJobChanged(object? sender, PropertyChangedEventArgs e)
	{
		string propertyName = e.PropertyName;
		if ((propertyName == "State" || propertyName == "HasProgress") ? true : false)
		{
			ApplyState();
		}
	}

	private void ApplyState()
	{
		Bar.IsIndeterminate = !_job.HasProgress;
		string text;
		switch (_job.State)
		{
		case JobState.Done:
			text = "\ue73e";
			break;
		case JobState.Failed:
			text = "\ue783";
			break;
		case JobState.NothingChanged:
			text = "\ue946";
			break;
		case JobState.Cancelled:
		case JobState.Undone:
			text = "\ue7a7";
			break;
		default:
			text = null;
			break;
		}
		string text2 = text;
		StateGlyph.Text = text2 ?? "";
		StateGlyph.Visibility = ((text2 == null) ? Visibility.Collapsed : Visibility.Visible);
		BadgeText.Visibility = ((text2 != null) ? Visibility.Collapsed : Visibility.Visible);
		bool flag = _job.State == JobState.Failed;
		Tile.SetResourceReference(Control.BackgroundProperty, flag ? "DangerSoft" : "AccentSoft");
		StateGlyph.SetResourceReference(Control.ForegroundProperty, flag ? "Danger" : "Accent");
		LocationLine.Visibility = (string.IsNullOrEmpty(_job.LocationText) ? Visibility.Collapsed : Visibility.Visible);
		MessageText.Visibility = (string.IsNullOrEmpty(_job.Message) ? Visibility.Collapsed : Visibility.Visible);
		Button openButton = OpenButton;
		int visibility;
		if (_job.Result?.Folder == null)
		{
			JobResult? result = _job.Result;
			if ((object)result != null && result.Outputs.Count == 1)
			{
				visibility = 0;
				goto IL_0173;
			}
		}
		visibility = 2;
		goto IL_0173;
		IL_0173:
		openButton.Visibility = (Visibility)visibility;
		if (_job.IsFinished)
		{
			_autoClose.Start();
		}
	}

	private void NoActivate()
	{
		nint handle = new WindowInteropHelper(this).Handle;
		int num = NativeWindowStyles.Get(handle, -20);
		NativeWindowStyles.Set(handle, -20, num | 0x8000000 | 0x80);
	}

	private void FadeIn()
	{
		Opacity = 0.0;
		BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160L)));
	}

	public void FadeOut()
	{
		_autoClose.Stop();
		DoubleAnimation doubleAnimation = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(160L));
		doubleAnimation.Completed += (object? _, EventArgs _) =>
		{
			Close();
			Dismissed?.Invoke(this);
		};
		BeginAnimation(UIElement.OpacityProperty, doubleAnimation);
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		FadeOut();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		_job.Cancellation.Cancel();
	}

	private void Action_Click(object sender, RoutedEventArgs e)
	{
		FadeOut();
		_job.OnAction?.Invoke();
	}

	private void Reveal_Click(object sender, RoutedEventArgs e)
	{
		string text = _job.Result?.Primary;
		if (text != null)
		{
			ShellServices.Reveal(text);
		}
		FadeOut();
	}

	private void Open_Click(object sender, RoutedEventArgs e)
	{
		string text = _job.Result?.Primary;
		if (text != null && File.Exists(text))
		{
			ShellServices.Open(text);
		}
		FadeOut();
	}

	private void Undo_Click(object sender, RoutedEventArgs e)
	{
		App.Host.Jobs.Undo(_job);
		_autoClose.Stop();
		_autoClose.Start();
	}
}
