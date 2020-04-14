using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.MySql.Resources;
using K4os.Xpovoc.Toolbox.Sql;
using MySql.Data.MySqlClient;
using Polly;

namespace K4os.Xpovoc.MySql
{
	public class MySqlJobStorage: AnySqlStorage<MySqlConnection>, IJobStorage
	{
		private static readonly Random Rng = new Random();

		private static double ExpRng(double limit)
		{
			const double em1 = Math.E - 1;
			double random;
			lock (Rng) random = Rng.NextDouble();
			return (Math.Exp(random) - 1) * limit / em1;
		}

		private static TimeSpan RetryInterval(int attempt) =>
			attempt <= 4
				? TimeSpan.Zero
				: ExpRng((attempt - 4) * 15)
					.NotMoreThan(1000)
					.PipeTo(TimeSpan.FromMilliseconds);

		private static readonly AsyncPolicy DeadlockPolicy = Policy
			.Handle<MySqlException>(e => e.Number == 1213)
			.WaitAndRetryForeverAsync(RetryInterval);

		private readonly Func<Task<MySqlConnection>> _connectionFactory;
		private readonly string _tablePrefix;
		private readonly Dictionary<string, string> _queryMap;
		private readonly MySqlResourceLoader _resourceLoader;

		public MySqlJobStorage(
			IJobSerializer serializer,
			IMySqlJobStorageConfig config):
			base(serializer)
		{
			if (config is null)
				throw new ArgumentNullException(nameof(config));

			_connectionFactory = ConnectionFactory(config.ConnectionString);
			_tablePrefix = config.TablePrefix ?? string.Empty;
			_resourceLoader = MySqlResourceLoader.Default;
			_queryMap = LoadQueryMap();
		}

		private Dictionary<string, string> LoadQueryMap()
		{
			var map = MySqlResourceLoader.Default.LoadQueries(_tablePrefix);
			// process loaded strings?
			return map;
		}

		private static Task Undeadlock(
			MySqlConnection connection, Func<MySqlConnection, Task> action) =>
			Undeadlock(CancellationToken.None, connection, action);

		private static Task<T> Undeadlock<T>(
			MySqlConnection connection, Func<MySqlConnection, Task<T>> action) =>
			Undeadlock(CancellationToken.None, connection, action);

		private static Task Undeadlock(
			CancellationToken token, MySqlConnection connection,
			Func<MySqlConnection, Task> action) =>
			DeadlockPolicy.ExecuteAsync(
				() => {
					token.ThrowIfCancellationRequested();
					return action(connection);
				});

		private static Task<T> Undeadlock<T>(
			CancellationToken token, MySqlConnection connection,
			Func<MySqlConnection, Task<T>> action) =>
			DeadlockPolicy.ExecuteAsync(
				() => {
					token.ThrowIfCancellationRequested();
					return action(connection);
				});

		private static Func<Task<MySqlConnection>> ConnectionFactory(string connectionString) =>
			() => Task.FromResult(new MySqlConnection(connectionString));

		protected override Task<MySqlConnection> CreateConnection() => _connectionFactory();

		protected override Task CreateDatabase(MySqlConnection connection)
		{
			var migrations = _resourceLoader.LoadMigrations(_tablePrefix);
			var migrator = new MySqlMigrator(connection, _tablePrefix, migrations);
			migrator.Install();
			return Task.CompletedTask;
		}

		public async Task<Guid> Schedule(object payload, DateTime when)
		{
			var guid = Guid.NewGuid();
			var serialized = Serialize(payload);

			Task Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["schedule"],
					new {
						job_id = guid,
						scheduled_for = when,
						payload = serialized
					});

			using (var connection = await Connect())
				await Undeadlock(connection, Action);

			return guid;
		}

		public async Task<IJob> Claim(
			CancellationToken token,
			Guid worker, DateTime now, DateTime until)
		{
			Task<JobRec> Action(IDbConnection connection) =>
				connection.QueryFirstOrDefaultAsync<JobRec>(
					_queryMap["claim"],
					new {
						claimed_by = worker,
						invisible_until = until,
						now,
					});

			Job ToJob(JobRec job) =>
				new Job(job.job_id, Deserialize(job.payload), job.attempt);

			using (var connection = await Connect())
				return (await Undeadlock(token, connection, Action))?.PipeTo(ToJob);
		}

		public async Task<bool> KeepClaim(
			CancellationToken token,
			Guid worker, Guid job, DateTime until)
		{
			Task<int> Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["keep"],
					new {
						job_id = job,
						claimed_by = worker,
						invisible_until = until,
					});

			using (var connection = await Connect())
				return await Undeadlock(token, connection, Action) > 0;
		}

		public async Task Complete(Guid worker, Guid job, DateTime now)
		{
			Task<int> Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["complete"],
					new {
						job_id = job,
						claimed_by = worker,
					});

			using (var connection = await Connect())
				await Undeadlock(connection, Action);
		}

		public async Task Retry(Guid worker, Guid job, DateTime when)
		{
			Task<int> Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["retry"],
					new {
						job_id = job,
						claimed_by = worker,
						invisible_until = when,
					});

			using (var connection = await Connect())
				await Undeadlock(connection, Action);
		}

		public async Task Forget(Guid worker, Guid job, DateTime now)
		{
			Task<int> Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["forget"],
					new {
						job_id = job,
						claimed_by = worker,
					});

			using (var connection = await Connect())
				await Undeadlock(connection, Action);
		}

		#region JobRec

		// ReSharper disable once ClassNeverInstantiated.Local
		private class JobRec
		{
#pragma warning disable 649
			// ReSharper disable InconsistentNaming
			public Guid job_id;
			public string payload;
			public int attempt;
			// ReSharper restore InconsistentNaming
#pragma warning restore 649
		}

		#endregion
	}
}
