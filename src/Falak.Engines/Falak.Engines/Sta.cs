using System;
using System.Threading;
using System.Threading.Tasks;

namespace Falak.Engines;

internal static class Sta
{
	public static Task<T> Run<T>(Func<T> body, CancellationToken ct = default(CancellationToken))
	{
		TaskCompletionSource<T> done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread(() =>
		{
			try
			{
				ct.ThrowIfCancellationRequested();
				done.SetResult(body());
			}
			catch (OperationCanceledException)
			{
				done.SetCanceled(ct);
			}
			catch (Exception exception)
			{
				done.SetException(exception);
			}
		});
		thread.IsBackground = true;
		thread.Name = "Falak.Sta";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return done.Task;
	}
}
