using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Memory
{
	public class MemoryJobStorage: IJobStorage
	{
		private readonly object _lock = new object();

		private readonly Dictionary<Guid, Job> _jobs = new Dictionary<Guid, Job>();
		private readonly SortedSet<(DateTime, Guid)> _queue = new SortedSet<(DateTime, Guid)>();

		public Task<IJob> Claim(
			CancellationToken token, Guid worker, DateTime now, DateTime until)
		{
			lock (_lock)
			{
				var job = _queue
					.Select(kv => new { Time = kv.Item1, Id = kv.Item2 })
					.TakeWhile(e => e.Time <= now)
					.Select(e => _jobs[e.Id])
					.FirstOrDefault(j => j.InvisibleUntil <= now);

				if (job is null)
					return null;

				var key = (job.ScheduledFor, job.Id);
				_queue.Remove(key);

				job.Attempt++;
				return Task.FromResult<IJob>(job);
			}
		}

		public Task<bool> KeepClaim(
			CancellationToken token, Guid worker, Guid job, DateTime until) =>
			Task.FromResult(true);

		public Task Complete(Guid worker, Guid job, DateTime now)
		{
			lock (_lock)
			{
				_jobs.Remove(job);
			}

			return Task.CompletedTask;
		}

		public Task Forget(Guid worker, Guid job, DateTime now)
		{
			lock (_lock)
			{
				_jobs.Remove(job);
			}

			return Task.CompletedTask;
		}

		public Task Retry(Guid worker, Guid job, DateTime when)
		{
			lock (_lock)
			{
				var entry = _jobs[job];
				entry.InvisibleUntil = when;
				_queue.Add((entry.ScheduledFor, entry.Id));
			}

			return Task.CompletedTask;
		}

		public Task<Guid> Schedule(object payload, DateTime when)
		{
			lock (_lock)
			{
				var job = new Job {
					Id = Guid.NewGuid(),
					Payload = payload,
					InvisibleUntil = when,
					ScheduledFor = when,
					Attempt = 0,
				};
				_jobs[job.Id] = job;
				_queue.Add((job.ScheduledFor, job.Id));

				return Task.FromResult(job.Id);
			}
		}
	}
}
