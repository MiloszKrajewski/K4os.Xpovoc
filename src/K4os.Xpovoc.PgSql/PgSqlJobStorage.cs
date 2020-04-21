using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.PgSql.Resources;
using Npgsql;

namespace K4os.Xpovoc.PgSql
{
	public class PgSqlJobStorage: AnySqlStorage<NpgsqlConnection>, IJobStorage
	{
		private readonly Func<Task<NpgsqlConnection>> _connectionFactory;
		private readonly string _schema;
		private readonly Dictionary<string, string> _queryMap;
		private readonly PgSqlResourceLoader _resourceLoader;

		public PgSqlJobStorage(
			IJobSerializer serializer,
			IPgSqlJobStorageConfig config):
			base(serializer)
		{
			if (config is null)
				throw new ArgumentNullException(nameof(config));

			_connectionFactory = ConnectionFactory(config.ConnectionString);
			_schema = config.Schema ?? string.Empty;
			_resourceLoader = PgSqlResourceLoader.Default;
			_queryMap = _resourceLoader.LoadQueries(_schema);
		}

		private static Func<Task<NpgsqlConnection>> ConnectionFactory(string connectionString) =>
			() => Task.FromResult(new NpgsqlConnection(connectionString));

		protected override Task<NpgsqlConnection> CreateConnection() => _connectionFactory();

		protected override Task CreateDatabase(NpgsqlConnection connection)
		{
			var migrations = _resourceLoader.LoadMigrations(_schema);
			var migrator = new PgSqlMigrator(connection, _schema, migrations);
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

			using (var lease = await Connect())
				await Action(lease.Connection);

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

			using (var lease = await Connect())
				return (await Action(lease.Connection))?.PipeTo(ToJob);
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

			using (var lease = await Connect())
				return await Action(lease.Connection) > 0;
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

			using (var lease = await Connect())
				await Action(lease.Connection);
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

			using (var lease = await Connect())
				await Action(lease.Connection);
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

			using (var lease = await Connect())
				await Action(lease.Connection);
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
