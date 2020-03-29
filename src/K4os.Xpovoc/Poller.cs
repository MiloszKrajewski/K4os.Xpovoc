using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc
{
	public class PollerConfig { }

	internal class Poller
	{
		protected ILogger Log { get; }
		private string ObjectId => $"{GetType().Name}@{RuntimeHelpers.GetHashCode(this)}";

		private readonly Guid _workerId;
		private readonly IJobStorage _jobStorage;
		private readonly IJobHandler _jobHandler;
		private readonly ISchedulerConfig _configuration;
		private readonly IDateTimeSource _dateTimeSource;

		private int _started;

		public Poller(
			ILoggerFactory loggerFactory,
			IDateTimeSource dateTimeSource,
			IJobStorage storage,
			IJobHandler handler,
			ISchedulerConfig config)
		{
			Log = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(ObjectId);

			_workerId = Guid.NewGuid();
			_dateTimeSource = dateTimeSource.Required(nameof(dateTimeSource));
			_jobStorage = storage.Required(nameof(storage));
			_jobHandler = handler.Required(nameof(handler));
			_configuration = config.Required(nameof(config));
		}

		private DateTime Now => _dateTimeSource.Now.UtcDateTime;

		public async Task Loop(CancellationToken token, Task ready)
		{
			if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
				throw new InvalidOperationException($"Poller has been already started");

			Log.LogDebug("Awaiting scheduler startup...");

			await ready;

			Log.LogInformation("Poller started for worker {0}", _workerId);

			var interval = _configuration.PollInterval;

			await ready;
			while (!token.IsCancellationRequested)
			{
				var job = await ClaimJob(token);
				if (job is null)
				{
					await Task.Delay(interval, token);
				}
				else
				{
					await ProcessJob(token, job);
				}
			}
		}

		private async Task<IJob> ClaimJob(CancellationToken token)
		{
			var now = Now;
			var until = now.Add(_configuration.KeepAliveInterval);

			try
			{
				return await _jobStorage.Claim(token, _workerId, now, until);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to claim job");
				return null;
			}
		}

		[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
		private async Task ProcessJob(CancellationToken token, IJob job)
		{
			using (var hijacked = new CancellationTokenSource())
			using (var combined = CancellationTokenSource
				.CreateLinkedTokenSource(token, hijacked.Token))
			using (var finished = new CancellationTokenSource())
			{
				var keepAlive = Task.Run(
					() => MaintainClaim(finished.Token, job, hijacked),
					CancellationToken.None);

				var success = await HandleJob(combined.Token, job);

				if (await CancelClaim(finished, hijacked, keepAlive))
				{
					await (success ? CompleteJob(job) : RetryJob(job));
				}
				else
				{
					Log.LogError("Execution of job {0} has been hijacked", job.Id);
				}
			}
		}

		private async Task<bool> HandleJob(CancellationToken token, IJob job)
		{
			try
			{
				await _jobHandler.Handle(token, job.Payload);
				return true;
			}
			catch (Exception e)
			{
				Log.LogError(e, "Job {0} execution failed", job.Id);
				return false;
			}
		}

		private async Task CompleteJob(IJob job)
		{
			try
			{
				await _jobStorage.Complete(_workerId, job.Id, Now);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to complete job");
			}
		}

		private async Task RetryJob(IJob job)
		{
			try
			{
				var interval = _configuration.RetryInterval;
				await _jobStorage.Retry(_workerId, job.Id, Now.Add(interval));
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to postpone job");
			}
		}

		private async Task MaintainClaim(
			CancellationToken token, IJob job, CancellationTokenSource hijacked)
		{
			await Task.CompletedTask; // just make sure it is async
			
			var healthyInterval = _configuration.KeepAliveInterval;
			var failedInterval = _configuration.KeepAliveRetryInterval;

			try
			{
				while (!token.IsCancellationRequested && !hijacked.IsCancellationRequested)
				{
					var status = await KeepClaim(token, job);
					if (status == ClaimStatus.Kept)
					{
						await Task.Delay(healthyInterval, token);
					}
					else if (status == ClaimStatus.Failed)
					{
						await Task.Delay(healthyInterval, token);
					}
					else if (status == ClaimStatus.Lost)
					{
						hijacked.Cancel();
					}
				}
			}
			catch (TaskCanceledException)
			{
				// ignore, it is all fine...
			}
		}

		public enum ClaimStatus { Kept, Failed, Lost }

		private async Task<ClaimStatus> KeepClaim(CancellationToken token, IJob job)
		{
			var until = Now.Add(_configuration.KeepAlivePeriod);

			try
			{
				var kept = await _jobStorage.KeepClaim(token, _workerId, job.Id, until);
				return kept ? ClaimStatus.Kept : ClaimStatus.Lost;
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to keep claim job");
				// even though we failed to keep claim, we optimistically
				// assume no one intercepted it (yet?)
				return ClaimStatus.Failed;
			}
		}

		private static async Task<bool> CancelClaim(
			CancellationTokenSource finished, CancellationTokenSource hijacked, Task keepAlive)
		{
			finished.Cancel();
			await keepAlive;
			return !hijacked.IsCancellationRequested;
		}
	}
}
