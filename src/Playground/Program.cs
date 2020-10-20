using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.XPath;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Memory;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.MsSql;
using K4os.Xpovoc.MySql;
using K4os.Xpovoc.PgSql;
using K4os.Xpovoc.SqLite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ReSharper disable UnusedParameter.Local

namespace Playground
{
	internal static class Program
	{
		public static Task Compositions(string[] args)
		{
			var collection = new ServiceCollection();
			MySqlExamples.Configure(collection);
			var provider = collection.BuildServiceProvider();
			MySqlExamples.Startup(provider);
			return Task.CompletedTask;
		}

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
			var secrets = Secrets.Load(".secrets.xml");
			
			ConfigureMySql(serviceCollection, secrets);
			ConfigurePgSql(serviceCollection, secrets);
			ConfigureSqLite(serviceCollection, secrets);
			ConfigureMsSql(serviceCollection, secrets);

			serviceCollection.AddSingleton<ISchedulerConfig>(
				new SchedulerConfig {
					WorkerCount = 4,
				});
		}

		private static void ConfigureMySql(ServiceCollection serviceCollection, XDocument secrets)
		{
			var connectionString = secrets.XPathSelectElement("/secrets/mysql")?.Value;
			serviceCollection.AddSingleton<IMySqlJobStorageConfig>(
				new MySqlJobStorageConfig {
					ConnectionString = connectionString,
					Prefix = "xpovoc_"
				});
		}

		private static void ConfigurePgSql(ServiceCollection serviceCollection, XDocument secrets)
		{
			var connectionString = secrets.XPathSelectElement("/secrets/pgsql")?.Value;
			serviceCollection.AddSingleton<IPgSqlJobStorageConfig>(
				new PgSqlJobStorageConfig {
					ConnectionString = connectionString,
				});
		}
		
		private static void ConfigureMsSql(ServiceCollection serviceCollection, XDocument secrets)
		{
			var connectionString = secrets.XPathSelectElement("/secrets/mssql")?.Value;
			serviceCollection.AddSingleton<IMsSqlJobStorageConfig>(
				new MsSqlJobStorageConfig {
					ConnectionString = connectionString,
					Schema = "xpovoc"
				});
		}

		
		private static void ConfigureSqLite(ServiceCollection serviceCollection, XDocument secrets)
		{
			var connectionString = secrets.XPathSelectElement("/secrets/sqlite")?.Value;
			serviceCollection.AddSingleton<ISqLiteJobStorageConfig>(
				new SqLiteJobStorageConfig {
					ConnectionString = connectionString,
					Prefix = "xpovoc_",
					PoolSize = 1,
				});
		}

		private static async Task Execute(
			ILoggerFactory loggerFactory, IServiceProvider serviceProvider, string[] args)
		{
			var cancel = new CancellationTokenSource();
			var token = cancel.Token;
			var serializer = new DefaultJobSerializer();

			var memStorage = new MemoryJobStorage();
			var mysqlStorage = new MySqlJobStorage(
				serviceProvider.GetRequiredService<IMySqlJobStorageConfig>(), serializer);
			var postgresStorage = new PgSqlJobStorage(
				serviceProvider.GetRequiredService<IPgSqlJobStorageConfig>(), serializer);
			var sqliteStorage = new SqLiteJobStorage(
				serviceProvider.GetRequiredService<ISqLiteJobStorageConfig>(), serializer);
			var mssqlStorage = new MsSqlJobStorage(
				serviceProvider.GetRequiredService<IMsSqlJobStorageConfig>(), serializer); 

			var handler = new AdHocJobHandler(ConsumeOne);
			var schedulerConfig = serviceProvider.GetRequiredService<ISchedulerConfig>();
			var scheduler = new DbJobScheduler(null, mysqlStorage, handler, schedulerConfig);
			// var scheduler = new RxJobScheduler(loggerFactory, handler, Scheduler.Default);

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

			logger.LogInformation($"{name} started");

			await Task.Delay(TimeSpan.FromSeconds(5), token);

			logger.LogInformation($"{name} active");

			var stopwatch = Stopwatch.StartNew();

			var counter = probe();
			var timestamp = stopwatch.Elapsed.TotalSeconds;

			while (!token.IsCancellationRequested)
			{
				await Task.Delay(TimeSpan.FromSeconds(3), token);
				var delta = probe() - counter;
				var interval = stopwatch.Elapsed.TotalSeconds - timestamp;
				logger.LogDebug($"Rate({name}): {delta / interval:F1}/s ({counter})");
				counter += delta;
				timestamp += interval;
			}
		}

		private static long _producedCount;
		private static long _consumedCount;

		private static async Task Producer(CancellationToken token, IJobScheduler scheduler)
		{
			var random = new Random();
			while (!token.IsCancellationRequested)
			{
				var delay = TimeSpan.FromSeconds(random.NextDouble() * 5);
				var message = Guid.NewGuid();
				await scheduler.Schedule(DateTimeOffset.UtcNow.Add(delay), message);
				Interlocked.Increment(ref _producedCount);
			}
		}

		private static ConcurrentDictionary<Guid, object> _guids =
			new ConcurrentDictionary<Guid, object>();

		private static void ConsumeOne(object payload)
		{
			var guid = (Guid) payload;
			var result = _guids.TryAdd(guid, null);
			if (!result)
				throw new ArgumentException("Job stealing!");

			Interlocked.Increment(ref _consumedCount);
		}
	}
}
