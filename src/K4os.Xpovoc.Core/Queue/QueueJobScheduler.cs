using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Core.Queue;

public class QueueJobScheduler: IJobScheduler
{
	public ILogger Log { get; }

	private readonly ITimeSource _timeSource;
	private readonly IJobQueueAdapter _jobQueueAdapter;
	private readonly IJobHandler _jobHandler;
	private readonly IDisposable _jobQueueSubscription;

	public QueueJobScheduler(
		ILoggerFactory? loggerFactory,
		IJobQueueAdapter jobStorage,
		IJobHandler jobHandler,
		ITimeSource? dateTimeSource = null)
	{
		Log = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;
		_timeSource = dateTimeSource ?? SystemTimeSource.Default;
		_jobHandler = jobHandler.Required(nameof(jobHandler));
		_jobQueueAdapter = jobStorage.Required(nameof(jobStorage));
		_jobQueueSubscription = _jobQueueAdapter.Subscribe(TryHandle);
	}

	private Task TryHandle(CancellationToken token, IJob job) => 
		Now < job.UtcTime ? Reschedule(job) : Handle(token, job);

	private async Task Handle(CancellationToken token, IJob job)
	{
		var jobId = job.JobId;

		Log.LogInformation("Job {Job} has been started", jobId);

		try
		{
			var payload = job.Payload;

			if (payload is null)
			{
				Log.LogWarning("Job {Job} had no payload and has been ignored", jobId);
			}
			else
			{
				await _jobHandler.Handle(token, payload);
			}
		}
		catch (Exception e)
		{
			// NOTE: error handling and retry policy is NOT concern of this class
			Log.LogError(e, "Job {Job} execution failed", jobId);
			throw;
		}
	}

	public DateTimeOffset Now => _timeSource.Now;
	
	protected class JobEnvelope: IJob
	{
		public Guid JobId { get; set; }
		public DateTime UtcTime { get; set; }
		public object? Payload { get; set; }
		public object? Context { get; set; }
	}
	
	public async Task<Guid> Schedule(DateTimeOffset time, object payload)
	{
		var jobId = Guid.NewGuid();
		var job = new JobEnvelope {
			JobId = jobId, 
			UtcTime = time.UtcDateTime, 
			Payload = payload, 
			Context = null,
		};
		await Schedule(job);
		return jobId;
	}

	private Task Reschedule(IJob envelope) => 
		Schedule(envelope);

	private async Task Schedule(IJob job)
	{
		var when = job.UtcTime;
		var delay = when - Now;
		await _jobQueueAdapter.Publish(delay, job, CancellationToken.None);
	}

	public void Dispose()
	{
		_jobQueueSubscription.Dispose();
		_jobQueueAdapter.TryDispose();
	}
}
