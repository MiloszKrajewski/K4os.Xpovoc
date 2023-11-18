using System.Diagnostics;
using Amazon.SQS;
using Amazon.SQS.Model;
using K4os.Xpovoc.Sqs.Internal;
using Xunit.Abstractions;

namespace K4os.Xpovoc.Sqs.Test;

public class Move10KUsingRawSqsAccess
{
	private const int SampleSize = 10_000;
	
	private readonly ITestOutputHelper _output;

	public Move10KUsingRawSqsAccess(ITestOutputHelper output)
	{
		_output = output;
	}
	
	[Fact]
	public async Task NoMessagesAreLost()
	{
		var factory = new SqsQueueFactory(new AmazonSQSClient());
		var settings = new SqsQueueSettings();
		var queue = await factory.Create("mk-test-move10k", settings);

		var guids = Enumerable.Range(0, SampleSize).Select(_ => Guid.NewGuid()).ToList();
		var stopwatch = Stopwatch.StartNew();

		var push = SendAll(queue, guids, stopwatch);
		var pull = PullAll(queue, guids, stopwatch);

		await Task.WhenAll(push, pull);
	}

	private async Task SendAll(ISqsQueue queue, ICollection<Guid> guids, Stopwatch stopwatch)
	{
		var semaphore = new SemaphoreSlim(10);
		var batches = guids
			.Select(g => g.ToString())
			.Select(g => new SendMessageBatchRequestEntry(g, g))
			.Chunk(10)
			.Select(b => b.ToList());

		await Task.WhenAll(batches.Select(b => SendBatch(queue, semaphore, b)));

		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		_output.WriteLine($"All messages sent {rate:0.0}/s");
	}

	private static async Task SendBatch(
		ISqsQueue queue, SemaphoreSlim semaphore, List<SendMessageBatchRequestEntry> batch)
	{
		await semaphore.WaitAsync();
		try
		{
			await queue.Send(batch, CancellationToken.None);
		}
		finally
		{
			semaphore.Release();
		}
	}

	private async Task PullAll(ISqsQueue queue, ICollection<Guid> guids, Stopwatch stopwatch)
	{
		var left = guids.ToHashSet();

		while (left.Count > 0)
		{
			var messages = await queue.Receive(CancellationToken.None);
			if (messages.Count == 0)
			{
				_output.WriteLine("No messages received");
				continue;
			}
			
			foreach (var message in messages)
			{
				var body = message.Body;
				
				if (!Guid.TryParse(body, out var guid))
				{
					_output.WriteLine($"Unexpected body: {body}");
					continue;
				}

				if (!left.Remove(guid))
				{
					_output.WriteLine($"Unexpected guid: {guid}");
				}
			}

			var receipts = messages
				.Select(m => new DeleteMessageBatchRequestEntry(m.MessageId, m.ReceiptHandle))
				.ToList();

			await queue.Delete(receipts, CancellationToken.None);
		}
		
		var rate = guids.Count / stopwatch.Elapsed.TotalSeconds;
		_output.WriteLine($"All messages received {rate:0.0}/s");
	}
}
