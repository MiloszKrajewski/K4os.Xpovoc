using Dapper;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.SqLite;
using Microsoft.Data.Sqlite;

namespace K4os.Xpovoc.Db.Test.Integrations
{
	public class SqLiteStorageTest: StorageTestBase
	{
		private string ConnectionString => Secret("/secrets/sqlite");

		protected override IDbJobStorage CreateStorage(string schema) =>
			new SqLiteJobStorage(
				new SqLiteJobStorageConfig {
					ConnectionString = ConnectionString,
					Prefix = schema,
				});

		protected override void ClearStorage(string schema)
		{
			var prefix = schema;
			using var connection = new SqliteConnection(ConnectionString);
			connection.Execute($"drop table if exists {prefix}Jobs");
			connection.Execute($"drop table if exists {prefix}Migrations");
		}

		protected override int CountJobs(string schema)
		{
			var prefix = schema;
			using var connection = new SqliteConnection(ConnectionString);
			return connection.QueryFirst<int>($"select count(*) from {prefix}Jobs");
		}
	}
}
