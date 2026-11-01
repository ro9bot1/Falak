using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Falak.Core;
using Falak.Engines;
using Falak.Interop;

namespace Falak.App;

public sealed class JobRunner
{
	private const int HistoryLimit = 30;

	private readonly FileEngines _engines;

	public ObservableCollection<JobItem> History { get; } = new ObservableCollection<JobItem>();

	public event Action<JobItem>? Started;

	public JobRunner(FileEngines engines)
	{
		_engines = engines;
	}

	public async void Run(WheelAction action, IReadOnlyList<string> inputs, string? options = null)
	{
		JobItem job = Track(action, inputs);
		Log.Write($"Job {action.Id} on {inputs.Count} item(s) started.");
		try
		{
			Progress<double> progress = new Progress<double>((double p) =>
			{
				job.Progress = p;
			});
			JobResult jobResult = await _engines.RunAsync(new JobRequest(action.Id, inputs, options), progress, job.Cancellation.Token);
			job.Complete(jobResult);
			Log.Write($"Job {action.Id} done in {job.Elapsed.TotalSeconds:0.0} s: {jobResult.Outputs.Count} output(s).");
		}
		catch (OperationCanceledException)
		{
			job.MarkCancelled();
		}
		catch (UserFacingException ex2)
		{
			job.Fail(ex2.Message);
			Log.Write($"Job {action.Id} failed: {ex2.Message} ({ex2.InnerException?.Message})");
		}
		catch (Exception value)
		{
			job.Fail("حدث خطأ غير متوقع. لم يتغير ملفك الأصلي، والتفاصيل في سجل فَلَك.");
			Log.Write($"Job {action.Id} crashed: {value}");
		}
	}

	public JobItem Track(WheelAction action, IReadOnlyList<string> inputs)
	{
		return Track(new JobItem(action, inputs));
	}

	public JobItem Track(JobItem job)
	{
		History.Insert(0, job);
		while (History.Count > 30)
		{
			History.RemoveAt(History.Count - 1);
		}
		Started?.Invoke(job);
		return job;
	}

	public void Undo(JobItem job)
	{
		Func<bool> undoOverride = job.UndoOverride;
		bool flag;
		if (undoOverride != null)
		{
			flag = undoOverride();
		}
		else
		{
			JobResult result = job.Result;
			string folder = result.Folder;
			IEnumerable<string> paths;
			if (folder == null)
			{
				IEnumerable<string> outputs = result.Outputs;
				paths = outputs;
			}
			else
			{
				paths = result.Outputs.Where((string o) => !o.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).Append(folder);
			}
			flag = ShellServices.Recycle(paths);
		}
		if (flag)
		{
			job.MarkUndone();
		}
		Log.Write($"Undo {job.Action.Id}: {(flag ? "ok" : "failed")}.");
	}
}
