using System;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using K4os.Xpovoc.Abstractions;

namespace K4os.Xpovoc.AnySql
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

		protected virtual async Task<TConnection> Connect()
		{
			var connection = await CreateConnection();
			try
			{
				await connection.OpenAsync();
				await TryCreateDatabase(connection);
				return connection;
			}
			catch
			{
				try
				{
					connection.Dispose();
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
