using Dapper;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.PgSql;
using Npgsql;

namespace K4os.Xpovoc.Db.Test.Integrations;

public class PgSqlStorageTest: StorageTestBase
{
	private string ConnectionString => Secret("/secrets/pgsql");

	protected override IDbJobStorage CreateStorage(string schema) =>
		new PgSqlJobStorage(
			new PgSqlJobStorageConfig {
				ConnectionString = ConnectionString,
				Schema = schema,
			});

	protected override void ClearStorage(string schema)
	{
		var prefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
		using var connection = new NpgsqlConnection(ConnectionString);
		if (!string.IsNullOrEmpty(schema))
			connection.Execute($"create schema if not exists \"{schema}\"");
		connection.Execute($"drop table if exists {prefix}\"Jobs\"");
		connection.Execute($"drop table if exists {prefix}\"Migrations\"");
	}

	protected override int CountJobs(string schema)
	{
		var prefix = string.IsNullOrEmpty(schema) ? string.Empty : $"\"{schema}\".";
		using var connection = new NpgsqlConnection(ConnectionString);
		return connection.QueryFirst<int>($"select count(*) from {prefix}\"Jobs\"");
	}
		
	// [Fact]
	// public Task MassPruningDoesNotThrowExceptions() => 
	// 	MassPruningDoesNotThrowExceptionsImpl();
}