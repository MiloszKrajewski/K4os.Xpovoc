using System;

namespace K4os.Xpovoc.Core.Sql
{
	internal class Lease<T>: ILease<T>
	{
		private readonly Action<T> _dispose;

		public Lease(T connection, Action<T> dispose)
		{
			_dispose = dispose;
			Connection = connection;
		}
		
		public T Connection { get; }

		public void Dispose() => _dispose(Connection);
	}
}
