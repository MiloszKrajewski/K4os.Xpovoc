using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.Sqs;
using K4os.Xpovoc.Sqs.Internal;
using Microsoft.Extensions.Logging;

namespace Playground;

public static class SqsThroughput
{

	private const int SampleSize = 100_000;
	private const string QueueName = "mk-test-move10k-adapter";

	public static async Task Run(ILoggerFactory loggerFactory)
	{
		var log = loggerFactory.CreateLogger("SqsThroughputTest");
		
		var client =
			new AmazonSQSClient(new AmazonSQSConfig { ServiceURL = "http://localhost:9324" });
		await Flush(client);

		var adapter = new SqsJobQueueAdapter(
			loggerFactory,
			client,
			new DefaultJobSerializer(),
			new SqsJobQueueAdapterSettings {
				QueueName = QueueName,
				PushConcurrency = 16,
				PullConcurrency = 4,
				JobConcurrency = 16,
			});

		var guids = Enumerable.Range(0, SampleSize).Select(_ => Guid.NewGuid()).ToList();

		var push = SendAll(guids, adapter, log);
		await push;

		var pull = PullAll(guids, adapter, log);
		await pull;

		await Task.WhenAll(push, pull);

		log.LogInformation("Disposing...");

		adapter.Dispose();

		log.LogInformation("Done");
	}

	public static async Task SendAll(ICollection<Guid> guids, IJobQueueAdapter queue, ILogger log)
	{
		var stopwatch = Stopwatch.StartNew();
		
		var publishTasks = guids
			.Select(g => new FakeJob(g))
			.Select(j => queue.Publish(TimeSpan.Zero, j, CancellationToken.None));
		await Task.WhenAll(publishTasks);

		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		log.LogInformation("All messages sent {Rate:0.0}/s", rate);
	}

	public static async Task PullAll(ICollection<Guid> guids, IJobQueueAdapter queue, ILogger log)
	{
		var left = guids.ToHashSet();
		var done = new TaskCompletionSource<bool>();

		Task HandleOne(IJob job)
		{
			lock (left)
			{
				left.Remove((Guid)job.Payload!);
				if (left.Count <= 0) done.TrySetResult(true);
			}

			return Task.CompletedTask;
		}
		
		var stopwatch = Stopwatch.StartNew();

		var subscription = queue.Subscribe((_, j) => HandleOne(j));

		await done.Task;

		subscription.Dispose();

		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		log.LogInformation("All messages received {Rate:0.0}/s", rate);
	}

	public static async Task Flush(IAmazonSQS amazonSqsClient)
	{
		var factory = new SqsQueueFactory(amazonSqsClient);
		var queue = await factory.Create(QueueName, new SqsQueueSettings());

		while (true)
		{
			var messages = await queue.Receive(CancellationToken.None);
			if (messages.Count <= 0) break;

			var deletes = messages
				.Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle))
				.ToList();
			await queue.Delete(deletes, CancellationToken.None);
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
}