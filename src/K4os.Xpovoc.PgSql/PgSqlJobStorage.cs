using System;
using System.Collections.Generic;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.PgSql.Resources;
using Npgsql;

namespace K4os.Xpovoc.PgSql;

public class PgSqlJobStorage: AnySqlStorage<NpgsqlConnection>
{
	private readonly Func<Task<NpgsqlConnection>> _connectionFactory;
	private readonly string _schema;
	private readonly PgSqlResourceLoader _resourceLoader;
	private readonly IDictionary<string, string> _queryMap;

	public PgSqlJobStorage(
		IPgSqlJobStorageConfig config,
		IJobSerializer? serializer = null):
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

	private string GetQuery(string queryName) =>
		_queryMap.TryGetValue(queryName, out var queryText) ? queryText : queryName;

	private Task<int> Exec(string queryName, object args, CancellationToken token = default)
	{
		var query = GetQuery(queryName);
		return Exec(c => c.ExecuteAsync(query, args), token);
	}

	private Task<T?> Eval<T>(string queryName, object args, CancellationToken token = default)
	{
		var query = GetQuery(queryName);
		return Exec(c => c.QueryFirstOrDefaultAsync<T?>(query, args), token);
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

	protected override async Task<SqlJob?> Claim(
		CancellationToken token,
		Guid worker, DateTime now, DateTime until)
	{
		var args = new {
			claimed_by = worker,
			invisible_until = until.ToUtc(),
			now,
		};

		SqlJob? ToJob(JobRec job) =>
			job.payload is null ? null : new SqlJob(
				job.row_id, job.job_id,
				job.scheduled_for.ToUtc(),
				Deserialize(job.payload),
				job.attempt
			);

		return (await Eval<JobRec>("claim", args, token))?.PipeTo(ToJob);
	}

	protected override async Task<bool> KeepClaim(
		CancellationToken token, Guid worker, SqlJob job, DateTime until)
	{
		var args = new {
			row_id = job.RowId,
			claimed_by = worker,
			invisible_until = until.ToUtc(),
		};

		return await Exec("keep", args, token) > 0;
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

	protected override async Task Complete(Guid worker, SqlJob job, DateTime now)
	{
		var args = new {
			row_id = job.RowId,
			claimed_by = worker,
			max_date = DateTime.MaxValue.ToUtc(),
		};

		await Exec("complete", args);
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
	private class JobRec
	{
#pragma warning disable 649
		// ReSharper disable InconsistentNaming
		public long row_id;
		public Guid job_id;
		public DateTime scheduled_for;
		public string? payload;
		public int attempt;
		// ReSharper restore InconsistentNaming
#pragma warning restore 649
	}

	#endregion
}
