using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.Toolbox.Sql
{
	public abstract class AnySqlStorage<TConnection> where TConnection: DbConnection
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

		protected string Serialize(object payload) => _serializer.Serialize(payload);
		protected object Deserialize(string payload) => _serializer.Deserialize(payload);
	}
}
