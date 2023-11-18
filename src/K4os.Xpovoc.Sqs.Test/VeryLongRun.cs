using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.Sqs.Internal;
using K4os.Xpovoc.Sqs.Test.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Reactive.Testing;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class VeryLongRun: IJobHandler
{
	protected readonly ILogger Log;

	private static readonly DateTimeOffset Time0 =
		new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

	private readonly TestScheduler _scheduler;
	private readonly QueueJobScheduler _jobScheduler;
	private readonly TestSqsQueue _testQueue;
	private readonly List<(DateTimeOffset, Guid)> _jobsHandled = new();
	
	public Func<object, Task>? HijackHandler = null;

	public VeryLongRun(ITestOutputHelper output)
	{
		var loggerFactory = new TestLoggerFactory(output);
		_scheduler = new TestScheduler();
		AdvanceTo(Time0);
		var queueName = "test-queue";
		var queueFactory = new TestSqsQueueFactory(_scheduler);
		_testQueue = queueFactory.Create(queueName, new SqsQueueSettings());
		var testTimeSource = new TestTimeSource(_scheduler);

		var adapter = new SqsJobQueueAdapter(
			loggerFactory,
			queueFactory,
			new DefaultJobSerializer(),
			new SqsJobQueueAdapterConfig { QueueName = queueName, JobConcurrency = 1 },
			testTimeSource);
		_jobScheduler = new QueueJobScheduler(
			loggerFactory, adapter, this, testTimeSource);

		Log = loggerFactory.CreateLogger("Test");
	}

	~VeryLongRun() { _jobScheduler.Dispose(); }

	private void AdvanceTo(DateTimeOffset time) { _scheduler.AdvanceTo(time.Ticks); }
	private void AdvanceTo(TimeSpan span) { AdvanceTo(Time0.Add(span)); }
	private void AdvanceBy(TimeSpan span) { AdvanceTo(Now.Add(span)); }

	private static async Task<bool> WaitUntil(
		Func<bool> condition,
		int milliseconds = 1000)
	{
		if (milliseconds <= 0)
		{
			await Task.Yield();
			return condition();
		}

		if (condition()) return true;

		using var token = new CancellationTokenSource(milliseconds);

		while (true)
		{
			try
			{
				await Task.Delay(10, token.Token);
			}
			catch (OperationCanceledException)
			{
				return false;
			}

			if (condition()) return true;
		}
	}

	private DateTimeOffset Now => _scheduler.Now;

	private IEnumerable<(DateTimeOffset At, Guid Id)> HandledJob
	{
		get
		{
			lock (_jobsHandled) return _jobsHandled.ToArray();
		}
	}

	private int HandledCount
	{
		get
		{
			lock (_jobsHandled) return _jobsHandled.Count;
		}
	}

	[Fact]
	public async Task MessagesScheduledInThePastAreHandledImmediately()
	{
		var guid = Guid.NewGuid();
		await _jobScheduler.Schedule(Now.AddSeconds(-1), guid);
		await WaitUntil(() => HandledCount > 0);
		Assert.Equal(1, HandledCount);
		Assert.True(HandledJob.Any(x => x.Id == guid));
	}

	[Fact]
	public async Task MessagesScheduledForNowAreHandledImmediately()
	{
		var guid = Guid.NewGuid();
		await _jobScheduler.Schedule(Now, guid);
		await WaitUntil(() => HandledCount > 0);
		Assert.Equal(1, HandledCount);
		Assert.True(HandledJob.Any(x => x.Id == guid));
	}

	[Fact]
	public async Task MessagesScheduledInTheFutureDoNoGetHandledPrematurely()
	{
		var guid = Guid.NewGuid();
		var when = Now.AddDays(1);
		await _jobScheduler.Schedule(when, guid);
		Assert.False(await WaitUntil(() => HandledCount > 0));

		while (true)
		{
			var next = Now.AddMinutes(7);
			if (next >= when) break;

			AdvanceTo(next);
			// every 7 minutes we make sure job is still not handled
			// NOTE: we cannot just jump ahead because underlying mechanism
			// will assume that message wasn't touched for a long time and
			// will put it back to the queue effectively duplicating it
			// this is how real SQS works!
			// This is not great but it is a price of not using Rx
			// for scheduling "Touch" operations
			Assert.False(await WaitUntil(() => HandledCount > 0, 100));
			Assert.True(await WaitUntil(() => _testQueue.InQueue + _testQueue.InFlight <= 1));
		}

		Assert.False(await WaitUntil(() => HandledCount > 0));
		AdvanceTo(when);
		Assert.True(await WaitUntil(() => _testQueue.InQueue + _testQueue.InFlight == 0));
		Assert.Equal(1, HandledCount);
	}

	Task IJobHandler.Handle(CancellationToken token, object payload)
	{
		if (HijackHandler != null)
			return HijackHandler(payload);

		var job = (Guid)payload;
		var now = _scheduler.Now;
		lock (_jobsHandled)
			_jobsHandled.Add((now, job));
		return Task.CompletedTask;
	}
}
