using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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
using ITimeAdapter = K4os.Async.Toys.ITimeSource;
using ITimeSource = K4os.Xpovoc.Abstractions.ITimeSource;

namespace K4os.Xpovoc.Sqs;

using SqsBatchPublisher =
	IBatchBuilder<SendMessageBatchRequestEntry, SqsResult<SendMessageBatchResultEntry>>;
using SqsBatchSubscriber = BatchSubscriber<Message, string>;

public class SqsJobQueueAdapter: IJobQueueAdapter
{
	private const int DefaultSqsConcurrency = 16;
	private const int DefaultJobConcurrency = 4;

	private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan MinimumRetryInterval = TimeSpan.Zero;
	private const int DefaultRetryCount = 0;

	protected ILogger Log { get; }

	private static readonly SqsQueueSettings DefaultSqsQueueSettings = new();

	private readonly IJobSerializer _serializer;
	private readonly ISqsJobQueueAdapterConfig _config;
	private readonly ISqsQueueFactory _factory;
	private readonly Task _ready;

	private ISqsQueue _queue;
	private TimeSpan _visibility;

	private readonly object _mutex = new();
	private long _messageId;

	private SqsBatchAdapter _adapter;
	private SqsBatchPublisher _publisher;
	private SqsBatchSubscriber? _subscriber;

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

	internal SqsJobQueueAdapter(
		ILoggerFactory? loggerFactory,
		ISqsQueueFactory queueFactory,
		IJobSerializer serializer,
		ISqsJobQueueAdapterConfig config,
		ITimeSource? timeSource = null)
	{
		var log = Log = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;

		_factory = queueFactory;
		_config = config;
		_serializer = serializer;
		_visibility = SqsConstants.DefaultVisibilityTimeout; // might not be true

		// those are set in Startup
		_queue = null!;
		_adapter = null!;
		_publisher = null!;

		_ready = Task.Run(() => Startup(log, timeSource));
	}

	private async Task Startup(ILogger log, ITimeSource? timeSource)
	{
		var timeAdapter = timeSource is null ? null : new ToysTimeSourceAdapter(timeSource);

		var queue = _queue = await CreateQueue();
		var attributes = await _queue.GetAttributes();
		var visibility = _visibility = TimeSpan.FromSeconds(attributes.VisibilityTimeout);
		var adapter = _adapter = new SqsBatchAdapter(log, queue, visibility);
		_publisher = CreatePublisher(adapter, timeAdapter);
	}

	private Task<ISqsQueue> CreateQueue() =>
		_factory.Create(
			_config.QueueName,
			// ReSharper disable once SuspiciousTypeConversion.Global
			_config.QueueSettings ?? _config as ISqsQueueSettings ?? DefaultSqsQueueSettings);

	private SqsBatchPublisher CreatePublisher(SqsBatchAdapter adapter, ITimeAdapter? timeAdapter)
	{
		var concurrency = (_config.SqsConcurrency ?? DefaultSqsConcurrency).NotLessThan(1);

		var batchSenderSettings = new BatchBuilderSettings {
			BatchDelay = TimeSpan.Zero,
			BatchSize = SqsConstants.MaximumNumberOfMessages,
			Concurrency = concurrency,
		};

		var builder = BatchBuilder
			.Create<string, SendMessageBatchRequestEntry, SqsResult<SendMessageBatchResultEntry>>(
				rq => rq.Id,
				rs => rs.Error?.Id ?? rs.Result?.Id ?? string.Empty,
				adapter.Send,
				batchSenderSettings,
				Log,
				timeAdapter);

		return builder;
	}

	private BatchSubscriber<Message, string> CreateSubscriber(
		SqsBatchAdapter adapter,
		Func<CancellationToken, IJob, Task> handler)
	{
		var retryInterval = (_config.RetryInterval ?? DefaultRetryInterval).NotLessThan(MinimumRetryInterval);
		var retryCount = (_config.RetryCount ?? DefaultRetryCount).NotLessThan(0);
		var sqsConcurrency = (_config.SqsConcurrency ?? DefaultSqsConcurrency).NotLessThan(1);
		var jobConcurrency = (_config.JobConcurrency ?? DefaultJobConcurrency).NotLessThan(1);
		
		var touchInterval = CalculateTouchInterval(_visibility, retryInterval, retryCount);
		var touchDelay = CalculateTouchDelay(_visibility, touchInterval, retryInterval, retryCount);
		var subscriber = new BatchSubscriber<Message, string>(
			adapter,
			(m, t) => HandleMessage(m, handler, t),
			new BatchSubscriberSettings {
				AlternateBatches = true,
				AsynchronousDeletes = true,
				BatchConcurrency = sqsConcurrency,
				HandlerCount = jobConcurrency,
				RetryInterval = retryInterval,
				RetryLimit = retryCount,
				TouchInterval = touchInterval,
				DeleteBatchSize = SqsConstants.MaximumNumberOfMessages,
				TouchBatchSize = SqsConstants.MaximumNumberOfMessages,
				TouchBatchDelay = touchDelay,
			});
		return subscriber;
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
		var delta = (visibility - interval - retryMargin) / (retryCount + 1);
		return TimeSpan.FromSeconds(delta.NotLessThan(0).NotMoreThan(1));
	}

	private SendMessageBatchRequestEntry CreateSendOneRequest(
		TimeSpan delay, Guid jobId, DateTime scheduledFor, string? payload)
	{
		delay = delay.NotLessThan(TimeSpan.Zero).NotMoreThan(SqsConstants.MaximumDelay);

		return new SendMessageBatchRequestEntry {
			Id = Interlocked.Increment(ref _messageId).ToString("x16"),
			DelaySeconds = (int)Math.Ceiling(delay.TotalSeconds),
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
		var response = await _publisher.Request(request);
		var error = response.Error;

		if (error is null)
			return;

		throw new ArgumentException(
			$"Failed to publish job {jobId}: {error.Message}",
			nameof(job));
	}

	public IDisposable Subscribe(Func<CancellationToken, IJob, Task> handler)
	{
		_ready.Await();

		lock (_mutex)
		{
			if (_subscriber is not null)
				throw new InvalidOperationException("Only one subscriber is allowed");

			_subscriber = CreateSubscriber(_adapter, handler);
			_subscriber.Start();
			return Disposable.Create(DisposeSubscriber);
		}
	}

	private void DisposeSubscriber()
	{
		lock (_mutex)
		{
			_subscriber?.Dispose();
			_subscriber = null;
		}
	}

	private async Task HandleMessage(
		Message message,
		Func<CancellationToken, IJob, Task> handler,
		CancellationToken token)
	{
		try
		{
			token.ThrowIfCancellationRequested();
			var job = new SqsReceivedJob(message, _serializer);
			await handler(token, job);
		}
		catch (Exception e)
		{
			Log.LogError(e, "Failed to process message {MessageId}", message.MessageId);
		}
	}

	#warning double dispose is killing it (most likely BatchBuilder)
	public void Dispose()
	{
		_ready.Await(); // it is safe to finish async initialization
		_publisher.Dispose();
		DisposeSubscriber();
	}
}
