using System;

namespace K4os.Xpovoc.Core.Sql
{
	internal class Lease<T>: ILease<T>
	{
		private readonly T _connection;
		private readonly Action<T> _dispose;

		public Lease(T connection, Action<T> dispose)
		{
			_connection = connection;
			_dispose = dispose;
		}

		public void Dispose() => _dispose(_connection);

		public T Connection => _connection;
	}
}
