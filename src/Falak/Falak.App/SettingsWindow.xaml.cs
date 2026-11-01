using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

public partial class SettingsWindow : Window, IComponentConnector
{
	private readonly Settings _settings;

	private bool _loading = true;

	private bool _checking;

	public SettingsWindow(Settings settings)
	{
		InitializeComponent();
		_settings = settings;
		Mark.Source = Art.Mark(72);
		VersionText.Text = Updater.Spaced("فَلَك", Updater.CurrentText);
		string theme = settings.Theme;
		((theme == "light") ? ThemeLight : ((!(theme == "dark")) ? ThemeSystem : ThemeDark)).IsChecked = true;
		foreach (AccentChoice accent in AccentChoice.All)
		{
			RadioButton radioButton = new RadioButton
			{
				Style = (Style)FindResource("AccentDot"),
				Content = UiText.Translate(accent.NameAr, settings.Language),
				GroupName = "accent",
				Background = new SolidColorBrush(Theme.Current.IsDark ? accent.Dark : accent.Light),
				IsChecked = (accent.Id == settings.Accent),
				Tag = accent.Id
			};
			AutomationProperties.SetName((DependencyObject)(object)radioButton, UiText.Translate("لون ", settings.Language) + UiText.Translate(accent.NameAr, settings.Language));
			radioButton.Checked += (object _, RoutedEventArgs _) =>
			{
				if (!_loading)
				{
					_settings.Accent = accent.Id;
					App.Host.ApplyAppearance();
					RecolorDots();
				}
			};
			AccentPanel.Children.Add(radioButton);
		}
		GlassSwitch.IsChecked = settings.Glass;
		if (!Backdrop.Supported)
		{
			GlassNote.Text = "العجلة زجاجية، والنوافذ تحتاج Windows 11";
		}
		StartupSwitch.IsChecked = settings.StartWithWindows;
		SoundSwitch.IsChecked = settings.WheelSound;
		
		
		
		Closed += (object? _, EventArgs _) =>
		{
			
		};
		SourceInitialized += (object? _, EventArgs _) =>
		{
			Backdrop.Apply(this, _settings.Glass, Theme.Current.IsDark);
		};
		Loaded += (_, _) => UiText.Apply(this, App.Host.Settings.Language);
		_loading = false;
	}

	public void RefreshLanguage()
	{
		foreach (RadioButton dot in AccentPanel.Children.OfType<RadioButton>())
		{
			var accent = AccentChoice.ById(dot.Tag as string);
			dot.Content = UiText.Translate(accent.NameAr, _settings.Language);
			AutomationProperties.SetName(dot, UiText.Translate("لون ", _settings.Language) + UiText.Translate(accent.NameAr, _settings.Language));
		}
		
		UiText.Apply(this, _settings.Language);
	}







	private void RecolorDots()
	{
		foreach (RadioButton item in AccentPanel.Children.OfType<RadioButton>())
		{
			AccentChoice accentChoice = AccentChoice.ById(item.Tag as string);
			item.Background = new SolidColorBrush(Theme.Current.IsDark ? accentChoice.Dark : accentChoice.Light);
		}
	}

	private void Theme_Checked(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			Settings settings = _settings;
			string theme;
			if (ThemeLight.IsChecked == true)
			{
				theme = "light";
			}
			else
			{
				theme = ((ThemeDark.IsChecked == true) ? "dark" : "system");
			}
			settings.Theme = theme;
			App.Host.ApplyAppearance();
			RecolorDots();
		}
	}

	private void Glass_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			_settings.Glass = GlassSwitch.IsChecked == true;
			App.Host.ApplyAppearance();
		}
	}

	private void Sound_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			App.Host.SetWheelSound(SoundSwitch.IsChecked == true);
		}
	}

	private void Startup_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			App.Host.SetStartWithWindows(StartupSwitch.IsChecked == true);
		}
	}

	private void OpenLog_Click(object sender, RoutedEventArgs e)
	{
		Directory.CreateDirectory(AppPaths.Logs);
		Process.Start(new ProcessStartInfo("explorer.exe", "\"" + AppPaths.Logs + "\"")
		{
			UseShellExecute = true
		});
	}
}
