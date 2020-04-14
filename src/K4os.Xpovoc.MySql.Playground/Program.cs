using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Memory;
using K4os.Xpovoc.Toolbox.Db;
using K4os.Xpovoc.Toolbox.Sql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

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
				"server=raspberrypi;database=xpovoc;uid=root;pwd=sa;MaximumPoolSize=32;AllowUserVariables=true";
			serviceCollection.AddTransient<DbConnection>(
				_ => new MySqlConnection(connectionString));
			serviceCollection.AddSingleton<IMySqlJobStorageConfig>(
				new MySqlJobStorageConfig {
					ConnectionString = connectionString,
				});
			serviceCollection.AddSingleton<ISchedulerConfig>(
				new SchedulerConfig {
					WorkerCount = 1,
				});
		}

		private static async Task Execute(
			ILoggerFactory loggerFactory, IServiceProvider serviceProvider, string[] args)
		{
			var cancel = new CancellationTokenSource();
			var token = cancel.Token;

			var storageConfig = serviceProvider.GetRequiredService<IMySqlJobStorageConfig>();
			var storage = new MySqlJobStorage(
				new DefaultJobSerializer(), 
				storageConfig);

			var memStorage = new MemoryJobStorage();
			
			var handler = new AdHocJobHandler(ConsumeOne);
			var schedulerConfig = serviceProvider.GetRequiredService<ISchedulerConfig>();
			var scheduler = new Scheduler(null, memStorage, handler, schedulerConfig);

			// var producer = Task.CompletedTask;
			var producer = Task.Run(() => Producer(token, scheduler), token);

			// var producerSpeed = Task.CompletedTask;
			var producerSpeed = Task.Run(
				() => Measure(
					token,
					loggerFactory,
					"Produced", 
					() => Volatile.Read(ref _producedCount)));
			
			var consumedSpeed = Task.Run(
				() => Measure(
					token,
					loggerFactory,
					"Consumed", 
					() => Volatile.Read(ref _consumedCount)));


			// ReSharper disable once MethodSupportsCancellation
			await Task.Run(Console.ReadLine);

			cancel.Cancel();
			await Task.WhenAny(producer, producerSpeed, consumedSpeed);
		}

		private static async Task Measure(
			CancellationToken token, ILoggerFactory loggerFactory, string name, Func<long> probe)
		{
			var logger = loggerFactory.CreateLogger(name);
			
			await Task.Delay(TimeSpan.FromSeconds(5), token);

			var value = probe();
			while (!token.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), token);
				var diff = probe() - value;
				value += diff;
				logger.LogDebug($"Rate({name}): {diff/3.0:F1}/s ({value})");
			}
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
				Interlocked.Increment(ref _producedCount);
			}
		}

		private static void ConsumeOne(object payload)
		{
			Interlocked.Increment(ref _consumedCount);
		}
	}
}
