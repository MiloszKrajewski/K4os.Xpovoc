using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;
using K4os.Xpovoc.Core.Db;

namespace K4os.Xpovoc.Core.Sql
{
	public abstract class AnySqlStorage<TConnection>: IDbJobStorage where TConnection: DbConnection
	{
		private int _databaseCreated;

		private readonly TaskCompletionSource<bool> _databaseReady =
			new TaskCompletionSource<bool>();

		private readonly IJobSerializer _serializer;

		protected AnySqlStorage(IJobSerializer serializer)
		{
			_serializer = serializer.Required(nameof(serializer));
		}

		protected abstract Task<TConnection> CreateConnection();

		protected virtual Task OpenConnection(TConnection connection) => connection.OpenAsync();

		protected virtual void DisposeConnection(TConnection connection) => connection.Dispose();

		protected virtual async Task<ILease<TConnection>> Connect()
		{
			var lease = new Lease<TConnection>(await CreateConnection(), DisposeConnection);
			try
			{
				var connection = lease.Connection;
				if (connection.State == ConnectionState.Closed)
					await OpenConnection(connection);

				await TryCreateDatabase(connection);
				return lease;
			}
			catch
			{
				try
				{
					lease.Dispose();
				}
				catch
				{
					// ignore
				}

				throw;
			}
		}

		protected abstract Task CreateDatabase(TConnection connection);

		protected async Task TryCreateDatabase(TConnection connection)
		{
			if (Interlocked.CompareExchange(ref _databaseCreated, 1, 0) == 0)
			{
				try
				{
					await CreateDatabase(connection);
					_databaseReady.TrySetResult(true);
				}
				catch (Exception e)
				{
					_databaseReady.TrySetException(e);
				}
			}

			await _databaseReady.Task;
		}

		protected virtual async Task<T> Exec<T>(
			TConnection connection, Func<TConnection, Task<T>> action,
			CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();
			return await action(connection);
		}

		protected async Task<T> Exec<T>(
			Func<TConnection, Task<T>> action,
			CancellationToken token = default)
		{
			token.ThrowIfCancellationRequested();
			using (var lease = await Connect())
				return await Exec(lease.Connection, action, token);
		}

		protected string Serialize(object payload) => _serializer.Serialize(payload);
		protected object Deserialize(string payload) => _serializer.Deserialize(payload);

		protected abstract Task<SqlJob> Claim(
			CancellationToken token, Guid worker, DateTime now, DateTime until);

		protected abstract Task<bool> KeepClaim(
			CancellationToken token, Guid worker, SqlJob job, DateTime until);

		protected abstract Task Complete(Guid worker, SqlJob job, DateTime now);

		protected abstract Task Forget(Guid worker, SqlJob job, DateTime now);

		protected abstract Task Retry(Guid worker, SqlJob job, DateTime when);

		public abstract Task<Guid> Schedule(object payload, DateTime when);

		async Task<IDbJob> IDbJobStorage.Claim(
			CancellationToken token, Guid worker, DateTime now, DateTime until) =>
			await Claim(token, worker, now, until);

		Task<bool> IDbJobStorage.KeepClaim(
			CancellationToken token, Guid worker, IDbJob job, DateTime until) =>
			KeepClaim(token, worker, (SqlJob) job.Context, until);

		Task IDbJobStorage.Complete(Guid worker, IDbJob job, DateTime now) =>
			Complete(worker, (SqlJob) job.Context, now);

		Task IDbJobStorage.Forget(Guid worker, IDbJob job, DateTime now) =>
			Forget(worker, (SqlJob) job.Context, now);

		Task IDbJobStorage.Retry(Guid worker, IDbJob job, DateTime when) =>
			Retry(worker, (SqlJob) job.Context, when);
	}
}
