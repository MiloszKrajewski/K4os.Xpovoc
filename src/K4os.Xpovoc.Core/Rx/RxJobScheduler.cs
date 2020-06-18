using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Core.Rx
{
	public class RxJobScheduler: IJobScheduler
	{
		private const int MaxAttempts = 10;

		private readonly IJobHandler _jobHandler;
		private readonly IScheduler _scheduler;

		private readonly LinkedList<RxJob> _jobs = new LinkedList<RxJob>();
		private readonly CancellationTokenSource _cancel;

		protected ILogger Log { get; }

		public RxJobScheduler(
			ILoggerFactory loggerFactory,
			IJobHandler jobHandler,
			IScheduler scheduler)
		{
			Log = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(GetType());
			_jobHandler = jobHandler.Required(nameof(jobHandler));
			_scheduler = scheduler.Required(nameof(scheduler));
			_cancel = new CancellationTokenSource();
		}

		public void Dispose()
		{
			_cancel.Cancel();

			IDisposable[] disposables;
			lock (_jobs)
			{
				disposables = _jobs.Select(e => e.Schedule).ToArray();
				_jobs.Clear();
			}

			disposables.ForEach(TryDispose);
		}

		private void TryDispose(IDisposable disposable)
		{
			try
			{
				disposable?.Dispose();
			}
			catch (Exception e)
			{
				Log.LogError(e, "Failed to dispose job execution");
			}
		}

		public DateTimeOffset Now => _scheduler.Now;

		public Task<Guid> Schedule(DateTimeOffset time, object payload) =>
			Task.FromResult(CreateAndScheduleEntry(Guid.NewGuid(), payload, time).Value.Id);

		private LinkedListNode<RxJob> CreateAndScheduleEntry(
			Guid guid, object payload, DateTimeOffset time, int attempt = 0)
		{
			var entry = new RxJob { Id = guid, Payload = payload, Attempt = attempt };
			var node = new LinkedListNode<RxJob>(entry);
			lock (_jobs) _jobs.AddLast(node);
			node.Value.Schedule = _scheduler.Schedule(time, CreateProxyHandler(node));
			return node;
		}

		private Action CreateProxyHandler(LinkedListNode<RxJob> node) =>
			() => Task.Run(() => Handle(node));

		private async Task Handle(LinkedListNode<RxJob> node)
		{
			if (_cancel.IsCancellationRequested || !TryRemoveNode(node))
				return;

			var job = node.Value;
			var guid = job.Id;
			var attempt = job.Attempt + 1;
			var payload = job.Payload;

			try
			{
				await _jobHandler.Handle(_cancel.Token, payload);
			}
			catch (Exception e)
			{
				var retry = attempt < MaxAttempts;
				var level = retry ? LogLevel.Warning : LogLevel.Error;
				Log.Log(level, e, "Failed to execute job");
				if (!retry || _cancel.IsCancellationRequested) return;

				var when = _scheduler.Now.AddSeconds(attempt);
				CreateAndScheduleEntry(guid, payload, when, attempt);
			}
		}

		private bool TryRemoveNode(LinkedListNode<RxJob> node)
		{
			lock (_jobs)
			{
				if (node.List is null) 
					return false;

				_jobs.Remove(node);
			}

			return true;
		}
	}
}
