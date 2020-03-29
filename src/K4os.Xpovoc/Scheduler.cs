using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc
{
	public class Scheduler: IJobScheduler
	{
		private const double KeepAliveFactor = 3;
		private static readonly TimeSpan MinimumPollInterval = TimeSpan.FromMilliseconds(100);
		private static readonly TimeSpan MinimumRetryInterval = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan MinimumKeepAliveInterval = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan MinimumKeepAlivePeriod = TimeSpan.FromMinutes(1);

		private readonly ILoggerFactory _loggerFactory;
		private readonly IDateTimeSource _dateTimeSource;
		private readonly IJobStorage _jobStorage;
		private readonly IJobHandler _jobHandler;
		private readonly Task[] _pollers;
		private readonly CancellationTokenSource _cancel;
		private readonly TaskCompletionSource<bool> _ready;

		private readonly SchedulerConfig _configuration;

		protected ILogger Log { get; }

		public Scheduler(
			ILoggerFactory loggerFactory,
			IJobStorage jobStorage,
			IJobHandler jobHandler,
			ISchedulerConfig configuration = null,
			IDateTimeSource dateTimeSource = null)
		{
			_loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
			Log = _loggerFactory.CreateLogger(typeof(Scheduler));
			
			_jobStorage = jobStorage.Required(nameof(jobStorage));
			_jobHandler = jobHandler.Required(nameof(jobHandler));
			_dateTimeSource = dateTimeSource ?? SystemDateTimeSource.Default;
			_cancel = new CancellationTokenSource();
			_ready = new TaskCompletionSource<bool>();


			_configuration = FixExternalConfig(configuration ?? SchedulerConfig.Default);
			_pollers = CreateWorkers();

			Startup();
		}

		private static SchedulerConfig FixExternalConfig(ISchedulerConfig configuration)
		{
			var workerCount = configuration.WorkerCount
				.NotLessThan(1);
			var keepAliveInterval = configuration.KeepAliveInterval
				.NotLessThan(MinimumKeepAliveInterval);
			var keepAlivePeriod = configuration.KeepAlivePeriod
				.NotLessThan(MinimumKeepAlivePeriod)
				.NotLessThan(keepAliveInterval.Times(KeepAliveFactor));
			var keepAliceRetryInterval = configuration.KeepAliveRetryInterval
					.NotMoreThan(keepAliveInterval)
					.NotMoreThan(keepAlivePeriod.Times(0.3));
			var pollInterval = configuration.PollInterval
				.NotLessThan(MinimumPollInterval);
			var retryInterval = configuration.RetryInterval
				.NotLessThan(MinimumRetryInterval);

			return new SchedulerConfig {
				WorkerCount = workerCount,
				PollInterval = pollInterval,
				RetryInterval = retryInterval,
				KeepAliveInterval = keepAliveInterval,
				KeepAliveRetryInterval = keepAliceRetryInterval,
				KeepAlivePeriod = keepAlivePeriod,
			};
		}

		private void Startup() => _ready.TrySetResult(true);

		private async Task Shutdown()
		{
			_cancel.Cancel();
			_ready.TrySetCanceled(_cancel.Token);

			await Task.WhenAll(_pollers);
		}

		private Task[] CreateWorkers() =>
			Enumerable
				.Range(0, _configuration.WorkerCount)
				.Select(_ => Task.Run(Poll))
				.ToArray();

		private async Task Poll()
		{
			var poller = new Poller(
				_loggerFactory, 
				_dateTimeSource, 
				_jobStorage, _jobHandler, 
				_configuration);
			await poller.Loop(_cancel.Token, _ready.Task);
		}

		public DateTimeOffset Now => _dateTimeSource.Now;

		public async Task<Guid> Schedule(object payload, DateTimeOffset time)
		{
			try
			{
				return await _jobStorage.Schedule(payload, time);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to schedule job");
				throw;
			}
		}

		public async Task Reschedule(Guid job, DateTimeOffset time)
		{
			try
			{
				await _jobStorage.Reschedule(job, time);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to reschedule job");
				throw;
			}
		}

		public async Task Cancel(Guid job)
		{
			try
			{
				await _jobStorage.Cancel(job);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to cancel job");
				throw;
			}
		}

		public void Dispose() { Shutdown().Wait(); }
	}

	internal class JobHijackedException: Exception
	{
		public JobHijackedException(Guid jobId) { throw new NotImplementedException(); }
	}
}
