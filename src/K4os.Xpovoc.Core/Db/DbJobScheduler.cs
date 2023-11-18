using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Core.Db;

public class DbJobScheduler: IJobScheduler
{
	private readonly ILoggerFactory _loggerFactory;
	private readonly ITimeSource _timeSource;
	private readonly IDbJobStorage _jobStorage;
	private readonly IJobHandler _jobHandler;
	private readonly Task[] _pollers;
	private readonly Task _cleaner;
	private readonly CancellationTokenSource _cancel;
	private readonly TaskCompletionSource<bool> _ready;

	private readonly SchedulerConfig _configuration;

	protected ILogger Log { get; }

	public DbJobScheduler(
		ILoggerFactory? loggerFactory,
		IDbJobStorage jobStorage,
		IJobHandler jobHandler,
		ISchedulerConfig? configuration = null,
		ITimeSource? dateTimeSource = null)
	{
		_loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
		Log = _loggerFactory.CreateLogger(GetType());

		_jobStorage = jobStorage.Required(nameof(jobStorage));
		_jobHandler = jobHandler.Required(nameof(jobHandler));
		_timeSource = dateTimeSource ?? SystemTimeSource.Default;
		_cancel = new CancellationTokenSource();
		_ready = new TaskCompletionSource<bool>();

		_configuration = FixExternalConfig(configuration ?? SchedulerConfig.Default);
		_pollers = CreateWorkers();
		_cleaner = CreateCleaner();

		Start();
	}

	public void Start() => _ready.TrySetResult(true);

	private async Task Shutdown()
	{
		_cancel.Cancel();
		_ready.TrySetCanceled(_cancel.Token);

		await Task.WhenAll(_pollers);
		await _cleaner;

		_jobStorage.TryDispose();
	}

	private Task[] CreateWorkers() =>
		Enumerable
			.Range(0, _configuration.WorkerCount)
			.Select(_ => Task.Run(Poll))
			.ToArray();

	private Task CreateCleaner() => Task.Run(Cleanup);

	private Task Poll()
	{
		var poller = new DbPoller(
			_loggerFactory,
			_timeSource,
			_jobStorage, _jobHandler,
			_configuration);
		return poller.Start(_cancel.Token, _ready.Task);
	}

	private Task Cleanup()
	{
		var cleaner = new DbCleaner(
			_loggerFactory, _timeSource, _jobStorage, _configuration);
		return cleaner.Start(_cancel.Token, _ready.Task);
	}

	public DateTimeOffset Now => _timeSource.Now;

	public async Task<Guid> Schedule(DateTimeOffset time, object payload)
	{
		try
		{
			return await _jobStorage.Schedule(payload, time.UtcDateTime);
		}
		catch (Exception e)
		{
			Log.LogError(e, "Failed to schedule job");
			throw;
		}
	}

	public void Dispose()
	{
		try
		{
			Shutdown().GetAwaiter().GetResult();
		}
		catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
		{
			Log.LogInformation("Scheduler disposed");
		}
		catch (Exception e)
		{
			Log.LogError(e, "Scheduler shutdown failed");
			throw;
		}
	}

	protected static SchedulerConfig FixExternalConfig(ISchedulerConfig configuration)
	{
		var workerCount = configuration.WorkerCount
			.NotLessThan(0);
		var keepAliveInterval = configuration.KeepAliveInterval
			.NotLessThan(DbJobSchedulerDefaults.MinimumKeepAliveInterval);
		var keepAlivePeriod = configuration.KeepAlivePeriod
			.NotLessThan(DbJobSchedulerDefaults.MinimumKeepAlivePeriod)
			.NotLessThan(keepAliveInterval.Times(DbJobSchedulerDefaults.KeepAliveFactor));
		var keepAliveRetryInterval = configuration.KeepAliveRetryInterval
			.NotMoreThan(keepAliveInterval)
			.NotMoreThan(keepAlivePeriod.Times(0.3));
		var pollInterval = configuration.PollInterval
			.NotLessThan(DbJobSchedulerDefaults.MinimumPollInterval);
		var retryLimit = configuration.RetryLimit
			.NotLessThan(1);
		var retryInterval = configuration.RetryInterval
			.NotLessThan(DbJobSchedulerDefaults.MinimumRetryInterval);
		var retryFactor = configuration.RetryFactor
			.NotLessThan(1)
			.NotMoreThan(DbJobSchedulerDefaults.MaximumRetryFactor);
		var maxRetryInterval = configuration.MaximumRetryInterval
			.NotLessThan(retryInterval)
			.NotMoreThan(DbJobSchedulerDefaults.MaximumRetryInterval);
		var pruneInterval = configuration.PruneInterval
			.NotLessThan(DbJobSchedulerDefaults.MinimumPruneInterval)
			.NotMoreThan(DbJobSchedulerDefaults.MaximumPruneInterval);
		var keepFinishedJobsPeriod = configuration.KeepFinishedJobsPeriod
			.NotLessThan(TimeSpan.Zero)
			.NotMoreThan(DbJobSchedulerDefaults.MaximumKeepFinishedJobsPeriod);

		return new SchedulerConfig {
			WorkerCount = workerCount,
			PollInterval = pollInterval,
			KeepAliveInterval = keepAliveInterval,
			KeepAliveRetryInterval = keepAliveRetryInterval,
			KeepAlivePeriod = keepAlivePeriod,
			RetryLimit = retryLimit,
			RetryInterval = retryInterval,
			RetryFactor = retryFactor,
			MaximumRetryInterval = maxRetryInterval,
			PruneInterval = pruneInterval,
			KeepFinishedJobsPeriod = keepFinishedJobsPeriod,
		};
	}
}