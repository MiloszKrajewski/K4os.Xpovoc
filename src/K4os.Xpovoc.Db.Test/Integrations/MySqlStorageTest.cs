using System.Threading.Tasks;
using Dapper;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.MySql;
using MySql.Data.MySqlClient;
using Xunit;

namespace K4os.Xpovoc.Db.Test.Integrations
{
	public class MySqlStorageTest: StorageTestBase
	{
		private string ConnectionString => Secret("/secrets/mysql");

		protected override IDbJobStorage CreateStorage(string schema) =>
			new MySqlJobStorage(
				new MySqlJobStorageConfig {
					ConnectionString = ConnectionString,
					Prefix = schema,
				});

		protected override void ClearStorage(string schema)
		{
			var prefix = schema;
			using var connection = new MySqlConnection(ConnectionString);
			connection.Execute($"drop table if exists {prefix}Jobs");
			connection.Execute($"drop table if exists {prefix}Migrations");
		}

		protected override int CountJobs(string schema)
		{
			var prefix = schema;
			using var connection = new MySqlConnection(ConnectionString);
			return connection.QueryFirst<int>($"select count(*) from {prefix}Jobs");
		}
		
		// [Fact]
		// public Task MassPruningDoesNotThrowExceptions() => 
		// 	MassPruningDoesNotThrowExceptionsImpl();
	}
}
