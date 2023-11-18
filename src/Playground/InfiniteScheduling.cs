using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Xml.XPath;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.SQS;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.Core.Memory;
using K4os.Xpovoc.Core.Queue;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.Mongo;
using K4os.Xpovoc.MsSql;
using K4os.Xpovoc.MySql;
using K4os.Xpovoc.PgSql;
using K4os.Xpovoc.Redis;
using K4os.Xpovoc.SqLite;
using K4os.Xpovoc.Sqs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Playground.Utilities;
using StackExchange.Redis;

// ReSharper disable UnusedParameter.Local

namespace Playground;

internal static class InfiniteScheduling
{
	private static readonly int ProduceDelay = 0;
	private static readonly int ConsumeDelay = 0;
	private static readonly int ConsumeThreads = 4;
	private static readonly bool EnablePruning = true;

	public static Task Compositions(string[] args)
	{
		var collection = new ServiceCollection();
		MySqlExamples.Configure(collection);
		var provider = collection.BuildServiceProvider();
		MySqlExamples.Startup(provider);
		return Task.CompletedTask;
	}

	public static async Task Run(ILoggerFactory loggerFactory)
	{
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton(loggerFactory);

		Configure(serviceCollection);
		var serviceProvider = serviceCollection.BuildServiceProvider();

		await Execute(loggerFactory, serviceProvider);
	}

	private static void Configure(ServiceCollection serviceCollection)
	{
		var secrets = Secrets.Load("databases.xml").Required();

		serviceCollection.AddSingleton<IJobSerializer, DefaultJobSerializer>();
		serviceCollection.AddSingleton<IJobHandler>(
			p => new AdHocJobHandler(ConsumeOne));

		ConfigureMemory(serviceCollection);
		ConfigureMySql(serviceCollection, secrets);
		ConfigurePgSql(serviceCollection, secrets);
		ConfigureSqLite(serviceCollection, secrets);
		ConfigureMsSql(serviceCollection, secrets);
		ConfigureMongo(serviceCollection, secrets);
		ConfigureRedis(serviceCollection, secrets);
		ConfigureSqs(serviceCollection, secrets);

		serviceCollection.AddSingleton<ISchedulerConfig>(
			new SchedulerConfig {
				WorkerCount = ConsumeThreads,
				KeepFinishedJobsPeriod = EnablePruning ? TimeSpan.Zero : TimeSpan.FromDays(90),
				PruneInterval = EnablePruning ? TimeSpan.FromSeconds(1) : TimeSpan.FromDays(1),
			});
	}

	private static void ConfigureMemory(ServiceCollection serviceCollection)
	{
		serviceCollection.AddTransient<MemoryJobStorage>(
			p => new MemoryJobStorage());
	}

	private static void ConfigureMySql(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/mysql")?.Value;
		serviceCollection.AddSingleton<IMySqlJobStorageConfig>(
			new MySqlJobStorageConfig {
				ConnectionString = connectionString.Required(),
				Prefix = "xpovoc_",
			});
		serviceCollection.AddSingleton<MySqlJobStorage>(
			p => new MySqlJobStorage(
				p.GetRequiredService<IMySqlJobStorageConfig>(),
				p.GetRequiredService<IJobSerializer>()));
	}

	private static void ConfigurePgSql(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/pgsql")?.Value;
		serviceCollection.AddSingleton<IPgSqlJobStorageConfig>(
			new PgSqlJobStorageConfig {
				ConnectionString = connectionString.Required(),
			});
		serviceCollection.AddSingleton<PgSqlJobStorage>(
			p => new PgSqlJobStorage(
				p.GetRequiredService<IPgSqlJobStorageConfig>(),
				p.GetRequiredService<IJobSerializer>()));
	}

	private static void ConfigureMsSql(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/mssql")?.Value;
		serviceCollection.AddSingleton<IMsSqlJobStorageConfig>(
			new MsSqlJobStorageConfig {
				ConnectionString = connectionString.Required(),
				Schema = "xpovoc",
			});
		serviceCollection.AddSingleton<MsSqlJobStorage>(
			p => new MsSqlJobStorage(
				p.GetRequiredService<IMsSqlJobStorageConfig>(),
				p.GetRequiredService<IJobSerializer>()));
	}

	private static void ConfigureSqLite(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/sqlite")?.Value;
		serviceCollection.AddSingleton<ISqLiteJobStorageConfig>(
			new SqLiteJobStorageConfig {
				ConnectionString = connectionString.Required(),
				Prefix = "xpovoc_",
				PoolSize = 1,
			});
		serviceCollection.AddSingleton<SqLiteJobStorage>(
			p => new SqLiteJobStorage(
				p.GetRequiredService<ISqLiteJobStorageConfig>(),
				p.GetRequiredService<IJobSerializer>()));
	}

	private static void ConfigureMongo(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/mongo")?.Value;
		serviceCollection.AddSingleton<IMongoClient>(
			p => new MongoClient(connectionString));
		serviceCollection.AddSingleton(
			p => p.GetRequiredService<IMongoClient>().GetDatabase("test"));
		serviceCollection.AddSingleton<MongoJobStorage>(
			p => new MongoJobStorage(
				p.GetRequiredService<IMongoDatabase>,
				"xpovoc_devel_jobs",
				DefaultMongoJobSerializer.Instance));
	}

