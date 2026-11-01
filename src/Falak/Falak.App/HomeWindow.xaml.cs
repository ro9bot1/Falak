using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Threading;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

public partial class HomeWindow : Window, IComponentConnector, IStyleConnector
{
	private readonly JobRunner _jobs;
    private CodeShareSession? _shareSession;
    private Border? _sharePanel;


	private readonly DispatcherTimer _clock = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(30L)
	};

	private bool _loading = true;

	private Func<string> _settingsAccent = () => "emerald";

	public HomeWindow(JobRunner jobs, Settings settings)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected Obj, but got Unknown
		HomeWindow homeWindow = this;
		InitializeComponent();
        ApplicationCredit.Text = ApplicationAttribution.Credit;
		_jobs = jobs;
		_settingsAccent = () => settings.Accent;
		SourceInitialized += (object? _, EventArgs _) =>
		{
			Backdrop.Apply(homeWindow, settings.Glass, Theme.Current.IsDark);
		};
		Mark.Source = Art.Mark(76);
		WheelPreview.Source = Art.SampleWheel(BrandPalette.From((!Theme.Current.IsDark) ? ThemePreference.Light : ThemePreference.Dark, AccentChoice.ById(settings.Accent), glass: false), arabic: settings.Language != "en");
		HistoryList.ItemsSource = jobs.History;
		jobs.History.CollectionChanged += (object? _, NotifyCollectionChangedEventArgs _) =>
		{
			homeWindow.UpdateEmptyState();
		};
		UpdateEmptyState();
		EnabledSwitch.IsChecked = !settings.Paused;
		UpdateStatus(!settings.Paused);
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		VersionText.Text = version != null && version.Build > 0 ? $"v{version.Major}.{version.Minor}.{version.Build}" : $"v{version?.Major}.{version?.Minor}";
		_clock.Tick += (object? _, EventArgs _) =>
		{
			foreach (JobItem item in jobs.History)
			{
				item.RefreshTime();
			}
		};
        _clock.Start();
        Closed += (_,_)=>StopCodeShare();
		LanguageSelector.SelectedIndex = settings.Language == "en" ? 1 : 0;
		Loaded += (_, _) => { UiText.Apply(this, settings.Language); RefreshShortcutCopy(); };
		_loading = false;
	}

    private void MacroLibrary_Click(object sender, RoutedEventArgs args) => App.Host.ShowMacros();

    private void CodeShare_Click(object sender, RoutedEventArgs args)
    {
        var dialog=new OpenFileDialog { Title=UiText.IsArabic?"اختر ملفات لنقلها إلى الهاتف":"Select files for Android",Multiselect=true };
        if(dialog.ShowDialog(this)==true) StartCodeShare(dialog.FileNames);
    }
    internal void StartCodeShare(IReadOnlyList<string> files)
    {
        _shareSession?.Dispose();_shareSession=null;
        if(_sharePanel!=null){Root.Children.Remove(_sharePanel);_sharePanel=null;}
        if(files.Count==0 || !files.Any(File.Exists))
        {
            MacroDialog.Notice(this, "Code Share", UiText.IsArabic?"اختر ملفاً أو أكثر للمشاركة":"Choose one or more files to share");
            return;
        }
        try { _shareSession=new CodeShareSession(files); }
        catch(Exception e) { MacroDialog.Notice(this, "Code Share", e.Message); return; }
        bool ar=UiText.IsArabic;
        var panel=new StackPanel { Margin=new Thickness(28) };
        panel.Children.Add(new TextBlock { Text="Code Share",FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,13) });
        panel.Children.Add(new TextBlock { Text=ar?"افتح هذا الرابط من متصفح Android على نفس شبكة Wi-Fi. يعمل النقل لمدة 10 دقائق فقط، ويمكنك إيقافه فورًا.":
            "On the same Wi-Fi, open this link in an Android browser. This read-only share expires in 10 minutes.",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14) });
        var url=new TextBox { Text=_shareSession.Url,IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight,
            FontSize=19,Height=42,VerticalContentAlignment=VerticalAlignment.Center };
        panel.Children.Add(url);
        panel.Children.Add(new TextBlock { Text=ar?"ملاحظة: هذا نقل محلي بواسطة المتصفح، وليس بروتوكول Quick Share الرسمي. الرابط غير مشفّر؛ شاركه فقط على شبكة موثوقة.":
            "This is browser-based local sharing, not Google's Quick Share protocol. The link is unencrypted: use trusted Wi-Fi only.",
            Foreground=Brushes.Goldenrod,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12) });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal };
        var copy=new Button { Content=ar?"نسخ الرابط":"Copy link",Width=120,Height=38,Margin=new Thickness(0,0,12,0) };
        copy.Click+=(_,_)=>{Clipboard.SetText(url.Text);};
        var close=new Button {Content=ar?"إيقاف المشاركة":"Stop sharing",Height=38,Width=145};
        close.Click+=(_,_)=>StopCodeShare();
        buttons.Children.Add(copy);buttons.Children.Add(close);panel.Children.Add(buttons);
        _sharePanel=new Border { Background=new SolidColorBrush(Color.FromRgb(38,42,40)),BorderBrush=Brushes.MediumSeaGreen,
            BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Width=710,MaxHeight=420,
            HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Child=panel,
            FlowDirection=ar?FlowDirection.RightToLeft:FlowDirection.LeftToRight };
        Grid.SetRowSpan(_sharePanel,4);Panel.SetZIndex(_sharePanel,500);
        Root.Children.Add(_sharePanel);
        // Closing the main window stops the local listener immediately.
    }
    private void StopCodeShare()
    {
        _shareSession?.Dispose(); _shareSession=null;
        if(_sharePanel!=null){Root.Children.Remove(_sharePanel);_sharePanel=null;}
    }
	private void UpdateEmptyState()
	{
		bool flag = _jobs.History.Count == 0;
		EmptyState.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		HistoryScroll.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
	}

	private void UpdateStatus(bool enabled)
	{
		StatusText.Text = UiText.Translate(enabled ? "يعمل في الخلفية" : "متوقف مؤقتا\u064b", App.Host.Settings.Language);
		StatusPill.SetResourceReference(Border.BackgroundProperty, enabled ? "AccentSoft" : "SurfaceRaised");
		StatusText.SetResourceReference(Control.ForegroundProperty, enabled ? "Accent" : "Muted");
		StatusDot.SetResourceReference(Shape.FillProperty, enabled ? "Accent" : "Muted");
		EnabledSwitch.Content = UiText.Translate(enabled ? "مفعّل" : "متوقف", App.Host.Settings.Language);
	}

	private void EnabledSwitch_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			bool valueOrDefault = EnabledSwitch.IsChecked == true;
			App.Host.SetPaused(!valueOrDefault);
			UpdateStatus(valueOrDefault);
		}
	}

	public void RefreshLanguage()
	{
		UpdateStatus(!App.Host.Settings.Paused);
		UiText.Apply(this, App.Host.Settings.Language);
        RefreshShortcutCopy();
		foreach (var job in _jobs.History) job.RefreshLanguage();
	}

    // These are individually-authored XAML Runs. Set both title and details explicitly
    // every time rather than translating a concatenated RTL/LTR sentence.
    private void RefreshShortcutCopy()
    {
        bool ar = App.Host.Settings.Language != "en";
        ShiftTitle.Text = ar ? "تحويل الصيغة" : "Convert formats";
        ShiftDetails.Text = ar ? " · صورة إلى PDF، وPDF إلى صور، وفيديو إلى MP3" :
                                 " · Image to PDF, PDF to images, video to MP3";
        CtrlTitle.Text = ar ? "أدوات" : "Tools";
        CtrlDetails.Text = ar ? " · ضغط، دمج، فصل، إصلاح الترميز" :
                                " · Compress, merge, split, fix encoding";
        MacroLibraryButton.Content = ar ? "التسجيلات" : "Recordings";
        SpaceTitle.Text = ar ? "إجراءات الملفات" : "File actions";
        SpaceDetails.Text = ar ? " · نسخ المسار، النقل، فتح الطرفية" :
                                 " · Copy path, move files, open Terminal";
    }

	private void LanguageSelector_Changed(object sender, SelectionChangedEventArgs e)
	{
		if (_loading) return;
		if (LanguageSelector.SelectedItem is ComboBoxItem item && item.Tag is string language)
			App.Host.SetLanguage(language);
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		App.Host.ShowSettings();
	}

	public void RefreshArt()
	{
		WheelPreview.Source = Art.SampleWheel(BrandPalette.From((!Theme.Current.IsDark) ? ThemePreference.Light : ThemePreference.Dark, AccentChoice.ById(_settingsAccent()), glass: false), arabic: App.Host.Settings.Language != "en");
	}

	private void TrySamples_Click(object sender, RoutedEventArgs e)
	{
		string text;
        try { text = SampleFiles.Create(); }
        catch (IOException ex)
        {
            MacroDialog.Notice(this, App.Host.Settings.Language == "en" ? "Falak samples" : "عينات فَلَك", ex.Message);
            return;
        }
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + text + "\"")
		{
			UseShellExecute = true
		});
	}

	private void RevealJob_Click(object sender, RoutedEventArgs e)
	{
		if (!((sender as FrameworkElement)?.Tag is JobItem jobItem))
		{
			return;
		}
		JobResult result = jobItem.Result;
		if ((object)result != null)
		{
			string primary = result.Primary;
			if (primary != null)
			{
				ShellServices.Reveal(primary);
			}
		}
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!App.Host.IsExiting)
		{
			e.Cancel = true;
			Hide();
			App.Host.NotifyStillRunningOnce();
		}
		base.OnClosing(e);
	}
}
