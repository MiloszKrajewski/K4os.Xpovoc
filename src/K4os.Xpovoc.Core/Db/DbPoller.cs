using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Core.Db
{
	internal class DbPoller 
	{
		protected ILogger Log { get; }

		private string ObjectId => $"{GetType().Name}@{RuntimeHelpers.GetHashCode(this)}";

		private readonly Guid _workerId;
		private readonly IDbJobStorage _jobStorage;
		private readonly IJobHandler _jobHandler;
		private readonly ISchedulerConfig _configuration;
		private readonly IDateTimeSource _dateTimeSource;

		private int _started;

		public DbPoller(
			ILoggerFactory loggerFactory,
			IDateTimeSource dateTimeSource,
			IDbJobStorage storage,
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
				throw new InvalidOperationException("Poller has been already started");

			Log.LogDebug("Awaiting scheduler startup...");

			await ready;

			Log.LogInformation("Poller started for worker {0}", _workerId);

			var interval = _configuration.PollInterval;

			await ready;
			while (!token.IsCancellationRequested)
			{
				var job = await Claim(token);
				if (job is null)
				{
					await Task.Delay(interval, token);
				}
				else
				{
					await Process(token, job);
				}
			}
		}

		private async Task<IDbJob> Claim(CancellationToken token)
		{
			var now = Now;
			var until = now.Add(_configuration.KeepAlivePeriod);

			try
			{
				var job = await _jobStorage.Claim(token, _workerId, now, until);
				if (job is null) return null;
				Log.LogDebug(
					"Job {0} has been claimed by {1} until {2}", 
					job.JobId, _workerId, until);
				return job;
			}
			catch (Exception e)
			{
				Log.LogError(e, "Worker {0} failed to claim job", _workerId);
				return null;
			}
		}

		[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
		private async Task Process(CancellationToken token, IDbJob job)
		{
			// this seems like performance drag for no reason
			// if job is hijacked do we need to try to interrupt it?
			using var hijacked = new CancellationTokenSource();
			using var combined = CancellationTokenSource
				.CreateLinkedTokenSource(token, hijacked.Token);
			using var finished = new CancellationTokenSource();
			
			var keepAlive = Task.Run(
				() => MaintainClaim(finished.Token, job, hijacked),
				CancellationToken.None);

			var success = await Handle(combined.Token, job);

			if (await CancelClaim(finished, hijacked, keepAlive))
			{
				if (success)
				{
					await Complete(job);
				}
				else if (job.Attempt < _configuration.RetryLimit)
				{
					await Retry(job);
				}
				else
				{
					await Forget(job);
				}
			}
			else
			{
				Log.LogError("Execution of job {0} has been hijacked", job.JobId);
			}
		}

		private async Task<bool> Handle(CancellationToken token, IDbJob job)
		{
			try
			{
				Log.LogInformation(
					"Job {0} attempt {1} has been started", 
					job.JobId, job.Attempt);
				await _jobHandler.Handle(token, job.Payload);
				return true;
			}
			catch (Exception e)
			{
				Log.LogError(e, "Job {0} execution failed", job.JobId);
				return false;
			}
		}

		private async Task Complete(IDbJob job)
		{
			try
			{
				Log.LogInformation("Job {0} has been completed", job.JobId);
				await _jobStorage.Complete(_workerId, job, Now);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to complete job {0}", job.JobId);
			}
		}

		private int MaxRetries()
		{
			// l = r*(f^n) -> n = log(l/r) / log(f)
			var l = _configuration.MaximumRetryInterval.TotalSeconds;
			var r = _configuration.RetryInterval.TotalSeconds;
			var f = _configuration.RetryFactor;
			return (int) Math.Ceiling(Math.Log(l / r) / Math.Log(f));
		}

		private TimeSpan RetryDelay(int attempt)
		{
			// l = r*(f^n) -> n = log(l/r) / log(f)
			var retry = attempt - 1; // second attempt is first retry
			var n = retry.NotLessThan(0).NotMoreThan(MaxRetries());
			var l = _configuration.MaximumRetryInterval.TotalSeconds;
			var r = _configuration.RetryInterval.TotalSeconds;
			var f = _configuration.RetryFactor;
			return TimeSpan.FromSeconds((r * Math.Pow(f, n)).NotMoreThan(l));
		}

		private async Task Retry(IDbJob job)
		{
			try
			{
				var delay = RetryDelay(job.Attempt - 1);
				var when = Now.Add(delay);
				Log.LogInformation(
					"Job {0} attempt {1} failed, job will retried after {2} at {3}",
					job.JobId, job.Attempt, delay, when);
				await _jobStorage.Retry(_workerId, job, when);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to postpone job {0}", job.JobId);
			}
		}

		private async Task Forget(IDbJob job)
		{
			try
			{
				Log.LogWarning(
					"Job {0} attempt {1} failed, giving up...", 
					job.JobId, job.Attempt);
				await _jobStorage.Forget(_workerId, job, Now);
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to forget job {0}", job.JobId);
			}
		}

		private async Task MaintainClaim(
			CancellationToken token, IDbJob job, CancellationTokenSource hijacked)
		{
			var healthyInterval = _configuration.KeepAliveInterval;
			var failedInterval = _configuration.KeepAliveRetryInterval;

			try
			{
				await Task.Delay(healthyInterval, token);

				while (!token.IsCancellationRequested)
				{
					var status = await KeepClaim(token, job);
					if (status == ClaimStatus.Kept)
					{
						await Task.Delay(healthyInterval, token);
					}
					else if (status == ClaimStatus.Failed)
					{
						await Task.Delay(failedInterval, token);
					}
					else if (status == ClaimStatus.Lost)
					{
						hijacked.Cancel();
						break;
					}
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				// ignore, it is all fine...
			}
		}

		private enum ClaimStatus { Kept, Failed, Lost }

		private async Task<ClaimStatus> KeepClaim(CancellationToken token, IDbJob job)
		{
			var until = Now.Add(_configuration.KeepAlivePeriod);

			try
			{
				var kept = await _jobStorage.KeepClaim(token, _workerId, job, until);

				if (!kept)
				{
					Log.LogError("Claim for {0} has been lost", job.JobId);
					return ClaimStatus.Lost;
				}

				Log.LogDebug("Claim for {0} has been renewed until {1}", job.JobId, until);
				return ClaimStatus.Kept;
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				// it is not kept, but we most likely don't care so failed is ok
				return ClaimStatus.Failed;
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to keep claim for {0}", job.JobId);
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
