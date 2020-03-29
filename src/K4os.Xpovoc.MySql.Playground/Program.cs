using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using K4os.Xpovoc.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Polly;

// ReSharper disable UnusedParameter.Local

namespace K4os.Xpovoc.MySql.Playground
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
				"server=raspberrypi;database=xpovoc;uid=root;pwd=sa;MaximumPoolSize=8";
			serviceCollection.AddTransient<DbConnection>(
				_ => new MySqlConnection(connectionString));
			serviceCollection.AddSingleton<IMySqlJobStorageConfig>(
				new MySqlJobStorageConfig {
					ConnectionString = connectionString,
					TablePrefix = "xprovoc_",
				});
		}

		private static async Task Execute(
			ILoggerFactory loggerFactory, IServiceProvider serviceProvider, string[] args)
		{
			DbConnection Connect() => serviceProvider.GetService<DbConnection>();
			ILogger Logger(string name) => loggerFactory.CreateLogger(name);

			var cancel = new CancellationTokenSource();
			var token = cancel.Token;

			var storageConfig = serviceProvider.GetRequiredService<IMySqlJobStorageConfig>();
			var storage = new MySqlJobStorage(storageConfig);
			var handler = new AdHocJobHandler(ConsumeOne);

			storage.Connect();

			// var scheduler = new Scheduler(loggerFactory, storage, handler);

			// var producer = Task.Run(() => Producer(token, scheduler), token);

			// ReSharper disable once MethodSupportsCancellation
			await Task.Run(Console.ReadLine);

			cancel.Cancel();
			// await Task.WhenAny(producer);
		}

		private static long _producedCount = 0;
		private static long _consumedCount = 0;

		private static async Task Producer(CancellationToken token, IJobScheduler scheduler)
		{
			var random = new Random();
			while (!token.IsCancellationRequested)
			{
				var delay = TimeSpan.FromSeconds(random.NextDouble() * 5);
				await scheduler.Schedule(Guid.NewGuid(), DateTimeOffset.UtcNow.Add(delay));
				var produced = Interlocked.Increment(ref _producedCount);
				if (produced % 100 == 0) Console.WriteLine("Produced: {0}", produced);
			}
		}

		private static void ConsumeOne(object payload)
		{
			var consumed = Interlocked.Increment(ref _consumedCount);
			if (consumed % 100 == 0) Console.WriteLine("Consumed: {0}", consumed);
		}

		// private static async Task Producer(
		// 	CancellationToken token, Func<DbConnection> connect, ILogger logger)
		// {
		// 	var random = new Random();
		//
		// 	while (!token.IsCancellationRequested)
		// 	{
		// 		try
		// 		{
		// 			var now = DateTime.UtcNow;
		// 			var time = now.AddSeconds(random.NextDouble() * 5);
		// 			using (var connection = connect())
		// 			{
		// 				await connection.OpenAsync(token);
		// 				await connection.ExecuteAsync(
		// 					@"-- insert job
		// 					insert into jobs (job_id, created_on, scheduled_on) 
		// 					values (@job_id, @created_on, @scheduled_on)",
		// 					new {
		// 						job_id = Guid.NewGuid(),
		// 						created_on = now,
		// 						scheduled_on = time
		// 					});
		// 			}
		//
		// 			var counter = Interlocked.Increment(ref _producedCount);
		// 			if (counter % 100 == 0) logger.LogDebug($"Inserted {counter} jobs it total");
		// 		}
		// 		catch (Exception e)
		// 		{
		// 			logger.LogError(e, "Producer failure");
		// 		}
		// 	}
		// }

		//
		//
		// private static long _producedCount = 0;
		//
		// private static async Task Producer(
		// 	CancellationToken token, Func<DbConnection> connect, ILogger logger)
		// {
		// 	var random = new Random();
		//
		// 	while (!token.IsCancellationRequested)
		// 	{
		// 		try
		// 		{
		// 			var now = DateTime.UtcNow;
		// 			var time = now.AddSeconds(random.NextDouble() * 5);
		// 			using (var connection = connect())
		// 			{
		// 				await connection.OpenAsync(token);
		// 				await connection.ExecuteAsync(
		// 					@"-- insert job
		// 					insert into jobs (job_id, created_on, scheduled_on) 
		// 					values (@job_id, @created_on, @scheduled_on)",
		// 					new {
		// 						job_id = Guid.NewGuid(),
		// 						created_on = now,
		// 						scheduled_on = time
		// 					});
		// 			}
		//
		// 			var counter = Interlocked.Increment(ref _producedCount);
		// 			if (counter % 100 == 0) logger.LogDebug($"Inserted {counter} jobs it total");
		// 		}
		// 		catch (Exception e)
		// 		{
		// 			logger.LogError(e, "Producer failure");
		// 		}
		// 	}
		// }
		//
		// private static long _consumedCount = 0;
		//
		// private static async Task Consumer(
		// 	CancellationToken token, Func<DbConnection> connect, ILogger logger)
		// {
		// 	var consumerId = Guid.NewGuid();
		// 	var random = new Random();
		//
		// 	while (!token.IsCancellationRequested)
		// 	{
		// 		try
		// 		{
		// 			var jobId = await GetJob(connect, consumerId);
		// 			if (jobId.HasValue)
		// 			{
		// 				// job execution is here
		// 				await DeadlockPolicy.ExecuteAsync(() => DeleteJob(connect, jobId.Value));
		// 				var counter = Interlocked.Increment(ref _consumedCount);
		// 				if (counter % 100 == 0)
		// 					logger.LogDebug($"Finished {counter} jobs it total");
		// 				continue;
		// 			}
		//
		// 			while (true)
		// 			{
		// 				var claimed =
		// 					await DeadlockPolicy.ExecuteAsync(() => ClaimJobs(connect, consumerId));
		// 				if (claimed > 0) break;
		//
		// 				logger.LogDebug("No jobs claimed, waiting 5s");
		// 				await Task.Delay(TimeSpan.FromSeconds(5), token);
		// 			}
		// 		}
		// 		catch (Exception e)
		// 		{
		// 			logger.LogError(e, "Consumer failure");
		// 		}
		// 	}
		// }
		//
		// private static async Task<int> ClaimJobs(Func<DbConnection> connect, Guid consumerId)
		// {
		// 	using (var connection = connect())
		// 	{
		// 		var now = DateTime.UtcNow;
		// 		var expires = now.AddMinutes(1);
		// 		return await connection.ExecuteAsync(
		// 			@"-- claim jobs 
		// 			update jobs 
		// 			set claimed_by = @consumer, claim_expires_on = @expires 
		// 			where scheduled_on <= @now and (claim_expires_on is null or claim_expires_on < @now)
		// 			order by scheduled_on
		// 			limit 1",
		// 			new { consumer = consumerId, now, expires });
		// 	}
		// }
		//
		// private static async Task<int?> GetJob(Func<DbConnection> connect, Guid consumerId)
		// {
		// 	using (var connection = connect())
		// 	{
		// 		return await connection.QuerySingleOrDefaultAsync<int?>(
		// 			@"-- get claimed job
		// 			select row_id from jobs where claimed_by = @id 
		// 			order by scheduled_on
		// 			limit 1",
		// 			new { id = consumerId });
		// 	}
		// }
		//
		// private static async Task DeleteJob(Func<DbConnection> connect, int rowId)
		// {
		// 	using (var connection = connect())
		// 	{
		// 		await connection.ExecuteAsync(
		// 			@"-- get claimed job
		// 			delete from jobs where row_id = @row_id",
		// 			new { row_id = rowId });
		// 	}
		// }
	}
}
