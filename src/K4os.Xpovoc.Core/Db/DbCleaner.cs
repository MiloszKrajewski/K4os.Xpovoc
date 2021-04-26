using System;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;

namespace K4os.Xpovoc.Core.Db
{
	internal class DbCleaner: DbAgent
	{
		private static readonly TimeSpan ShortInterval = TimeSpan.FromSeconds(5);
		public TimeSpan PruneInterval { get; }

		public DbCleaner(
			ILoggerFactory loggerFactory,
			IDateTimeSource dateTimeSource,
			IDbJobStorage storage,
			ISchedulerConfig config):
			base(loggerFactory, dateTimeSource, storage, config)
		{
			PruneInterval = config.PruneInterval.NotLessThan(ShortInterval);
		}

		protected override async Task Loop(CancellationToken token)
		{
			Log.LogInformation("Database cleanup agent started");

			var rng = new Random();
			var seq = 0u; // uint to make overflow start from 0

			while (!token.IsCancellationRequested)
			{
				await Jitter(token, rng, seq);
				seq = await Prune() ? seq + 1 : 0;
			}
		}

		private Task Jitter(
			CancellationToken token, Random rng, uint seq)
		{
			var interval = seq switch {
				0 => PruneInterval, < 16 => ShortInterval, _ => TimeSpan.Zero,
			};
			return Jitter(token, rng, interval, 0.3);
		}

		private Task Jitter(
			CancellationToken token, Random rng, TimeSpan interval, double jitter)
		{
			var scale = 1 + jitter.ClampBetween(0, 1) * (rng.NextDouble() * 2 - 1);
			var delay = TimeSpan.FromSeconds(interval.TotalSeconds.NotLessThan(0) * scale);
			if (delay <= TimeSpan.Zero) 
				return Task.CompletedTask;

			Log.LogDebug($"Scheduling database cleanup in {delay:c}");
			return Task.Delay(delay, token);
		}

		private Task<bool> Prune()
		{
			Log.LogInformation("Database cleanup operation started");
			var cutoff = Now.Subtract(Configuration.KeepFinishedJobsPeriod);
			return JobStorage.Prune(cutoff);
		}
	}
}
