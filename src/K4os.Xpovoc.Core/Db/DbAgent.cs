using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Xpovoc.Core.Db
{
	internal abstract class DbAgent
	{
		private string ObjectId => $"{GetType().Name}@{RuntimeHelpers.GetHashCode(this)}";

		protected readonly ILogger Log;

		protected DateTime Now => _dateTimeSource.Now.UtcDateTime;

		protected readonly IDbJobStorage JobStorage;
		protected readonly ISchedulerConfig Configuration;
		private readonly IDateTimeSource _dateTimeSource;
		private int _started;

		protected DbAgent(
			ILoggerFactory loggerFactory,
			IDateTimeSource dateTimeSource,
			IDbJobStorage storage,
			ISchedulerConfig config)
		{
			Log = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(ObjectId);
			_dateTimeSource = dateTimeSource;
			Configuration = config;
			JobStorage = storage;
		}

		public async Task Start(CancellationToken token, Task ready = null)
		{
			if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
				throw new InvalidOperationException(
					"Background operation has been already started");

			await (ready ?? Task.CompletedTask);

			var random = new Random();
			var errors = 0;
			while (!token.IsCancellationRequested)
			{
				try
				{
					if (errors > 0) await DelayOnErrors(token, random, errors);
					await Loop(token);
					errors = 0;
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
					Log.LogWarning("Background operation cancelled.");
					return;
				}
				catch (Exception e)
				{
					Log.LogError(e, "Background operation failed. Retrying...");
					errors++;
				}
			}
		}

		private Task DelayOnErrors(CancellationToken token, Random random, int errors)
		{
			if (errors <= 0) return Task.CompletedTask;

			var delay = (0.6 + random.NextDouble() * 0.4) * Math.Min(errors, 5);
			Log.LogWarning($"Errors encountered. Retrying in {delay:0.0}s.");
			return Task.Delay(TimeSpan.FromSeconds(delay), token);
		}

		protected abstract Task Loop(CancellationToken token);
	}
}
