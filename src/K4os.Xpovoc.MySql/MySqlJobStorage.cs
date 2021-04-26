using System;
using System.Collections.Generic;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.MySql.Resources;
using MySql.Data.MySqlClient;

namespace K4os.Xpovoc.MySql
{
	public class MySqlJobStorage: AnySqlStorage<MySqlConnection>
	{
		private readonly Func<Task<MySqlConnection>> _connectionFactory;
		private readonly string _tablePrefix;
		private readonly IDictionary<string, string> _queryMap;
		private readonly MySqlResourceLoader _resourceLoader;
		private readonly MySqlExecutionPolicy _executionPolicy;

		public MySqlJobStorage(
			IMySqlJobStorageConfig config,
			IJobSerializer serializer = null):
			base(serializer)
		{
			config.Required(nameof(config));
			_connectionFactory = ConnectionFactory(config.ConnectionString);
			_tablePrefix = config.Prefix ?? string.Empty;
			_resourceLoader = MySqlResourceLoader.Default;
			_executionPolicy = new MySqlExecutionPolicy();
			_queryMap = _resourceLoader.LoadQueries(_tablePrefix);
		}

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

		protected override Task<T> Exec<T>(
			MySqlConnection connection, Func<MySqlConnection, Task<T>> action,
			CancellationToken token = default) =>
			_executionPolicy.Undeadlock(token, connection, action);

		private string GetQuery(string queryName) =>
			_queryMap.TryGetValue(queryName, out var queryText) ? queryText : queryName;

		private Task<int> Exec(
			string queryName, object args, CancellationToken token = default)
		{
			var query = GetQuery(queryName);
			return Exec(c => c.ExecuteAsync(query, args), token);
		}

		private Task<T> Eval<T>(
			string queryName, object args, CancellationToken token = default)
		{
			var query = GetQuery(queryName);
			return Exec(c => c.QueryFirstOrDefaultAsync<T>(query, args), token);
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

			SqlJob ToJob(JobRec job) => new SqlJob(
				job.row_id, job.job_id,
				job.scheduled_for.ToUtc(),
				Deserialize(job.payload),
				job.attempt
			);

			return (await Eval<JobRec>("claim", args, token))?.PipeTo(ToJob);
		}

		protected override async Task<bool> KeepClaim(
			CancellationToken token,
			Guid worker, SqlJob job, DateTime until)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
				invisible_until = until.ToUtc(),
			};

			return await Exec("keep", args, token) > 0;
		}

		protected override async Task Complete(Guid worker, SqlJob job, DateTime now)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
				max_date = DateTime.MaxValue.ToUtc(),
			};

			await Exec("complete", args);
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

		protected override async Task Forget(Guid worker, SqlJob job, DateTime now)
		{
			var args = new {
				row_id = job.RowId,
				claimed_by = worker,
				max_date = DateTime.MaxValue.ToUtc(),
			};

			await Exec("forget", args);
		}

		public override async Task<bool> Prune(DateTime cutoff)
		{
			var args = new {
				cutoff_date = cutoff.ToUtc(),
				max_date = DateTime.MaxValue.ToUtc(),
			};

			return await Exec("prune", args) > 0;
		}

		#region JobRec

		// ReSharper disable once ClassNeverInstantiated.Local
		// ReSharper disable InconsistentNaming
		#pragma warning disable 649
		private class JobRec
		{
			public long row_id;
			public Guid job_id;
			public DateTime scheduled_for;
			public string payload;
			public int attempt;
		}
		#pragma warning restore 649
		// ReSharper restore InconsistentNaming

		#endregion
	}
}
