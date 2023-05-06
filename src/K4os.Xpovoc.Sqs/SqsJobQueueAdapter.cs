using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using K4os.Async.Toys;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Sqs.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Sqs;

public class SqsJobQueueAdapter: IJobQueueAdapter
{
	protected ILogger Log { get; }
	
	private readonly IJobSerializer _serializer;
	private readonly ISqsJobQueueAdapterConfig _config;
	private readonly ISqsQueueFactory _factory;
	private readonly Task _ready;
	
	private ISqsQueue _queue;
	private TimeSpan _visibility;

	private IBatchBuilder<SendMessageBatchRequestEntry, SqsResult<SendMessageBatchResultEntry>>
		_batchSender;

	private IAliveKeeper<Message> _aliveKeeper;

	public SqsJobQueueAdapter(
		ILoggerFactory? loggerFactory,
		IAmazonSQS client,
		IJobSerializer serializer,
		ISqsJobQueueAdapterConfig config):
		this(
			loggerFactory,
			new SqsQueueFactory(client),
			serializer,
			config) { }

	public SqsJobQueueAdapter(
		ILoggerFactory? loggerFactory,
		ISqsQueueFactory queueFactory,
		IJobSerializer serializer,
		ISqsJobQueueAdapterConfig config)
	{
		Log = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;

		_factory = queueFactory;
		_config = config;
		_serializer = serializer;

		_queue = null!;
		_visibility = SqsConstants.DefaultVisibilityTimeout; // might not be true
		_batchSender = null!;
		_aliveKeeper = null!;

		_ready = Task.Run(Startup);
	}

	private async Task Startup()
	{
		_queue = await CreateQueue();
		var attributes = await _queue.GetAttributes();
		_visibility = TimeSpan.FromSeconds(attributes.VisibilityTimeout);
		_batchSender = CreateBatchSender();
		_aliveKeeper = CreateAliveKeeper();
	}

	private static string TextId => Guid.NewGuid().ToString("D");

	private async Task<ISqsQueue> CreateQueue()
	{
		var queueSettings = _config.SqsQueueSettings ?? new SqsQueueSettings();
		var queue = await _factory.Create(_config.QueueName, queueSettings);
		return queue;
	}

	private IAliveKeeper<Message> CreateAliveKeeper()
	{
		
		const int retryCount = 5;
		var retryInterval = TimeSpan.FromSeconds(1);
		var touchInterval = CalculateTouchInterval(
			_visibility, retryInterval, retryCount);
		var touchDelay = CalculateTouchDelay(
			_visibility, touchInterval, retryInterval, retryCount);

		var aliveKeeperSettings = new AliveKeeperSettings {
			DeleteBatchSize = SqsConstants.MaximumNumberOfMessages,
			TouchBatchSize = SqsConstants.MaximumNumberOfMessages,
			RetryLimit = retryCount,
			RetryInterval = retryInterval,
			TouchInterval = touchInterval,
			TouchBatchDelay = touchDelay,
		};

		var aliveKeeper = AliveKeeper.Create<Message>(
			TouchMany,
			DeleteMany,
			m => m.MessageId,
			aliveKeeperSettings,
			Log);

		return aliveKeeper;
	}

	private IBatchBuilder<SendMessageBatchRequestEntry, SqsResult<SendMessageBatchResultEntry>>
		CreateBatchSender()
	{
		var batchSenderSettings = new BatchBuilderSettings {
			BatchDelay = TimeSpan.Zero,
			BatchSize = SqsConstants.MaximumNumberOfMessages,
			Concurrency = 4,
		};

		return BatchBuilder
			.Create<string, SendMessageBatchRequestEntry, SqsResult<SendMessageBatchResultEntry>>(
				rq => rq.Id,
				rs => rs.Error?.Id ?? rs.Result?.Id ?? string.Empty,
				SendMany,
				batchSenderSettings,
				Log);
	}

	private static TimeSpan CalculateTouchInterval(
		TimeSpan visibilityTimeout, TimeSpan retryInterval, int retryCount)
	{
		var retryMargin = retryInterval.TotalSeconds * retryCount;
		var visibility = visibilityTimeout.TotalSeconds;
		var interval = Math.Min(visibility / 2, visibility - retryMargin).NotLessThan(1);
		return TimeSpan.FromSeconds(interval);
	}

	private static TimeSpan CalculateTouchDelay(
		TimeSpan visibilityTimeout,
		TimeSpan touchInterval,
		TimeSpan retryInterval,
		int retryCount)
	{
		var visibility = visibilityTimeout.TotalSeconds;
		var interval = touchInterval.TotalSeconds;
		var retryMargin = retryInterval.TotalSeconds * retryCount;
		var delta = (visibility - interval - retryMargin) / retryCount;
		return TimeSpan.FromSeconds(delta.NotLessThan(0).NotMoreThan(1));
	}

	private async Task<SqsResult<SendMessageBatchResultEntry>[]> SendMany(
		SendMessageBatchRequestEntry[] messages)
	{
		await _ready;
		var requests = messages.ToList();
		Log.LogDebug("Sending batch of {Count} messages", requests.Count);
		var responses = await _queue.Send(requests, CancellationToken.None);
		return responses.ToArray();
	}

