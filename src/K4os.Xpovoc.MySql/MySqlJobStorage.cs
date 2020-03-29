using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.AnySql;
using MySql.Data.MySqlClient;
using Polly;

namespace K4os.Xpovoc.MySql
{
	public class MySqlJobStorage: AnySqlStorage<MySqlConnection>, IJobStorage
	{
		private readonly Func<Task<MySqlConnection>> _connectionFactory;
		private readonly string _tablePrefix;
		private readonly Dictionary<string, string> _queryMap;

		public MySqlJobStorage(
			IJobSerializer serializer,
			IMySqlJobStorageConfig config):
			base(serializer)
		{
			if (config is null)
				throw new ArgumentNullException(nameof(config));

			_connectionFactory = ConnectionFactory(config.ConnectionString);
			_tablePrefix = config.TablePrefix ?? string.Empty;
			_queryMap = LoadQueries();
		}
		
		private static TimeSpan RetryInterval(int attempt) =>
			TimeSpan.FromMilliseconds(Math.Min((attempt - 1) * 33, 1000));

		private static readonly AsyncPolicy DeadlockPolicy = Policy
			.Handle<MySqlException>(e => e.Number == 1213)
			.WaitAndRetryForeverAsync(RetryInterval);

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
			var xml = GetEmbeddedXml<MySqlJobStorage>("Migrations.xml");
			var migrator = new MySqlMigrationManager(connection, _tablePrefix, xml);
			migrator.Install();
			return Task.CompletedTask;
		}

		private Dictionary<string, string> LoadQueries() =>
			GetEmbeddedXml<MySqlJobStorage>("Queries.xml")
				.Elements("query")
				.Select(e => new { Id = e.Attribute("id").Required("id").Value, Text = e.Value })
				.ToDictionary(
					kv => kv.Id,
					kv => kv.Text.Replace("{prefix}", _tablePrefix));

		public async Task<Guid> Schedule(object payload, DateTimeOffset when)
		{
			var guid = Guid.NewGuid();
			var whenUtc = when.UtcDateTime;
			var serialized = Serialize(payload);

			Task Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["schedule"],
					new {
						job_id = guid,
						scheduled_for = whenUtc,
						payload = serialized
					});

			using (var connection = await Connect())
				await Undeadlock(connection, Action);

			return guid;
		}

		public Task Reschedule(Guid job, DateTimeOffset when)
		{
			throw new NotImplementedException();
		}

		public Task Cancel(Guid job) { throw new NotImplementedException(); }

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

		public async Task<IJob> Claim(
			CancellationToken token,
			Guid worker, DateTimeOffset now, DateTimeOffset until)
		{
			var nowUtc = now.UtcDateTime;
			var untilUtc = until.UtcDateTime;

			Task<JobRec> Action(IDbConnection connection) =>
				connection.QueryFirstOrDefaultAsync<JobRec>(
					_queryMap["claim"],
					new {
						claimed_by = worker,
						invisible_until = untilUtc,
						now = nowUtc,
					});

			Job ToJob(JobRec job) =>
				new Job(job.job_id, Deserialize(job.payload), job.attempt);

			using (var connection = await Connect())
				return (await Undeadlock(token, connection, Action))?.PipeTo(ToJob);
		}

		public async Task<bool> KeepClaim(
			CancellationToken token,
			Guid worker, Guid job, DateTimeOffset until)
		{
			var untilUtc = until.UtcDateTime;

			Task<int> Action(IDbConnection connection) =>
				connection.ExecuteAsync(
					_queryMap["keep"],
					new {
						job_id = job,
						claimed_by = worker,
						invisible_until = untilUtc,
					});

			using (var connection = await Connect())
				return await Undeadlock(token, connection, Action) > 0;
		}

		public async Task Complete(Guid worker, Guid job, DateTimeOffset now)
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

		public Task Retry(Guid worker, Guid job, DateTimeOffset when)
		{
			throw new NotImplementedException();
		}
	}
}
