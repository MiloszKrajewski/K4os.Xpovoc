using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Json;
using K4os.Xpovoc.Sqs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Playground;

public class TrueDeferredJobs: IJobHandler
{
	protected readonly ILogger<TrueDeferredJobs> Log;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IAmazonSQS _sqsClient;
	private readonly ConcurrentDictionary<Guid, Event> _scheduled = new();
	private readonly TimeSpan _timeLimit;

	public TrueDeferredJobs(IAmazonSQS client, ILoggerFactory loggerFactory, TimeSpan timeLimit)
	{
		Log = loggerFactory.CreateLogger<TrueDeferredJobs>();
		_sqsClient = client;
		_loggerFactory = loggerFactory;
		_timeLimit = timeLimit;
	}

	public async Task RunAsync()
	{
		var adapter = new SqsJobQueueAdapter(
			_loggerFactory,
			_sqsClient,
			new JsonJobSerializer(
				new JsonSerializerSettings {
					TypeNameHandling = TypeNameHandling.All,
				}),
			new SqsJobQueueAdapterSettings {
				QueueName = "xpovoc-TrueDeferredJobs",
				JobConcurrency = 16,
			});
		using var scheduler = new QueueJobScheduler(_loggerFactory, adapter, this);

		var limit = DateTime.UtcNow.Add(_timeLimit);
		var next = DateTime.UtcNow;

		while (next < limit)
		{
			var @event = new Event { Id = Guid.NewGuid(), Time = next };
			_scheduled.TryAdd(@event.Id, @event);
			next = next.AddSeconds(1);
		}

		var actions = _scheduled.Values
			.ToArray()
			.OrderBy(x => x.Time)
			.Select(e => scheduler.Schedule(e.Time, e))
			.ToArray();
		await Task.WhenAll(actions);

		while (true)
		{
			var now = DateTime.UtcNow;
			if (now > limit)
			{
				if (_scheduled.IsEmpty)
				{
					Log.LogInformation("All events received");
					break;
				}

				Log.LogWarning("Time limit reached");
			}

			if (now > limit.AddSeconds(10))
			{
				if (!_scheduled.IsEmpty)
				{
					Log.LogError("Some events were not received: {Count}", _scheduled.Count);
				}

				break;
			}

			Log.LogInformation(
				"Waiting for events... {Count} expected, time left {Left}", _scheduled.Count,
				(limit - now).NotLessThan(TimeSpan.Zero));
			await Task.Delay(TimeSpan.FromSeconds(3));
		}
	}

	public Task Handle(CancellationToken token, object payload)
	{
		var received = (Event)payload;
		if (!_scheduled.TryRemove(received.Id, out _))
		{
			Log.LogWarning("Received event {Id} which was not scheduled", received.Id);
			return Task.CompletedTask;
		}

		var now = DateTime.UtcNow;
		var expected = received.Time;

		if (now < expected)
		{
			Log.LogError(
				"Received event {Id} too early: {Received} vs {Now}",
				received.Id, received.Time, now);
		}

		if (now > expected.AddSeconds(3))
		{
			Log.LogError(
				"Received event {Id} too late: {Received} vs {Now}",
				received.Id, received.Time, now);
		}

		Log.LogInformation(
			"Received event {Id} at {Now} (expected {Expected}, diff {Diff:0.00}s)",
			received.Id, now, expected, (now - expected).TotalSeconds);

		return Task.CompletedTask;
	}
}

public class Event
{
	public Guid Id { get; set; }
	public DateTime Time { get; set; }
}
