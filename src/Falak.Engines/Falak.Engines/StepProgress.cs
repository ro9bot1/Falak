using System;

namespace Falak.Engines;

internal sealed class StepProgress(IProgress<double>? sink, int steps)
{
	private int _done;

	public void Item(double fraction)
	{
		sink?.Report(Math.Clamp(((double)_done + fraction) / (double)Math.Max(1, steps), 0.0, 1.0));
	}

	public void Next()
	{
		sink?.Report(Math.Clamp((double)(++_done) / (double)Math.Max(1, steps), 0.0, 1.0));
	}
}
