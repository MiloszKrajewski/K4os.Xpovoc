using System.Diagnostics;
using Amazon.SQS;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.Sqs.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class Move10KUsingSqsAdapter
{
	private const int SampleSize = 100_000;
	private readonly ITestOutputHelper _output;

	public Move10KUsingSqsAdapter(ITestOutputHelper output)
	{
		_output = output;
	}
	
	[Theory]
	[InlineData(16, 128)]
	[InlineData(32, 4)]
	[InlineData(16, 4)]
	[InlineData(8, 4)]
	[InlineData(1, 4)]
	public async Task NoMessagesAreLost(int sqsConcurrency, int jobConcurrency)
	{
		var loggerFactory = NullLoggerFactory.Instance;
		// var loggerFactory = new TestLoggerFactory(_output); 
		var queueFactory = new SqsQueueFactory(new AmazonSQSClient());
		var adapter = new SqsJobQueueAdapter(
			loggerFactory,
			queueFactory,
			new DefaultJobSerializer(),
			new SqsJobQueueAdapterConfig {
				QueueName = "mk-test-move10k-adapter",
				SqsConcurrency = sqsConcurrency,
				JobConcurrency = jobConcurrency,
			});

		var guids = Enumerable.Range(0, SampleSize).Select(_ => Guid.NewGuid()).ToList();
		var stopwatch = Stopwatch.StartNew();

		var push = SendAll(adapter, guids, stopwatch);
		var pull = PullAll(adapter, guids, stopwatch);

		await Task.WhenAll(push, pull);
	
		adapter.Dispose();
	}

	private async Task SendAll(IJobQueueAdapter queue, ICollection<Guid> guids, Stopwatch stopwatch)
	{
		var publishTasks = guids
			.Select(g => new FakeJob(g))
			.Select(j => queue.Publish(TimeSpan.Zero, j, CancellationToken.None));
		await Task.WhenAll(publishTasks);
		
		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		_output.WriteLine($"All messages sent {rate:0.0}/s");
	}

	private async Task PullAll(IJobQueueAdapter queue, ICollection<Guid> guids, Stopwatch stopwatch)
	{
		var left = guids.ToHashSet();
		var counter = left.Count;
		var done = new TaskCompletionSource<bool>();

		Task HandleOne(IJob job)
		{
			lock (left)
			{
				left.Remove((Guid) job.Payload!);
				if (--counter <= 0) done.TrySetResult(true);
			}

			return Task.CompletedTask;
		}

		var subscription = queue.Subscribe((_, j) => HandleOne(j));

		await done.Task;

		subscription.Dispose();
		
		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		_output.WriteLine($"All messages received {rate:0.0}/s");
	}
}

internal class FakeJob: IJob
{
	public Guid JobId { get; }
	public DateTime UtcTime { get; }
	public object? Payload { get; }
	public object? Context { get; }

	public FakeJob(Guid guid)
	{
		JobId = guid;
		UtcTime = DateTime.UtcNow;
		Payload = guid;
		Context = null;
	}
}
