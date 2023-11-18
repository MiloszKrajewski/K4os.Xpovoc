using System.Net;
using System.Reactive.Concurrency;
using Amazon.SQS;
using Amazon.SQS.Model;
using K4os.Xpovoc.Sqs.Internal;

namespace K4os.Xpovoc.Sqs.Test.Utilities;

internal class TestSqsQueue: ISqsQueue
{
	private readonly object _sync = new();
	private readonly IScheduler _scheduler;
	private readonly TimeSpan _visibility;
	private readonly Queue<FakeSqsMessage> _messages = new();
	private readonly SemaphoreSlim _messagesLock = new(0, int.MaxValue);
	private readonly Dictionary<string, FakeSqsMessage> _receipts = new();

	public TestSqsQueue(
		ISqsQueueSettings settings,
		IScheduler scheduler)
	{
		_scheduler = scheduler;
		_visibility = settings.VisibilityTimeout;
	}

	public int InQueue
	{
		get
		{
			lock (_sync) return _messages.Count;
		}
	}

	public int InFlight
	{
		get
		{
			lock (_sync) return _receipts.Count;
		}
	}

	public Task<GetQueueAttributesResponse> GetAttributes(CancellationToken token = default)
	{
		var visibilityTimeout = (int)Math.Ceiling(_visibility.TotalSeconds);

		var attributes = new GetQueueAttributesResponse {
			Attributes = {
				[QueueAttributeName.VisibilityTimeout] = visibilityTimeout.ToString(),
				// maybe more later
			},
			HttpStatusCode = HttpStatusCode.OK,
			ContentLength = 1337,
		};

		return Task.FromResult(attributes);
	}

	public Task<List<SqsResult<SendMessageBatchResultEntry>>> Send(
		List<SendMessageBatchRequestEntry> entries, CancellationToken token)
	{
		var now = _scheduler.Now;
		var results = new List<SqsResult<SendMessageBatchResultEntry>>();

		foreach (var entry in entries)
		{
			var messageId = Guid.NewGuid().ToString("D");
			var delay = entry.DelaySeconds.NotLessThan(0);
			results.Add(ToResultEntry(messageId, entry));

			if (delay > 0)
			{
				_scheduler.Schedule(now.AddSeconds(delay), () => EnqueueOne(messageId, entry));
			}
			else
			{
				EnqueueOne(messageId, entry);
			}
		}

		return Task.FromResult(results);
	}

	public async Task<List<Message>> Receive(CancellationToken token)
	{
		await _messagesLock.WaitAsync(token);

		var result = new List<Message>();
		while (result.Count < SqsConstants.MaximumReceiveCount)
		{
			var message = DequeueOne();
			if (message is null) break;

			result.Add(message);
		}

		return result;
	}

	public Task<List<SqsResult<DeleteMessageBatchResultEntry>>> Delete(
		List<DeleteMessageBatchRequestEntry> entries, CancellationToken token)
	{
		var result = new List<SqsResult<DeleteMessageBatchResultEntry>>();
		foreach (var entry in entries)
		{
			DeleteOne(entry.ReceiptHandle);
			result.Add(ToResultEntry(entry));
		}

		return Task.FromResult(result);
	}

	private void DeleteOne(string receiptId)
	{
		lock (_sync)
		{
			_receipts.Remove(receiptId);
		}
	}

	public Task<List<SqsResult<ChangeMessageVisibilityBatchResultEntry>>> Touch(
		List<ChangeMessageVisibilityBatchRequestEntry> entries, CancellationToken token)
	{
		var result = new List<SqsResult<ChangeMessageVisibilityBatchResultEntry>>();
		foreach (var entry in entries)
		{
			TouchOne(entry.ReceiptHandle, entry.VisibilityTimeout);
			result.Add(ToResultEntry(entry));
		}

		return Task.FromResult(result);
	}

	private void TouchOne(string receiptId, int visibilityTimeout)
	{
		lock (_sync)
		{
			if (!_receipts.TryGetValue(receiptId, out var message)) return;

			var invisibleUntil = _scheduler.Now.AddSeconds(visibilityTimeout);
			message.InvisibleUntil = invisibleUntil;
			_scheduler.Schedule(invisibleUntil, () => TryRequeueOne(receiptId));
		}
	}

	private void EnqueueOne(string messageId, SendMessageBatchRequestEntry entry)
	{
		lock (_sync)
		{
			_messages.Enqueue(ToMessage(messageId, entry));
			_messagesLock.Release();
		}
	}

	private void TryRequeueOne(string receiptId)
	{
		lock (_sync)
		{
			if (!_receipts.TryGetValue(receiptId, out var message)) return;

			var now = _scheduler.Now;
			if (message.InvisibleUntil > now) return;

			message.InvisibleUntil = null;
			_receipts.Remove(receiptId);
			_messages.Enqueue(message);
			_messagesLock.Release();
		}
	}

	private Message? DequeueOne()
	{
		var now = _scheduler.Now;

		lock (_sync)
		{
			if (!_messages.TryDequeue(out var message)) return null;

			if (message.InvisibleUntil.HasValue)
				throw new InvalidOperationException("Message is already in flight");

			var invisibleUntil = now.Add(_visibility);
			message.InvisibleUntil = invisibleUntil;
			var receiptId = Guid.NewGuid().ToString("D");
			_receipts.Add(receiptId, message);
			_scheduler.Schedule(invisibleUntil, () => TryRequeueOne(receiptId));

			return ToMessage(receiptId, message);
		}
	}

	private static FakeSqsMessage ToMessage(string messageId, SendMessageBatchRequestEntry entry) =>
		new() {
			Id = messageId,
			Body = entry.MessageBody,
			Attributes = entry.MessageAttributes.ToDictionary(x => x.Key, x => x.Value.StringValue),
		};

	private static Message ToMessage(string receiptId, FakeSqsMessage entry)
	{
		return new Message {
			MessageId = entry.Id,
			ReceiptHandle = receiptId,
			Body = entry.Body,
			MessageAttributes = entry.Attributes.ToDictionary(
				x => x.Key, x => new MessageAttributeValue {
					DataType = "String", StringValue = x.Value,
				}),
		};
	}

	private static SqsResult<SendMessageBatchResultEntry> ToResultEntry(
		string messageId, SendMessageBatchRequestEntry entry) =>
		new(new SendMessageBatchResultEntry { Id = entry.Id, MessageId = messageId });

	private static SqsResult<DeleteMessageBatchResultEntry> ToResultEntry(
		DeleteMessageBatchRequestEntry entry) =>
		new(new DeleteMessageBatchResultEntry { Id = entry.Id });

	private static SqsResult<ChangeMessageVisibilityBatchResultEntry> ToResultEntry(
		ChangeMessageVisibilityBatchRequestEntry entry) =>
		new(new ChangeMessageVisibilityBatchResultEntry { Id = entry.Id });
}

internal class FakeSqsMessage
{
	public string Id { get; set; } = null!;
	public string Body { get; set; } = null!;
	public Dictionary<string, string> Attributes { get; set; } = null!;
	public DateTimeOffset? InvisibleUntil { get; set; }
}
