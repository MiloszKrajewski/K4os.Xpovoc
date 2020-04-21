using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.SqLite.Resources;
using K4os.Xpovoc.Toolbox.Sql;
using Microsoft.Data.Sqlite;

namespace K4os.Xpovoc.SqLite
{
	public class SqLiteJobStorage: AnySqlStorage<SqliteConnection>, IJobStorage, IDisposable
	{
		private readonly ConcurrentQueue<SqliteConnection> _pool = 
			new ConcurrentQueue<SqliteConnection>();
		private readonly SemaphoreSlim _semaphore = 
			new SemaphoreSlim(0);
		
		private readonly string _prefix;
		private readonly Dictionary<string, string> _queryMap;
		private readonly SqLiteResourceLoader _resourceLoader;
		
		private readonly Random _tokenGenerator = new Random(Guid.NewGuid().GetHashCode());

		public SqLiteJobStorage(
			IJobSerializer serializer,
			ISqLiteJobStorageConfig config):
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
						claim_token = ClaimToken(),
						invisible_until = until,
						now,
					});

			Job ToJob(JobRec job) =>
				new Job(Guid.Parse(job.job_id), Deserialize(job.payload), job.attempt);

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
			public string job_id;
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
