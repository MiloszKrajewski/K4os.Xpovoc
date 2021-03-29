using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Rx;

namespace K4os.Xpovoc.Core.Memory
{
	/// <summary>
	/// Toy implementation of <see cref="IDbJobStorage"/>. It is thread safe but
	/// uses locking so it is not very efficient. It is a quick way to fake your storage
	/// for testing, but not really good solution to work with.
	/// If you want memory based scheduler use <see cref="RxJobScheduler"/>
	/// </summary>
	public class MemoryJobStorage: IDbJobStorage
	{
		private readonly object _lock = new();
		private readonly Dictionary<Guid, MemoryJob> _jobs = new();
		private readonly SortedSet<(DateTime, Guid)> _queue = new();
		private static readonly Task<IDbJob> NullJob = Task.FromResult<IDbJob>(null);

		public Task<IDbJob> Claim(
			CancellationToken token, Guid worker, DateTime now, DateTime until)
		{
			lock (_lock)
			{
				var job = FirstVisibleJob(now);
				if (job is null) return NullJob;

				_queue.Remove((job.InvisibleUntil, job.JobId));
				job.Context = worker;
				job.Attempt++;

				return Task.FromResult<IDbJob>(job);
			}
		}

		private MemoryJob FirstVisibleJob(DateTime now)
		{
			// it only works if in 'lock'
			foreach (var (time, id) in _queue)
			{
				if (time > now) break; // no more candidates

				return _jobs[id]; // that's the one
			}

			return null;
		}

		public Task<bool> KeepClaim(
			CancellationToken token, Guid worker, IDbJob job, DateTime until)
		{
			lock (_lock)
				return Task.FromResult(job.Context as Guid? == worker);
		}

		public Task Complete(Guid worker, IDbJob job, DateTime now)
		{
			lock (_lock)
			{
				_jobs.Remove(job.JobId);
			}

			return Task.CompletedTask;
		}

		public Task Forget(Guid worker, IDbJob job, DateTime now)
		{
			lock (_lock)
			{
				_jobs.Remove(job.JobId);
			}

			return Task.CompletedTask;
		}

		public Task Retry(Guid worker, IDbJob job, DateTime when)
		{
			lock (_lock)
			{
				var jobId = job.JobId;
				var entry = _jobs[jobId];
				entry.InvisibleUntil = when;
				entry.Context = null;
				_queue.Add((entry.InvisibleUntil, entry.JobId));
			}

			return Task.CompletedTask;
		}

		public Task<Guid> Schedule(object payload, DateTime when)
		{
			lock (_lock)
			{
				var guid = Guid.NewGuid();
				var job = new MemoryJob {
					JobId = guid,
					Payload = payload,
					InvisibleUntil = when,
					ScheduledFor = when,
					Attempt = 0,
				};
				_jobs[guid] = job;
				_queue.Add((job.InvisibleUntil, guid));

				return Task.FromResult(job.JobId);
			}
		}
	}
}
