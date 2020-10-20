using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Dapper;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Sql;
using K4os.Xpovoc.SqLite.Resources;
using Microsoft.Data.Sqlite;

namespace K4os.Xpovoc.SqLite
{
	public class SqLiteJobStorage: AnySqlStorage<SqliteConnection>, IDisposable
	{
		private readonly ConcurrentQueue<SqliteConnection> _pool =
			new ConcurrentQueue<SqliteConnection>();

		private readonly SemaphoreSlim _semaphore =
			new SemaphoreSlim(0);

		private readonly string _prefix;
		private readonly IDictionary<string, string> _queryMap;
		private readonly SqLiteResourceLoader _resourceLoader;

		private readonly Random _tokenGenerator = new Random(Guid.NewGuid().GetHashCode());

		public SqLiteJobStorage(
			ISqLiteJobStorageConfig config,
			IJobSerializer serializer = null):
			base(serializer)
		{
			if (config is null)
				throw new ArgumentNullException(nameof(config));

			_prefix = config.Prefix ?? string.Empty;
			_resourceLoader = SqLiteResourceLoader.Default;
			_queryMap = _resourceLoader.LoadQueries(_prefix);

			var poolSize = config.PoolSize.NotLessThan(1).NotMoreThan(32);
			BuildConnectionPool(poolSize, config.ConnectionString);
		}

		private void BuildConnectionPool(int count, string connectionString)
		{
			while (count-- > 0)
			{
				_pool.Enqueue(new SqliteConnection(connectionString));
				_semaphore.Release();
			}
		}

		private int ClaimToken()
		{
			lock (_tokenGenerator) return _tokenGenerator.Next();
		}

		protected override async Task<SqliteConnection> CreateConnection()
		{
			await _semaphore.WaitAsync();
			_pool.TryDequeue(out var connection);
			return connection;
		}

		protected override async Task OpenConnection(SqliteConnection connection)
		{
			await connection.OpenAsync();
			await connection.ExecuteAsync("pragma journal_mode = 'wal'");
		}

		protected override void DisposeConnection(SqliteConnection connection)
		{
			_pool.Enqueue(connection);
			_semaphore.Release();
		}

		protected override Task CreateDatabase(SqliteConnection connection)
		{
			var migrations = _resourceLoader.LoadMigrations(_prefix);
			var migrator = new SqLiteMigrator(connection, _prefix, migrations);
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

		private Task<T> Eval<T>(string queryName, object args, CancellationToken token = default)
		{
			var query = GetQuery(queryName);
			return Exec(c => c.QueryFirstOrDefaultAsync<T>(query, args), token);
		}

		public override async Task<Guid> Schedule(object payload, DateTime when)
		{
			var guid = Guid.NewGuid();
			var args = new {
				job_id = guid,
				scheduled_for = when.ToUtc(),
				payload = Serialize(payload)
			};

			await Exec("schedule", args);

			return guid;
		}

		protected override async Task<SqlJob> Claim(
			CancellationToken token,
			Guid worker, DateTime now, DateTime until)
		{
			SqlJob ToJob(JobRec rec) => new SqlJob(
				rec.row_id, Guid.Parse(rec.job_id),
				rec.scheduled_for.ToUtc(),
				Deserialize(rec.payload),
				rec.attempt
			);

			var args = new {
				claimed_by = worker,
				claim_token = ClaimToken(),
				invisible_until = until.ToUtc(),
				now,
			};

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
			public string job_id; // NOTE: not Guid
			public DateTime scheduled_for;
			public string payload;
			public int attempt;
			// ReSharper restore InconsistentNaming
#pragma warning restore 649
		}

		#endregion

		public void Dispose()
		{
			_semaphore.Dispose();

			while (!_pool.IsEmpty)
			{
				_pool.TryDequeue(out var connection);
				connection.Dispose();
			}
		}
	}
}
