using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

public partial class App : Application
{
	[Serializable]
	[CompilerGenerated]
	private sealed class _003C_003Ec
	{
		public static readonly _003C_003Ec _003C_003E9 = new _003C_003Ec();

		public static DispatcherUnhandledExceptionEventHandler _003C_003E9__37_1;

		internal void _003COnStartup_003Eb__37_1(object _, DispatcherUnhandledExceptionEventArgs ex)
		{
			Log.Write($"Unhandled: {ex.Exception}");
			ex.Handled = true;
		}
	}

	private const string InstanceName = "Local\\Falak.v1_3.Instance";

	private const string ShowSignalName = "Local\\Falak.v1_3.Show";

	private Mutex? _singleInstance;

	private EventWaitHandle? _showSignal;

	private GestureEngine? _gesture;

	private TrayIcon? _tray;

	private HomeWindow? _home;

    private MacroWindow? _macroWindow;
    internal MacroLibrary Macros { get; } = new MacroLibrary();
    private bool _macroRunning;
    private CancellationTokenSource? _macroPlaybackCancellation;
    internal bool IsMacroPlaying => _macroRunning;
    internal void CancelMacroPlayback() => _macroPlaybackCancellation?.Cancel();

	private bool _toldStillRunning;

	private bool _updating;

	private readonly DispatcherTimer _updateTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMinutes(1L)
	};

	public static App Host => (App)Application.Current;

	public Settings Settings { get; private set; } = new Settings();

	internal FileEngines Engines { get; } = new FileEngines();

	internal JobRunner Jobs { get; private set; }

	internal ToastManager Toasts { get; } = new ToastManager();

	internal bool IsExiting { get; private set; }

	internal UpdateManifest? AvailableUpdate { get; private set; }

	internal event Action? UpdateStateChanged;

	protected override void OnStartup(StartupEventArgs e)
	{
		RuntimeDiagnostics.Write("OnStartup entered. Args=" + string.Join(" ", e.Args));
		try
		{
			StartApplication(e);
		}
		catch (Exception ex)
		{
			RuntimeDiagnostics.Report("Startup fatal error", ex);
			ShowRecoveryWindow(ex);
		}
	}

	private void StartApplication(StartupEventArgs e)
	{
		base.OnStartup(e);
        try { ApplicationAttribution.Verify(); }
        catch (Exception ex)
        {
            RuntimeDiagnostics.Report("Attribution integrity failure", ex);
            MessageBox.Show("تعذّر التحقق من سلامة موارد الحقوق. أعد تثبيت نسخة أصلية.\nAttribution resources are damaged. Reinstall an original copy.", "Falak", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

		string[] args = e.Args;
		if (args.Length >= 2 && args[0] == "--qa-app-scroll")
        {
            AppScrollQaHarness.Run(args[1]);
            return;
        }
		if (args.Length >= 3 && args[0] == "--qa-pdf-pptx")
        {
            PdfQaHarness.Run(args[1], args[2]);
            return;
        }
        if (args.Length >= 2 && args[0] == "--qa-macro")
        {
            MacroQaHarness.Run(args[1]);
            return;
        }
        bool qaNative = args.Contains("--qa-native");
        bool qaMode = qaNative || args.Contains("--qa-ui-home") || args.Contains("--qa-ui-editor") || args.Contains("--qa-ui-video-region") || args.Contains("--qa-ui-video-trim");
        bool safeMode = args.Contains("--safe-mode") || Environment.GetEnvironmentVariable("FALAK_SAFE_MODE") == "1";
		RuntimeDiagnostics.Write(safeMode ? "Safe mode selected" : "Normal mode selected");
		if (args.Length >= 2 && args[0] == "--snapshot")
		{
			Snapshot.Run(args[1]);
			Shutdown();
			return;
		}
		if (args.Length >= 2 && args[0] == "--export-icon")
		{
			IconExport.Write(args[1]);
			Shutdown();
			return;
		}
		if (args.Length >= 1 && args[0] == "--wheel-demo")
		{
			WheelDemo.Run(args.Contains("--dark"));
			return;
		}
		bool afterUpdate = args.Contains("--after-update");
		string instanceName = qaMode ? "Local\\Falak.QA.Instance" : (safeMode ? "Local\\Falak.Dev.Safe.Instance" : InstanceName);
		_singleInstance = new Mutex(initiallyOwned: false, instanceName);
		bool entered;
		try
		{
			entered = _singleInstance.WaitOne(afterUpdate ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
		}
		catch (AbandonedMutexException)
		{
			entered = true;
		}
		if (!entered)
		{
			RuntimeDiagnostics.Write("Existing Falak instance detected. Stopping secondary instance.");
			if (!safeMode && EventWaitHandle.TryOpenExisting(ShowSignalName, out EventWaitHandle show))
			{
				show.Set();
				show.Dispose();
			}
			Shutdown();
			return;
		}
		if (!safeMode && !qaMode)
		{
			try { ListenForShowSignal(); }
			catch (Exception ex) { RuntimeDiagnostics.Report("Show-signal initialization failed", ex); }
		}
		RuntimeDiagnostics.Write("Loading settings");
		Settings = Falak.App.Settings.Load();
        Macros.Load();
        RefreshMacroWheel();
        if (!safeMode && !qaMode)
        {
            try { MacroHotkeys.Start(); }
            catch (Exception ex) { RuntimeDiagnostics.Report("Macro hotkey initialization failed (nonfatal)", ex); }
        }
		try
		{
			RuntimeDiagnostics.Write("Applying theme");
			Theme.Apply(Settings.Palette());
		}
		catch (Exception ex)
		{
			RuntimeDiagnostics.Report("Theme initialization failed, continuing with default", ex);
		}
		RuntimeDiagnostics.Write("Preparing conversion job runner");
		Jobs = new JobRunner(Engines);
		Jobs.Started += (JobItem job) => Toasts.Show(job);
		DispatcherUnhandledException += (_, err) =>
		{
			RuntimeDiagnostics.Report("Unhandled WPF UI exception", err.Exception);
			try
			{
				MessageBox.Show("Falak encountered an error. Diagnostic log:\n" + RuntimeDiagnostics.LogPath,
					"Falak - diagnostic", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			catch { /* Logging must remain independent of the UI. */ }
			err.Handled = true;
		};
		if (!safeMode && (!qaMode || qaNative))
		{
			try
			{
				RuntimeDiagnostics.Write("Starting Explorer gesture engine");
				_gesture = new GestureEngine(Dispatcher, GestureKeyMap.Default,
					Settings.Palette(forPopups: true), (id, paths) => id.StartsWith("macro.", StringComparison.Ordinal) || Engines.IsAvailable(id, paths), arabic: Settings.Language != "en", Settings.Glass);
				_gesture.SoundEnabled = Settings.WheelSound;
				_gesture.Log += Log.Write;
				_gesture.Dropped += OnDropped;
				_gesture.Start();
				_gesture.Paused = Settings.Paused;
			}
			catch (Exception ex)
			{
				RuntimeDiagnostics.Report("Gesture engine startup failed (nonfatal)", ex);
				try { MacroCapture.StopSilently();
        _gesture?.Dispose(); } catch { }
				_gesture = null;
			}
			try
			{
				RuntimeDiagnostics.Write("Creating tray icon");
				_tray = new TrayIcon(AppPaths.Icon, TrayTooltip());
				_tray.Activated += ShowHome;
				_tray.MenuRequested += ShowTrayMenu;
				RuntimeDiagnostics.Write(_tray.HasIcon ? "Tray icon ready" : "Tray icon not loaded");
			}
			catch (Exception ex)
			{
				RuntimeDiagnostics.Report("Tray initialization failed (nonfatal)", ex);
				try { _tray?.Dispose(); } catch { }
				_tray = null;
			}
			if (Settings.StartWithWindows)
			{
				try { StartupRegistration.Set(enabled: true); }
				catch (Exception ex) { RuntimeDiagnostics.Report("Startup registration failed", ex); }
			}
			// Legacy updater disabled.
			
			// Offline-first edition: no automatic update check or network requests.
		}
		RuntimeDiagnostics.Write("Preparing main window");
		if (safeMode || !args.Contains("--tray"))
		{
			ShowHome();
			RuntimeDiagnostics.Write("Home window Show() returned successfully");
            if (qaMode && args.Length >= 2 && File.Exists(args[1]))
            {
                var target = args[1];
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (args[0] == "--qa-ui-editor") ExtendedActions.ShowEditor(target, Jobs, startCrop: true);
                    else if (args[0] == "--qa-ui-video-trim")
                    {
                        string? range = ExtendedActions.TrimRangeDialog(target);
                        if (range != null) Jobs.Run(SliceCatalog.Action("tool.video-trim"), new[] { target }, range);
                    }
                    else
                    {
                        try
                        {
                            string? region = VisualRegionDialog.Choose(target, video: true);
                            if(region != null) Jobs.Run(SliceCatalog.Action("tool.video-crop"), new[]{target}, region);
                        }
                        catch(Exception ex) {RuntimeDiagnostics.Report("QA video region test failed",ex);}
                    }
                }), DispatcherPriority.ApplicationIdle);
            }
		}
		else
		{
			RuntimeDiagnostics.Write("Started in tray-only mode");
		}
	}

	private void ShowRecoveryWindow(Exception error)
	{
		try
		{
			var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
			panel.Children.Add(new System.Windows.Controls.TextBlock {
				Text = "Falak could not start normally.", FontSize = 19, FontWeight = FontWeights.Bold
			});
			panel.Children.Add(new System.Windows.Controls.TextBlock {
				Text = "A startup error was detected and saved. Please share the diagnostic ZIP, not private files.",
				TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
			});
			panel.Children.Add(new System.Windows.Controls.TextBlock {
				Text = error.GetType().FullName + ": " + error.Message,
				TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
			});
			panel.Children.Add(new System.Windows.Controls.TextBlock {
				Text = "Log: " + RuntimeDiagnostics.LogPath,
				TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0)
			});
			var fallback = new Window {
				Title = "Falak - startup recovery", Width = 600, Height = 300,
				WindowStartupLocation = WindowStartupLocation.CenterScreen,
				Content = panel
			};
			fallback.Closed += (_, _) => Shutdown(-1);
			fallback.Show();
		}
		catch (Exception fatal)
		{
			RuntimeDiagnostics.Report("Recovery window also failed", fatal);
			try { MessageBox.Show(error.ToString(), "Falak startup failure"); } catch { }
			Shutdown(-1);
		}
	}

	private void StartUpdateChecks()
	{
		_updateTimer.Tick += async (object? _, EventArgs _) =>
		{
			_updateTimer.Interval = TimeSpan.FromHours(3);
			if (!Settings.CheckUpdates)
			{
				return;
			}
			DateTime? lastUpdateCheck = Settings.LastUpdateCheck;
			if (lastUpdateCheck.HasValue)
			{
				DateTime valueOrDefault = lastUpdateCheck.GetValueOrDefault();
				if (DateTime.Now - valueOrDefault < TimeSpan.FromHours(20))
				{
					return;
				}
			}
			try
			{
				await CheckForUpdatesAsync(announce: true);
			}
			catch (Exception ex)
			{
				Log.Write("Update check failed: " + ex.Message);
			}
		};
		_updateTimer.Start();
	}

	internal async Task<UpdateManifest?> CheckForUpdatesAsync(bool announce)
	{
		try
		{
			UpdateManifest manifest = await Updater.CheckAsync();
			Settings.LastUpdateCheck = DateTime.Now;
			Settings.Save();
			AvailableUpdate = manifest;
			Log.Write(((object)manifest == null) ? "Update check: up to date." : ("Update check: " + manifest.Version + " available."));
			if (((object)manifest != null) & announce)
			{
				Toasts.Show(JobItem.Info(Updater.Spaced("تحديث متاح: فَلَك", manifest.Version), WhatsNewOf(manifest), "تحديث الآن", () =>
				{
					InstallUpdate(manifest);
				}));
			}
			return manifest;
		}
		finally
		{
			UpdateStateChanged?.Invoke();
		}
	}

	internal static string WhatsNewOf(UpdateManifest manifest)
	{
		IReadOnlyList<string> notes = manifest.Notes;
		if (notes == null || notes.Count <= 0)
		{
			return "تحسينات وإصلاحات.";
		}
		return notes[0];
	}

	internal async void InstallUpdate(UpdateManifest manifest)
	{
		if (_updating)
		{
			return;
		}
		_updating = true;
		JobItem job = Jobs.Track(Updater.NewJob(manifest.Version));
		Log.Write("Update to " + manifest.Version + " started.");
		try
		{
			string exe = await Updater.DownloadAsync(manifest, new Progress<double>((double p) =>
			{
				job.Progress = p;
			}), job.Cancellation.Token);
			if (Updater.CanReplaceInPlace)
			{
				job.Complete(new JobResult(Array.Empty<string>(), null, "تحققنا من النسخة الموق\u0651عة، وي\u064fعاد تشغيل فَلَك الآن."), succeededWithoutFiles: true);
				Settings.WhatsNew = WhatsNewOf(manifest);
				Settings.Save();
				await Task.Delay(1500);
				Updater.Install(exe);
				ExitApp();
			}
			else
			{
				job.Complete(new JobResult(new _003C_003Ez__ReadOnlySingleElementList<string>(exe), null, "تحققنا من النسخة. أغلق فَلَك ثم شغ\u0651ل الملف الجديد."));
			}
		}
		catch (OperationCanceledException)
		{
			job.MarkCancelled();
		}
		catch (UserFacingException ex2)
		{
			job.Fail(ex2.Message);
			Log.Write($"Update failed: {ex2.Message} ({ex2.InnerException?.Message})");
		}
		catch (Exception value)
		{
			job.Fail("تعذ\u0651ر تثبيت التحديث، وبقيت نسختك الحالية كما هي. التفاصيل في سجل فَلَك.");
			Log.Write($"Update crashed: {value}");
		}
		finally
		{
			_updating = false;
		}
	}

	private void ListenForShowSignal()
	{
		_showSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\Falak.v1_3.Show");
		Thread thread = new Thread(() =>
		{
			while (_showSignal.WaitOne() && !IsExiting)
			{
				((DispatcherObject)this).Dispatcher.BeginInvoke((Delegate)new Action(ShowHome), Array.Empty<object>());
			}
		});
		thread.IsBackground = true;
		thread.Name = "Falak.ShowSignal";
		thread.Start();
	}

	private string TrayTooltip()
	{
		if (!Settings.Paused)
		{
			return "فَلَك · اسحب ملفا\u064b واضغط Shift";
		}
		return "فَلَك · متوقف مؤقتا\u064b";
	}

	private void OnDropped(WheelDropResult result)
	{
		if ((object)result.Action != null)
		{
			IReadOnlyList<string> paths = result.Files.Paths;
			if (TryRunMacroAction(result.Action)) return;
            if (paths.Count == 0)
			{
				Toasts.Show(JobItem.Info("لا يمكن التعامل مع هذا العنصر", "هو ملف داخل برنامج آخر (مرفق بريد أو ملف داخل ZIP). احفظه في مجلد أولا\u064b ثم اسحبه."));
			}
			else if (result.Action.Id == "tool.background")
			{
				ExtendedActions.ShowEditor(paths[0], Jobs);
			}
			else if (ExtendedActions.TryRun(Jobs, result.Action, paths))
            {
                // Route crop/redact to their real interactive WPF selector before processing.
            }
            else if (FileEngines.IsAppAction(result.Action.Id))
			{
				AppActions.Run(Jobs, result.Action, paths);
			}
			else
			{
				Jobs.Run(result.Action, paths);
			}
		}
	}

    internal void RefreshMacroWheel()
    {
        SliceCatalog.SetMacroActions(Macros.All.Select(m => new WheelAction("macro." + m.Id, m.Name, m.Name)));
    }

    internal void ShowMacros()
    {
        if (_macroWindow == null)
        {
            _macroWindow = new MacroWindow(Macros);
            _macroWindow.Closed += (_, _) => _macroWindow = null;
        }
        _macroWindow.Show();
        if (_macroWindow.WindowState == WindowState.Minimized) _macroWindow.WindowState = WindowState.Normal;
        _macroWindow.Activate();
    }

    internal bool TryRunMacroAction(WheelAction action)
    {
        if (action.Id == "action.macros") { ShowMacros(); return true; }
        if (!action.Id.StartsWith("macro.", StringComparison.Ordinal)) return false;
        var macro = Macros.Find(action.Id.Substring("macro.".Length));
        if (macro == null) return true; // A stale or deleted wheel entry is safe to ignore.
        _ = PlayMacroAsync(macro);
        return true;
    }

    internal async Task PlayMacroAsync(MacroDefinition macro)
    {
        if (_macroRunning || MacroCapture.IsRecording) return;
        using var cancellation = new CancellationTokenSource();
        _macroPlaybackCancellation = cancellation;
        _macroRunning = true;
        if (_gesture != null) _gesture.Paused = true;
        try
        {
            // Let the Space wheel close before sending any simulated input.
            await Task.Delay(350, cancellation.Token);
            await MacroPlayback.PlayAsync(macro, cancellation.Token);
        }
        catch (OperationCanceledException) { /* Esc or Stop: normal completion */ }
        catch (Exception ex)
        {
            RuntimeDiagnostics.Report("Macro playback failed", ex);
            MacroDialog.Notice(_macroWindow, Settings.Language == "en" ? "Macro playback failed" : "تعذر تشغيل الماكرو", ex.Message);
        }
        finally
        {
            if (_gesture != null) _gesture.Paused = Settings.Paused;
            _macroRunning = false;
            if (ReferenceEquals(_macroPlaybackCancellation, cancellation))
                _macroPlaybackCancellation = null;
        }
    }

    internal void SetGestureSuspendedForCapture(bool suspended)
    {
        if (_gesture != null) _gesture.Paused = suspended || Settings.Paused;
    }

    internal void ShowCodeShare(IReadOnlyList<string> paths)
    {
        ShowHome();
        _home?.StartCodeShare(paths);
    }

	public void ShowHome()
	{
		if (_home == null)
		{
			_home = new HomeWindow(Jobs, Settings);
			_home.Closed += (object? _, EventArgs _) =>
			{
				_home = null;
			};
		}
		_home.Show();
		if (_home.WindowState == WindowState.Minimized)
		{
			_home.WindowState = WindowState.Normal;
		}
		_home.Activate();
		if (!Settings.Onboarded)
		{
			Settings.Onboarded = true;
			Settings.Save();
		}
	}

	private void ShowTrayMenu(int x, int y)
	{
		double num = 1.0 / ShellServices.ScaleAt(x, y);
		new TrayMenuWindow(Settings.Paused).ShowAt((double)x * num, (double)y * num);
	}

	public void ApplyAppearance()
	{
		Settings.Save();
		Theme.Apply(Settings.Palette());
		_gesture?.SetPalette(Settings.Palette(forPopups: true), Settings.Glass, Settings.Language != "en");
		foreach (Window window in Windows)
		{
			if ((window is HomeWindow || window is SettingsWindow || window is DesignerWindow) ? true : false)
			{
				Backdrop.Apply(window, Settings.Glass, Theme.Current.IsDark);
			}
		}
		_home?.RefreshArt();
	}

	public void SetLanguage(string language)
	{
		if (language != "ar" && language != "en") return;
		Settings.Language = language;
		Settings.Save();
		_gesture?.SetPalette(Settings.Palette(forPopups: true), Settings.Glass, language != "en");
		_home?.RefreshArt();
		_home?.RefreshLanguage();
		foreach (Window window in Windows)
		{
			if (window is SettingsWindow sw) sw.RefreshLanguage();
			else UiText.Apply(window, language);
		}
	}

	public void ShowSettings()
	{
		SettingsWindow settingsWindow = new SettingsWindow(Settings);
		settingsWindow.Owner = _home;
		settingsWindow.Show();
	}

	public void SetPaused(bool paused)
	{
		Settings.Paused = paused;
		Settings.Save();
		if (_gesture != null)
		{
			_gesture.Paused = paused;
		}
		_tray?.SetTooltip(TrayTooltip());
		Log.Write(paused ? "Paused by user." : "Resumed by user.");
	}

	public void SetWheelSound(bool enabled)
	{
		Settings.WheelSound = enabled;
		Settings.Save();
		if (_gesture != null)
		{
			_gesture.SoundEnabled = enabled;
		}
		if (enabled)
		{
			WheelSound.Tick();
		}
	}

	public void SetStartWithWindows(bool enabled)
	{
		Settings.StartWithWindows = enabled;
		Settings.Save();
		StartupRegistration.Set(enabled);
	}

	public void NotifyStillRunningOnce()
	{
		if (!_toldStillRunning)
		{
			_toldStillRunning = true;
			Toasts.Show(JobItem.Info("فَلَك ما زال يعمل", "تجده بجانب الساعة. اسحب أي ملف واضغط Shift متى شئت."));
		}
	}

	public void ExitApp()
	{
		IsExiting = true;
		_macroWindow?.Close();
        _home?.Close();
		Shutdown();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		IsExiting = true;
		_updateTimer.Stop();
		Log.Write($"Falak exiting (code {e.ApplicationExitCode}).");
        CancelMacroPlayback();
		MacroCapture.StopSilently();
        MacroHotkeys.Stop();
        _gesture?.Dispose();
		_tray?.Dispose();
		_showSignal?.Set();
		_showSignal?.Dispose();
		_singleInstance?.Dispose();
		base.OnExit(e);
	}

	public App()
	{
		RuntimeDiagnostics.Install();
		RuntimeDiagnostics.Write("App object constructed (WPF resources may load next)");
	}
}
