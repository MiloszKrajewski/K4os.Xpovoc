using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.MsSql.Resources;

namespace K4os.Xpovoc.MsSql
{
	public class MsSqlJobStorage: AnySqlStorage<SqlConnection>
	{
		private readonly Func<Task<SqlConnection>> _connectionFactory;
		private readonly string _schema;
		private readonly IDictionary<string, string> _queryMap;
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
		
		private string GetQuery(string queryName) =>
			_queryMap.TryGetValue(queryName, out var queryText) ? queryText : queryName;

		private Task<int> Exec(string queryName, object args)
		{
			var query = GetQuery(queryName);
			return Exec(c => c.ExecuteAsync(query, args));
		}

		private Task<T> Eval<T>(string queryName, object args)
		{
			var query = GetQuery(queryName);
			return Exec(c => c.QueryFirstOrDefaultAsync<T>(query, args));
		}
		
		public override async Task<Guid> Schedule(object payload, DateTime when)
		{
			var guid = Guid.NewGuid();
			var serialized = Serialize(payload);
			var args = new {
				job_id = guid,
				scheduled_for = when.ToUtc(),
				payload = serialized
			};

			await Exec("schedule", args);

			return guid;
		}
		
		protected override async Task<SqlJob> Claim(
			CancellationToken token,
			Guid worker, DateTime now, DateTime until)
		{
			var args = new {
				claimed_by = worker,
				invisible_until = until.ToUtc(),
				now,
			};

			SqlJob ToJob(JobRec job) =>
				new SqlJob(
					job.row_id, job.job_id,
					job.scheduled_for.ToUtc(),
					Deserialize(job.payload), 
					job.attempt);

			return (await Eval<JobRec>("claim", args))?.PipeTo(ToJob);
		}

		protected override async Task<bool> KeepClaim(
			CancellationToken token, Guid worker, SqlJob job, DateTime until)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
				invisible_until = until.ToUtc(),
			};

			return await Exec("keep", args) > 0;
		}

		protected override async Task Complete(Guid worker, SqlJob job, DateTime now)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
			};

			await Exec("complete", args);
		}

		protected override async Task Forget(Guid worker, SqlJob job, DateTime now)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
			};

			await Exec("forget", args);
		}

		protected override async Task Retry(Guid worker, SqlJob job, DateTime when)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
				invisible_until = when.ToUtc(),
			};

			await Exec("retry", args);
		}

		#region JobRec

		// ReSharper disable once ClassNeverInstantiated.Local
		private class JobRec
		{
#pragma warning disable 649
			// ReSharper disable InconsistentNaming
			public long row_id;
			public Guid job_id;
			public DateTime scheduled_for;
			public string payload;
			public int attempt;
			// ReSharper restore InconsistentNaming
#pragma warning restore 649
		}

		#endregion
	}
}
