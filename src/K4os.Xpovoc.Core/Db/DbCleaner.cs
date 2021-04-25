using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;

namespace K4os.Xpovoc.Core.Db
{
	internal class DbCleaner: DbAgent
	{
		private static readonly TimeSpan MinimumMoreInterval = TimeSpan.FromSeconds(1);
		private static readonly TimeSpan MaximumMoreInterval = TimeSpan.FromSeconds(5);
		private static readonly TimeSpan MinimumCleanupInterval = TimeSpan.FromMinutes(1);
		private static readonly TimeSpan MaximumCleanupInterval = TimeSpan.FromMinutes(15);

		public DbCleaner(
			ILoggerFactory loggerFactory,
			IDateTimeSource dateTimeSource,
			IDbJobStorage storage,
			ISchedulerConfig config):
			base(loggerFactory, dateTimeSource, storage, config) { }

		protected override async Task Loop(CancellationToken token)
		{
			Log.LogInformation("Database cleanup agent started");

			var random = new Random();
			var more = false;

			while (!token.IsCancellationRequested)
			{
				await Jitter(token, random, more);
				more = await Cleanup(token);
			}
		}
		
		private Task Jitter(
			CancellationToken token, Random random, bool more)
		{
			var (min, max) = more
				? (MinimumMoreInterval, MaximumMoreInterval)
				: (MinimumCleanupInterval, MaximumCleanupInterval);
			return Jitter(token, random, min, max);
		}

		private Task Jitter(
			CancellationToken token, Random random, TimeSpan minimum, TimeSpan maximum)
		{
			var scale = (maximum - minimum).TotalSeconds;
			var delay = minimum + TimeSpan.FromSeconds(random.NextDouble() * scale);
			Log.LogDebug($"Scheduling database cleanup in {delay:0}s");
			return Task.Delay(delay, token);
		}

		private Task<bool> Cleanup(CancellationToken token)
		{
			Log.LogInformation("Database cleanup operation started");
			return JobStorage.Cleanup(token, Now);
		}
	}
}
