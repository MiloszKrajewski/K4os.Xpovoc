using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using K4os.Xpovoc.MySql.Playground;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Polly;

namespace K4os.Xpovoc.PgSql.Playground
{
	internal static class Program
	{
		public static async Task Main(string[] args)
		{
			var loggerFactory = new LoggerFactory();
			loggerFactory.AddProvider(new ColorConsoleProvider());
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ILoggerFactory>(loggerFactory);

			Configure(serviceCollection);
			var serviceProvider = serviceCollection.BuildServiceProvider();

			await Execute(loggerFactory, serviceProvider, args);
		}

		private static void Configure(ServiceCollection serviceCollection)
		{
			var connectionString =
				"host=raspberrypi;database=xpovoc;username=postgres;password=tsfvc095;Maximum Pool Size=8";
			serviceCollection.AddTransient<DbConnection>(
				_ => new NpgsqlConnection(connectionString));
		}

		private static readonly AsyncPolicy DeadlockPolicy = Policy
			.Handle<NpgsqlException>(e => e.ErrorCode == int.MaxValue)
			.WaitAndRetryForeverAsync(i => TimeSpan.FromMilliseconds(Math.Min((i - 1) * 33, 1000)));

		private static async Task Execute(
			ILoggerFactory loggerFactory, IServiceProvider serviceProvider, string[] args)
		{
			DbConnection Connect() => serviceProvider.GetService<DbConnection>();
			ILogger Logger(string name) => loggerFactory.CreateLogger(name);

			var cancel = new CancellationTokenSource();
			var token = cancel.Token;

			var producers = Enumerable
				.Range(0, 16)
				.Select(i => Producer(token, Connect, Logger($"Producer{i}")))
				.ToArray();

			var consumers = Enumerable
				.Range(0, 16)
				.Select(i => Consumer(token, Connect, Logger($"Consumer{i}")))
				.ToArray();

			// ReSharper disable once MethodSupportsCancellation
			await Task.Run(Console.ReadLine);

			cancel.Cancel();
			await Task.WhenAny(producers.Concat(consumers));
		}

		private static long _producedCount = 0;

		private static async Task Producer(
			CancellationToken token, Func<DbConnection> connect, ILogger logger)
		{
			var random = new Random();

			while (!token.IsCancellationRequested)
			{
				try
				{
					var now = DateTime.UtcNow;
					var time = now.AddSeconds(random.NextDouble() * 5);
					using (var connection = connect())
					{
						await connection.OpenAsync(token);
						await connection.ExecuteAsync(
							@"-- insert job
							insert into jobs (job_id, created_on, scheduled_on) 
							values (@job_id, @created_on, @scheduled_on)",
							new {
								job_id = Guid.NewGuid(),
								created_on = now,
								scheduled_on = time
							});
					}

					var counter = Interlocked.Increment(ref _producedCount);
					if (counter % 100 == 0) logger.LogDebug($"Inserted {counter} jobs it total");
				}
				catch (Exception e)
				{
					logger.LogError(e, "Producer failure");
				}
			}
		}

		private static long _consumedCount = 0;

		private static async Task Consumer(
			CancellationToken token, Func<DbConnection> connect, ILogger logger)
		{
			var consumerId = Guid.NewGuid();
			var random = new Random();

			while (!token.IsCancellationRequested)
			{
				try
				{
					var jobId = await GetJob(connect, consumerId);
					if (jobId.HasValue)
					{
						// job execution is here
						await DeadlockPolicy.ExecuteAsync(() => DeleteJob(connect, jobId.Value));
						var counter = Interlocked.Increment(ref _consumedCount);
						if (counter % 100 == 0)
							logger.LogDebug($"Finished {counter} jobs it total");
						continue;
					}

					while (true)
					{
						var claimed =
							await DeadlockPolicy.ExecuteAsync(() => ClaimJobs(connect, consumerId));
						if (claimed > 0) break;

						logger.LogDebug("No jobs claimed, waiting 5s");
						await Task.Delay(TimeSpan.FromSeconds(5), token);
					}
				}
				catch (Exception e)
				{
					logger.LogError(e, "Consumer failure");
				}
			}
		}

		private static async Task<int> ClaimJobs(Func<DbConnection> connect, Guid consumerId)
		{
			using (var connection = connect())
			{
				var now = DateTime.UtcNow;
				var expires = now.AddMinutes(1);
				return await connection.ExecuteAsync(
					@"-- claim jobs 
					with selected as (
						select row_id 
						from jobs 
						where scheduled_on <= @now and (claim_expires_on is null or claim_expires_on < @now)
						order by scheduled_on
						limit 1
					)
					update jobs
					set claimed_by = @consumer, claim_expires_on = @expires
					from selected
					where jobs.row_id = selected.row_id
					returning jobs.row_id",
					new { consumer = consumerId, now, expires });
			}
		}

		private static async Task<int?> GetJob(Func<DbConnection> connect, Guid consumerId)
		{
			using (var connection = connect())
			{
				return await connection.QuerySingleOrDefaultAsync<int?>(
					@"-- get claimed job
					select row_id from jobs where claimed_by = @id 
					order by scheduled_on
					limit 1",
					new { id = consumerId });
			}
		}

		private static async Task DeleteJob(Func<DbConnection> connect, int rowId)
		{
			using (var connection = connect())
			{
				await connection.ExecuteAsync(
					@"-- get claimed job
					delete from jobs where row_id = @row_id",
					new { row_id = rowId });
			}
		}
	}
}
