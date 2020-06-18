// using System;
// using System.Reactive.Concurrency;
// using System.Threading;
// using System.Threading.Tasks;
// using K4os.Xpovoc.Abstractions;
// using Microsoft.Extensions.Logging;
// using Microsoft.Extensions.Logging.Abstractions;
//
// namespace K4os.Xpovoc.Core.Queue
// {
// 	public class QueueJobScheduler: IJobScheduler, IDisposable
// 	{
// 		private static readonly TimeSpan AtLeastOneSecond = TimeSpan.FromSeconds(1);
// 		private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);
//
// 		public ILogger Log { get; }
//
// 		private readonly IDisposable _subscription;
// 		private readonly IJobHandler _handler;
// 		private readonly IScheduler _scheduler;
// 		private IJobQueue _jobQueue;
//
// 		public DateTimeOffset Now => _scheduler.Now;
//
// 		public QueueJobScheduler(
// 			ILoggerFactory logFactory,
// 			IJobHandler handler,
// 			IJobQueue jobQueue,
// 			IScheduler scheduler = null)
// 		{
// 			Log = (logFactory ?? NullLoggerFactory.Instance).CreateLogger(GetType());
// 			
// 			_scheduler = scheduler ?? Scheduler.Default;
// 			_handler = handler.Required(nameof(handler));
// 			_jobQueue = jobQueue.Required(nameof(jobQueue));
// 			_subscription = jobQueue.Subscribe(Handle);
// 		}
//
// 		private Task Handle(IJob job) { throw new NotImplementedException(); }
//
// 		public Task<Guid> Schedule(DateTimeOffset time, object payload)
// 		{
// 			try
// 			{
// 				_jobQueue.Publish(payload, time, 1)
// 					.Publish(new Envelope { Body = body, Time = time })
// 					.Wait(PublishTimeout);
// 			}
// 			catch (Exception e)
// 			{
// 				throw e.Unwrap().Rethrow();
// 			}
//
// 		}
//
//
// 		private async Task Handle(CancellationToken token, Envelope envelope)
// 		{
// 			var now = Now;
//
// 			if (now >= envelope.Time)
// 			{
// 				// time has come - execute action
// 				Log.LogDebug($"Handle.Execute({envelope.Time})");
// 				var message = _deserializer(envelope.Body);
// 				await _handler.Execute(message);
// 			}
// 			else
// 			{
// 				// still time - put it back to the queue
// 				Log.LogDebug($"Handle.Republish({envelope.Time})");
// 				await _publisher.Publish(envelope);
// 			}
// 		}
//
// 		public void Dispose()
// 		{
// 			_subscription?.Dispose();
// 		}
// 	}
//
// 	public interface IJobQueue
// 	{
// 		IDisposable Subscribe(Func<IJob, Task> handle);
// 	}
// }