	private async Task<Message[]> DeleteMany(Message[] messages)
	{
		await _ready;
		var map = messages.ToDictionary(m => m.MessageId);
		var requests = messages.Select(ToDeleteRequest).ToList();
		Log.LogDebug("Deleting batch of {Count} messages", requests.Count);
		if (requests.Count == 0) Debugger.Break();
		var response = await _queue.Delete(requests, CancellationToken.None);
		return response
			.SelectNotNull(r => r.Result?.Id)
			.SelectNotNull(id => map.TryGetOrDefault(id))
			.ToArray();
	}

	private async Task<Message[]> TouchMany(Message[] messages)
	{
		await _ready;
		var timeout = (int)_visibility.TotalSeconds;
		var map = messages.ToDictionary(m => m.MessageId);
		var requests = messages.Select(m => ToTouchRequest(m, timeout)).ToList();
		Log.LogDebug("Touching batch of {Count} messages", requests.Count);
		if (requests.Count == 0) Debugger.Break();
		var response = await _queue.Touch(requests, CancellationToken.None);
		return response
			.SelectNotNull(r => r.Result?.Id)
			.SelectNotNull(id => map.TryGetOrDefault(id))
			.ToArray();
	}

	private static DeleteMessageBatchRequestEntry ToDeleteRequest(
		Message message) =>
		new() {
			Id = message.MessageId,
			ReceiptHandle = message.ReceiptHandle,
		};

	private static ChangeMessageVisibilityBatchRequestEntry ToTouchRequest(
		Message message, int timeout) =>
		new() {
			Id = message.MessageId,
			ReceiptHandle = message.ReceiptHandle,
			VisibilityTimeout = timeout,
		};

	private static SendMessageBatchRequestEntry CreateSendOneRequest(
		TimeSpan delay, Guid jobId, DateTime scheduledFor, string? payload)
	{
		delay = delay.NotLessThan(TimeSpan.Zero).NotMoreThan(SqsConstants.MaximumDelay);

		return new SendMessageBatchRequestEntry {
			Id = TextId,
			DelaySeconds = (int)delay.TotalSeconds,
			MessageBody = payload ?? string.Empty,
			MessageAttributes = new Dictionary<string, MessageAttributeValue> {
				[SqsAttributes.JobId] = CreateAttribute(jobId),
				[SqsAttributes.ScheduledFor] = CreateAttribute(scheduledFor),
			},
		};
	}

	private SendMessageBatchRequestEntry CreateSendOneRequest(
		TimeSpan delay, IJob job) =>
		CreateSendOneRequest(delay, job.JobId, job.UtcTime, Serialize(job));

	private string? Serialize(IJob job) =>
		(job.Context as Message)?.Body ?? (
			job.Payload is null ? null : _serializer.Serialize(job.Payload)
		);

	private static MessageAttributeValue CreateAttribute(string text) =>
		new() { DataType = "String", StringValue = text };

	private static MessageAttributeValue CreateAttribute(Guid guid) =>
		CreateAttribute(guid.ToString("D"));

	private static MessageAttributeValue CreateAttribute(DateTime time) =>
		CreateAttribute(time.ToString("O"));

	public async Task Publish(TimeSpan delay, IJob job, CancellationToken token)
	{
		await _ready;

		var jobId = job.JobId;
		var request = CreateSendOneRequest(delay, job);
		var response = await _batchSender.Request(request);
		var error = response.Error;

		if (error is null)
			return;

		throw new ArgumentException(
			$"Failed to publish job {jobId}: {error.Message}",
			nameof(job));
	}

	public IDisposable Subscribe(Func<CancellationToken, IJob, Task> handler) =>
		Agent.Launch(c => LongPollLoop(c, handler, _config.Concurrency), Log);

	private async Task LongPollLoop(
		IAgentContext context,
		Func<CancellationToken, IJob, Task> handler,
		int concurrency)
	{
		await _ready;

		var token = context.Token;
		var semaphore = new SemaphoreSlim(concurrency.NotLessThan(1));

		while (!token.IsCancellationRequested)
		{
			await LongPoll(handler, semaphore, token);
		}
	}

	private async Task LongPoll(
		Func<CancellationToken, IJob, Task> handler,
		SemaphoreSlim semaphore,
		CancellationToken token)
	{
		var messages = (await _queue.Receive(token)).ToArray();
		if (messages.Length <= 0) return;

		foreach (var message in messages)
		{
			_aliveKeeper.Upkeep(message, token);
		}

		foreach (var message in messages)
		{
			await semaphore.WaitAsync(token);
			// every execution is in separate "thread" and is not awaited
			Task.Run(() => HandleMessage(message, handler, semaphore, token), token).Forget();
		}
	}

	private async Task HandleMessage(
		Message message,
		Func<CancellationToken, IJob, Task> handler,
		SemaphoreSlim semaphore,
		CancellationToken token)
	{
		try
		{
			token.ThrowIfCancellationRequested();
			var job = new SqsReceivedJob(message, _serializer);
			await handler(token, job);
			await _aliveKeeper.Delete(message, token);
		}
		catch (Exception e)
		{
			Log.LogError(e, "Failed to process message {MessageId}", message.MessageId);
		}
		finally
		{
			_aliveKeeper.Forget(message);
			semaphore.Release();
		}
	}

	public void Dispose()
	{
		_ready.Await(); // it is safe to finish async initialization
		_batchSender.Dispose();
		_aliveKeeper.Dispose();
	}
}