	private static void ConfigureRedis(ServiceCollection serviceCollection, XDocument secrets)
	{
		var connectionString = secrets.XPathSelectElement("/secrets/redis")?.Value;
		serviceCollection.AddSingleton(
			p => ConnectionMultiplexer.Connect(connectionString).GetDatabase());
		serviceCollection.AddSingleton<IRedisJobStorageConfig>(
			p => new RedisJobStorageConfig { Prefix = "xpovoc" });
		serviceCollection.AddSingleton<RedisJobStorage>(
			p => new RedisJobStorage(
				p.GetRequiredService<IDatabase>(),
				p.GetRequiredService<IRedisJobStorageConfig>(),
				p.GetRequiredService<IJobSerializer>()));
	}

	private static void ConfigureSqs(ServiceCollection serviceCollection, XDocument secrets)
	{
		serviceCollection.AddSingleton<IAmazonSQS>(
			p => new AmazonSQSClient(new AmazonSQSConfig()));
//		serviceCollection.AddSingleton<IAmazonSQS>(
//			p => new AmazonSQSClient(
//				new AmazonSQSConfig { ServiceURL = "http://localhost:9324", }));
		serviceCollection.AddSingleton<ISqsJobQueueAdapterSettings>(
			p => new SqsJobQueueAdapterSettings {
				QueueName = "xpovoc-playground",
				JobConcurrency = 16,
				PullConcurrency = 16,
				PushConcurrency = 16,
			});
		serviceCollection.AddSingleton<IJobQueueAdapter>(
			p => new SqsJobQueueAdapter(
				NullLoggerFactory.Instance, 
				// p.GetRequiredService<ILoggerFactory>(),
				p.GetRequiredService<IAmazonSQS>(),
				p.GetRequiredService<IJobSerializer>(),
				p.GetRequiredService<ISqsJobQueueAdapterSettings>()));
		serviceCollection.AddSingleton<QueueJobScheduler>(
			p => new QueueJobScheduler(
				NullLoggerFactory.Instance, 
				// p.GetRequiredService<ILoggerFactory>(),
				p.GetRequiredService<IJobQueueAdapter>(),
				p.GetRequiredService<IJobHandler>()));
	}

	private static async Task Execute(
		ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
	{
		var cancel = new CancellationTokenSource();
		var token = cancel.Token;

//			var scheduler = new RxJobScheduler(loggerFactory, handler, Scheduler.Default);
//			var scheduler = new DbJobScheduler(loggerFactory, mysqlStorage, handler, schedulerConfig);
//			var scheduler = new DbJobScheduler(loggerFactory, redisStorage, handler, schedulerConfig);
		var scheduler = serviceProvider.GetRequiredService<QueueJobScheduler>();

		// var producer = Task.CompletedTask;
		// var producerSpeed = Task.CompletedTask;
		var producer = Task.WhenAll(
			Task.Run(() => Producer(token, scheduler), token),
			Task.Run(() => Producer(token, scheduler), token),
			Task.Run(() => Producer(token, scheduler), token),
			Task.Run(() => Producer(token, scheduler), token)
		);
		var producerSpeed = Task.Run(
			() => Measure(
				token,
				loggerFactory,
				"Produced",
				() => Volatile.Read(ref _producedCount)),
			token);

		var consumedSpeed = Task.Run(
			() => Measure(
				token,
				loggerFactory,
				"Consumed",
				() => Volatile.Read(ref _consumedCount)),
			token);

		// ReSharper disable once MethodSupportsCancellation
		await Task.Run(Console.ReadLine);

		cancel.Cancel();
		await Task.WhenAny(producer, producerSpeed, consumedSpeed);
	}

	private static async Task Measure(
		CancellationToken token, ILoggerFactory loggerFactory, string name, Func<long> probe)
	{
		var logger = loggerFactory.CreateLogger(name);

		logger.LogInformation("{Name} started", name);

		await Task.Delay(TimeSpan.FromSeconds(5), token);

		logger.LogInformation("{Name} active", name);

		var stopwatch = Stopwatch.StartNew();

		var counter = probe();
		var timestamp = stopwatch.Elapsed.TotalSeconds;

		while (!token.IsCancellationRequested)
		{
			await Task.Delay(TimeSpan.FromSeconds(3), token);
			var delta = probe() - counter;
			var interval = stopwatch.Elapsed.TotalSeconds - timestamp;
			logger.LogDebug("Rate({Name}): {Rate:0}/s ({Counter})", name, delta / interval, counter);
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
			if (ProduceDelay > 0)
				await Task.Delay(ProduceDelay, token);
			var delay = TimeSpan.FromSeconds(random.NextDouble() * 5);
			var message = Guid.NewGuid();
			var when = DateTimeOffset.UtcNow.Add(delay);
			Produced.TryAdd(message, when);
			await scheduler.Schedule(when, message);
			Interlocked.Increment(ref _producedCount);
		}
	}

	private static readonly ConcurrentDictionary<Guid, DateTimeOffset> Produced = new();
	private static readonly ConcurrentDictionary<Guid, DateTimeOffset> Consumed = new();

	private static void ConsumeOne(object payload)
	{
		if (ConsumeDelay > 0)
			Thread.Sleep(ConsumeDelay);
		var now = DateTimeOffset.UtcNow;
		var guid = (Guid)payload;
		var result = Consumed.TryAdd(guid, DateTimeOffset.UtcNow);
		if (!result)
		{
			var consumed = Consumed[guid];
			var produced = Produced[guid];
			Console.WriteLine(
				$"{guid}: scheduled {produced:HH:mm:ss.fff} consumed {consumed:HH:mm:ss.fff} now {now:HH:mm:ss.fff}");
		}

		Interlocked.Increment(ref _consumedCount);
	}
}