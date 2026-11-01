using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Falak.Core;
using Falak.Engines;

namespace Falak.App;

public sealed class JobItem : INotifyPropertyChanged
{
	private double _progress;

	private bool _hasProgress;

	private JobState _state;

	private string _message = "";

	private JobResult? _result;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private string? _titleOverride;

	public WheelAction Action { get; }

	public IReadOnlyList<string> Inputs { get; }

	public DateTime StartedAt { get; }

	public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

	public Func<bool>? UndoOverride { get; set; }

	public TimeSpan Elapsed { get; private set; }

	public string FirstName => Path.GetFileName(Path.TrimEndingDirectorySeparator(Inputs[0]));

	public string InputsLabel
	{
		get
		{
			if (!UiText.IsArabic)
			{
				int others = Inputs.Count - 1;
				return others == 0 ? FirstName : $"{FirstName} and {others} more file{(others == 1 ? "" : "s")}";
			}
			int num = Inputs.Count - 1;
			if (num <= 10)
			{
				return num switch
				{
					0 => FirstName, 
					1 => FirstName + " وملف آخر", 
					2 => FirstName + " وملفان آخران", 
					_ => $"{FirstName} و{num} ملفات أخرى", 
				};
			}
			int value = num;
			return $"{FirstName} و{value} ملفا\u064b آخر";
		}
	}

	public string Badge
	{
		get
		{
			if (BadgeOverride != null)
			{
				return UiText.IsArabic ? BadgeOverride : UiText.Translate(BadgeOverride, "en");
			}
			string text = Path.GetExtension(Inputs[0]).TrimStart('.').ToUpperInvariant();
			int length = text.Length;
			if (length <= 0 || length > 4)
			{
				return UiText.IsArabic ? "ملف" : "File";
			}
			return text;
		}
	}

	public bool IsInfo => _titleOverride != null;

	private string? _actionText;
	public string? ActionText { get => UiText.IsArabic ? _actionText : UiText.Translate(_actionText, "en"); init => _actionText = value; }

	public Action? OnAction { get; init; }

	public bool HasAction => OnAction != null;

	public int LingerSeconds { get; init; } = 7;

	public string? BadgeOverride { get; init; }

	public Func<JobState, string>? TitleFor { get; init; }

	public string Title
	{
		get
		{
			if (!UiText.IsArabic && _titleOverride is null && TitleFor is null)
			{
				return _state switch
				{
					JobState.Running => Action.IsFormat ? $"Converting {InputsLabel} to {Action.LabelEn}" : $"{Action.LabelEn}: {InputsLabel}",
					JobState.Done => Action.IsFormat ? $"Converted to {Action.LabelEn}" : $"Done: {Action.LabelEn}",
					JobState.NothingChanged => "Nothing changed",
					JobState.Failed => "Operation failed",
					JobState.Cancelled => "Cancelled",
					_ => "Undone"
				};
			}
			string text = _titleOverride;
			if (text == null)
			{
				string text2 = TitleFor?.Invoke(_state);
				if (text2 == null)
				{
					text2 = _state switch
					{
						JobState.Running => Action.IsFormat ? ("نحو\u0651ل " + InputsLabel + "\u200f إلى " + (UiText.IsArabic ? Action.LabelAr : Action.LabelEn)) : ((UiText.IsArabic ? Action.LabelAr : Action.LabelEn) + ": " + InputsLabel + "\u200f"), 
						JobState.Done => Action.IsFormat ? ("تم التحويل إلى " + (UiText.IsArabic ? Action.LabelAr : Action.LabelEn)) : ("تم: " + (UiText.IsArabic ? Action.LabelAr : Action.LabelEn)), 
						JobState.NothingChanged => "لم يتغير شيء", 
						JobState.Failed => "تعذ\u0651ر إكمال العملية", 
						JobState.Cancelled => "ألغيت العملية", 
						_ => "تراجعنا عن العملية", 
					};
				}
				text = text2;
			}
			return UiText.IsArabic ? text : UiText.Translate(text, "en");
		}
	}

	public void RefreshLanguage()
	{
		foreach (string name in new[] { "Title", "Summary", "Message", "InputsLabel", "Badge", "StateText", "LocationText", "PercentText", "Meta" })
			OnChanged(name);
	}

	public string Summary
	{
		get
		{
			if (!Action.IsFormat)
			{
				return (UiText.IsArabic ? Action.LabelAr : Action.LabelEn) + " · " + InputsLabel + "\u200f";
			}
			return InputsLabel + (UiText.IsArabic ? "\u200f إلى " : " to ") + (UiText.IsArabic ? Action.LabelAr : Action.LabelEn);
		}
	}

	public string Message
	{
		get
		{
            string translated = UiText.Translate(_message, App.Host.Settings.Language);
            if (!UiText.IsArabic && translated.Any(c => c >= '\u0600' && c <= '\u06ff'))
            {
                // Engine result messages can be composed at runtime from Arabic plurals.
                // Display a truthful, fully-English result instead of mixed-language UI.
                if (_state == JobState.Done && _result != null)
                {
                    int count = _result.Outputs.Count;
                    return count == 0 ? "Operation completed locally." :
                        $"Created {count} output file{(count == 1 ? "" : "s")}. Original files remain unchanged.";
                }
                if (_state == JobState.Failed) return "Processing failed. See the Falak diagnostic log for details.";
                return "Processing locally. Original files remain unchanged.";
            }
            return translated;
		}
		private set
		{
			Set(ref _message, value, "Message");
			OnChanged("Meta");
		}
	}

