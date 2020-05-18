using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Memory
{
	public class MemoryJobStorage: IDbJobStorage
	{
		private readonly object _lock = new object();

		private readonly Dictionary<Guid, Job> _jobs = 
			new Dictionary<Guid, Job>();
		private readonly SortedSet<(DateTime, Guid)> _queue = 
			new SortedSet<(DateTime, Guid)>();

		public Task<IDbJob> Claim(
			CancellationToken token, Guid worker, DateTime now, DateTime until)
		{
			lock (_lock)
			{
				var job = FirstVisibleJob(now);
				if (job is null) return null;

				_queue.Remove((job.ScheduledFor, job.JobId));
				job.Attempt++;
				return Task.FromResult<IDbJob>(job);
			}
		}

		private Job FirstVisibleJob(DateTime now)
		{
			// it only works if in 'lock'
			foreach (var (time, id) in _queue)
			{
				if (time > now) break; // no more candidates

				var job = _jobs[id];
				if (job.InvisibleUntil > now) continue; // hidden, try next

				return job; // that's the one
			}

			return null;
		}

		public Task<bool> KeepClaim(
			CancellationToken token, Guid worker, IDbJob job, DateTime until) =>
			Task.FromResult(true);

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
				_queue.Add((entry.ScheduledFor, entry.JobId));
			}

			return Task.CompletedTask;
		}

		public Task<Guid> Schedule(object payload, DateTime when)
		{
			lock (_lock)
			{
				var guid = Guid.NewGuid();
				var job = new Job {
					JobId = guid,
					Payload = payload,
					InvisibleUntil = when,
					ScheduledFor = when,
					Attempt = 0,
				};
				_jobs[guid] = job;
				_queue.Add((job.ScheduledFor, guid));

				return Task.FromResult(job.JobId);
			}
		}
	}
}
