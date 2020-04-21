using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.MsSql.Resources;
using K4os.Xpovoc.Toolbox.Sql;

namespace K4os.Xpovoc.MsSql
{
	public class MsSqlJobStorage: AnySqlStorage<SqlConnection>, IJobStorage
	{
		private readonly Func<Task<SqlConnection>> _connectionFactory;
		private readonly string _schema;
		private readonly Dictionary<string, string> _queryMap;
		private readonly MsSqlResourceLoader _resourceLoader;

		public MsSqlJobStorage(
			IJobSerializer serializer,
			IMsSqlJobStorageConfig config):
			base(serializer)
		{
			if (config is null)
				throw new ArgumentNullException(nameof(config));

			_connectionFactory = ConnectionFactory(config.ConnectionString);
			_schema = config.Schema ?? string.Empty;
			_resourceLoader = MsSqlResourceLoader.Default;
			_queryMap = _resourceLoader.LoadQueries(_schema);
		}

		private static Func<Task<SqlConnection>> ConnectionFactory(string connectionString) =>
			() => Task.FromResult(new SqlConnection(connectionString));

		protected override Task<SqlConnection> CreateConnection() => _connectionFactory();

		protected override Task CreateDatabase(SqlConnection connection)
		{
			var migrations = _resourceLoader.LoadMigrations(_schema);
			var migrator = new MsSqlMigrator(connection, _schema, migrations);
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