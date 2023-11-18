using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Sqs.Test.Utilities;

public class TestTimeSource: ITimeSource
{
	private readonly IScheduler _scheduler;

	public TestTimeSource(IScheduler scheduler) { _scheduler = scheduler; }

	public DateTimeOffset Now => _scheduler.Now;

	public Task Delay(TimeSpan delay, CancellationToken token)
	{
		var tcs = new TaskCompletionSource();
		var disposables = new CompositeDisposable();

		void Done()
		{
			var first = token.IsCancellationRequested
				? tcs.TrySetCanceled(token)
				: tcs.TrySetResult();
			if (first) disposables.Dispose();
		}

		disposables.Add(_scheduler.Schedule(Now.Add(delay), Done));
		disposables.Add(token.Register(Done));

		return tcs.Task;
	}
}
