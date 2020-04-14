using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Rx
{
	public class Class1: IJobScheduler {
		private IJobHandler _jobHandler;
		private IScheduler _scheduler;

		protected ILogger Log { get; }

		public Class1(
			ILoggerFactory loggerFactory,
			IJobHandler jobHandler,
			IScheduler scheduler)
		{
			Log = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(GetType());
			_jobHandler = jobHandler.Required(nameof(jobHandler));
			_scheduler = scheduler.Required(nameof(scheduler));
		}

		public void Dispose() { throw new NotImplementedException(); }

		public DateTimeOffset Now => _scheduler.Now;

		public Task<Guid> Schedule(object payload, DateTimeOffset time)
		{
			var guid = Guid.NewGuid();
			_scheduler.Schedule(time, () => Handle(guid, payload, 1));
			return Task.FromResult(guid);
		}

		private void Handle(Guid job, object payload, int attempt)
		{
		}
	}
}