	public double Progress
	{
		get
		{
			return _progress;
		}
		set
		{
			Set(ref _progress, value, "Progress");
			HasProgress = true;
			OnChanged("PercentText");
		}
	}

	public bool HasProgress
	{
		get
		{
			return _hasProgress;
		}
		private set
		{
			Set(ref _hasProgress, value, "HasProgress");
		}
	}

	public string PercentText
	{
		get
		{
			if (!HasProgress)
			{
				return "";
			}
			return $"{Math.Round(_progress * 100.0)}{(UiText.IsArabic ? "٪" : "%")}";
		}
	}

	public JobState State
	{
		get
		{
			return _state;
		}
		private set
		{
			Set(ref _state, value, "State");
			OnChanged("Title");
			OnChanged("IsRunning");
			OnChanged("IsFinished");
			OnChanged("CanUndo");
			OnChanged("HasOutput");
			OnChanged("StateText");
			OnChanged("LocationText");
		}
	}

	public bool IsRunning => _state == JobState.Running;

	public bool IsFinished => !IsRunning;

	public JobResult? Result => _result;

	public bool HasOutput
	{
		get
		{
			if (_state == JobState.Done)
			{
				return _result?.Primary != null;
			}
			return false;
		}
	}

	public bool CanUndo
	{
		get
		{
			if (_state == JobState.Done)
			{
				if (UndoOverride == null)
				{
					JobResult result = _result;
					if ((object)result != null)
					{
						IReadOnlyList<string> outputs = result.Outputs;
						if (outputs != null)
						{
							return outputs.Count > 0;
						}
					}
					return false;
				}
				return true;
			}
			return false;
		}
	}

	public string StateText => !UiText.IsArabic ? _state switch
	{
		JobState.Running => "Running",
		JobState.Done => "Done",
		JobState.NothingChanged => "Unchanged",
		JobState.Failed => "Failed",
		JobState.Cancelled => "Cancelled",
		_ => "Undone"
	} : _state switch
	{
		JobState.Running => "جار\u064d", 
		JobState.Done => "تم", 
		JobState.NothingChanged => "بلا تغيير", 
		JobState.Failed => "تعذ\u0651ر", 
		JobState.Cancelled => "أ\u064fلغي", 
		_ => "تراجعنا", 
	};

	public string LocationText
	{
		get
		{
			string text = _result?.Primary;
			if (text == null)
			{
				return "";
			}
			string folder = _result.Folder;
			if (folder != null)
			{
				return (UiText.IsArabic ? "في مجلد جديد: " : "In new folder: ") + Path.GetFileName(folder);
			}
			string directoryName = Path.GetDirectoryName(text);
			if (!string.Equals(directoryName, Path.GetDirectoryName(Inputs[0]), StringComparison.OrdinalIgnoreCase))
			{
				return (UiText.IsArabic ? "في مجلد " : "In folder ") + Path.GetFileName(directoryName);
			}
			return (UiText.IsArabic ? "بجانب الأصل في مجلد " : "Beside source in folder ") + Path.GetFileName(directoryName);
		}
	}

	public string WhenText => UiText.IsArabic ? Ago.Format(StartedAt) : StartedAt.ToString("g", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

	public string Meta
	{
		get
		{
			if (!string.IsNullOrEmpty(_message))
			{
				return Message + "  ·  " + WhenText;
			}
			return WhenText;
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public JobItem(WheelAction action, IReadOnlyList<string> inputs)
	{
		Action = action;
		Inputs = inputs;
		StartedAt = DateTime.Now;
	}

	public static JobItem Info(string title, string message, string? actionText = null, Action? onAction = null)
	{
		JobItem jobItem = new JobItem(new WheelAction("info", title, title), new _003C_003Ez__ReadOnlySingleElementList<string>("فَلَك"))
		{
			_titleOverride = title,
			ActionText = actionText,
			OnAction = onAction,
			LingerSeconds = ((onAction == null) ? 7 : 30)
		};
		jobItem.Complete(new JobResult(Array.Empty<string>(), null, message), succeededWithoutFiles: true);
		return jobItem;
	}

	public void Complete(JobResult result, bool succeededWithoutFiles = false)
	{
		Elapsed = _clock.Elapsed;
		_result = result;
		Message = result.Message;
		Progress = 1.0;
		State = ((result.Outputs.Count != 0 || UndoOverride != null || succeededWithoutFiles) ? JobState.Done : JobState.NothingChanged);
	}

	public void Fail(string message)
	{
		Elapsed = _clock.Elapsed;
		Message = message;
		State = JobState.Failed;
	}

	public void MarkCancelled()
	{
		Message = "لم ي\u064fحفظ أي ملف ناقص.";
		State = JobState.Cancelled;
	}

	public void MarkUndone()
	{
		Message = "ن\u064fقل الناتج إلى سلة المحذوفات.";
		State = JobState.Undone;
	}

	public void RefreshTime()
	{
		OnChanged("WhenText");
		OnChanged("Meta");
	}

	private void OnChanged([CallerMemberName] string? name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
	{
		if (!EqualityComparer<T>.Default.Equals(field, value))
		{
			field = value;
			OnChanged(name);
		}
	}
}
