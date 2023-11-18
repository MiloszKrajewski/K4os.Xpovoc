using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using Amazon.SQS;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Json;
using K4os.Xpovoc.Sqs.Internal;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class SqsJobQueueAdapterTests
{
	private readonly ITestOutputHelper _output;
	private readonly TestLoggerFactory _loggerFactory;
	private static readonly AmazonSQSClient AmazonSqsClient = new();

	private static readonly JsonJobSerializer JsonJobSerializer = new(
		new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });

	private static readonly SqsJobQueueAdapterConfig DefaultAdapterConfig = new() {
		QueueName = "mk-SqsJobQueueAdapter-tests",
		JobConcurrency = 1,
	};

	private static readonly TestConfig FastRoundtripConfig = new() {
		QueueName = "mk-SqsJobQueueAdapter-5s-tests",
		JobConcurrency = 1,
		QueueSettings = new SqsQueueSettings {
			VisibilityTimeout = TimeSpan.FromSeconds(5),
		},
	};

	private static readonly TestConfig BatchingConfig = new() {
		QueueName = "mk-SqsJobQueueAdapter-10s-tests",
		JobConcurrency = 16,
		QueueSettings = new SqsQueueSettings {
			VisibilityTimeout = TimeSpan.FromSeconds(10),
		},
	};

	public SqsJobQueueAdapterTests(ITestOutputHelper output)
	{
		_output = output;
		_loggerFactory = new TestLoggerFactory(_output);
	}

	[Fact]
	public void AdapterCanBeInstantiatedAndDisposed()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			DefaultAdapterConfig);
		adapter.Dispose();
	}

	[Fact]
	public async Task CanPublishJob()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			DefaultAdapterConfig);

		await adapter.Publish(
			TimeSpan.Zero,
			new TestJob(DateTime.UtcNow, Guid.NewGuid()),
			CancellationToken.None);

		adapter.Dispose();
	}

	[Fact]
	public async Task CanSubscribe()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			DefaultAdapterConfig);

		var guid = Guid.NewGuid().ToString();
		_output.WriteLine("Expecting: {0}", guid);

		await adapter.Publish(
			TimeSpan.Zero,
			new TestJob(DateTime.UtcNow, guid),
			CancellationToken.None);

		var jobs = new ReplaySubject<IJob>();
		var subscription = Subscribe(adapter, jobs);
		var found = jobs
			.Do(j => _output.WriteLine("Received: {0}", j.Payload))
			.FirstOrDefaultAsync(j => guid.Equals(j.Payload))
			.ToTask();

		WaitForTask(found);

		subscription.Dispose();
		adapter.Dispose();
	}

	[Fact]
	public async Task CanDelayMessage()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			DefaultAdapterConfig);

		var guid = Guid.NewGuid().ToString();
		_output.WriteLine("Expecting: {0}", guid);

		var delay = TimeSpan.FromSeconds(5);
		var started = DateTime.UtcNow;

		await adapter.Publish(
			delay,
			new TestJob(DateTime.UtcNow.Add(delay), guid),
			CancellationToken.None);

		var jobs = new ReplaySubject<IJob>();
		var subscription = Subscribe(adapter, jobs);
		var found = jobs
			.Do(j => _output.WriteLine("Received: {0}", j.Payload))
			.FirstOrDefaultAsync(j => guid.Equals(j.Payload))
			.ToTask();

		WaitForTask(found);

		var finished = DateTime.UtcNow;
		var elapsed = finished - started;

		Assert.True(elapsed >= delay);

		subscription.Dispose();
		adapter.Dispose();
	}

	[Fact]
	public async Task HandledMessageIsKeptInvisible()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			FastRoundtripConfig);

		// make some artificial crowd
		for (var i = 0; i < 10; i++)
		{
			await adapter.Publish(
				TimeSpan.Zero,
				new TestJob(DateTime.UtcNow, Guid.NewGuid()),
				CancellationToken.None);
		}

		var guid = Guid.NewGuid().ToString();
		_output.WriteLine("Expecting: {0}", guid);

		await adapter.Publish(
			TimeSpan.Zero,
			new TestJob(DateTime.UtcNow, guid),
			CancellationToken.None);

		var done = new TaskCompletionSource<bool>();
		var flag = new[] { 0 };
		var subscription = adapter.Subscribe((_, m) => LongAction(flag, m, guid, done));

		WaitForTask(done.Task, TimeSpan.FromMinutes(1));

		subscription.Dispose();
		adapter.Dispose();
	}

	private async Task LongAction(
		int[] flag,
		IJob job,
		string guid,
		TaskCompletionSource<bool> done)
	{
		if (job.Payload as string != guid)
			return;

		if (Interlocked.CompareExchange(ref flag[0], 1, 0) != 0)
		{
			done.SetException(new InvalidOperationException("Already handled"));
			return;
		}

		for (var i = 0; i < 30; i++)
		{
			await Task.Delay(1000);
			_output.WriteLine($"Long action... {i}");
		}

		done.SetResult(true);
	}

	[Fact]
	public async Task InConcurrentSettingItUsesBatching()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			AmazonSqsClient,
			JsonJobSerializer,
			BatchingConfig);

		var expected = Enumerable
			.Range(0, 100)
			.Select(_ => Guid.NewGuid().ToString())
			.ToHashSet();
		var original = expected.ToHashSet(); // clone
		int concurrent = 0;
		var done = new TaskCompletionSource<bool>();

		var publishTasks = expected
			.Select(g => new TestJob(DateTime.UtcNow, g))
			.Select(j => adapter.Publish(TimeSpan.Zero, j, CancellationToken.None))
			.ToArray();

		await Task.WhenAll(publishTasks);

		async Task ActOnMessage(IJob job, int time)
		{
			var id = (string)job.Payload!;
			
			lock (expected)
			{
				if (!original.Contains(id)) return;
				if (!expected.Contains(id)) 
					throw new InvalidOperationException("Duplicate message");
			}
			
			var saturation = Interlocked.Increment(ref concurrent);
			_output.WriteLine($"Started {id} ({time}s left, {saturation} concurrent, ~{expected.Count} left)");
			
			await Task.Delay(TimeSpan.FromSeconds(time));

			lock (expected)
			{
				expected.Remove(id);
				if (expected.Count == 0) done.SetResult(true);
			}
			
			_output.WriteLine($"Done {id}");
			Interlocked.Decrement(ref concurrent);
		}

		var subscription = adapter.Subscribe((_, m) => ActOnMessage(m, 10/*Random.Shared.Next(8, 13)*/));
		
		// 100 messages, 8-12 seconds each, but 16 at the time = 100*12/16 = 75 seconds
		WaitForTask(done.Task, TimeSpan.FromSeconds(80));

		subscription.Dispose();
		adapter.Dispose();
	}

	public T WaitForTask<T>(Task<T> task, TimeSpan? timeout = null)
	{
		var wait = timeout ?? (
			Debugger.IsAttached ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(10)
		);
		var done = task.Wait(wait);
		if (!done) throw new TimeoutException();

		return task.GetAwaiter().GetResult();
	}

	private static IDisposable Subscribe(SqsJobQueueAdapter adapter, IObserver<IJob> jobs) =>
		adapter.Subscribe(
			(_, j) => {
				jobs.OnNext(j);
				return Task.CompletedTask;
			});
}
