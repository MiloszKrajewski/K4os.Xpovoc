using Dapper;
using K4os.Xpovoc.Core.Db;
using K4os.Xpovoc.MsSql;
using Microsoft.Data.SqlClient;

namespace K4os.Xpovoc.Db.Test.Integrations;

public class MsSqlStorageTest: StorageTestBase
{
	private string ConnectionString => Secret("/secrets/mssql");

	protected override IDbJobStorage CreateStorage(string schema) =>
		new MsSqlJobStorage(
			new MsSqlJobStorageConfig {
				ConnectionString = ConnectionString,
				Schema = schema,
			});

	protected override void ClearStorage(string schema)
	{
		var prefix = string.IsNullOrWhiteSpace(schema) ? string.Empty : $"[{schema}].";
		using var connection = new SqlConnection(ConnectionString);
		connection.Execute($"drop table if exists {prefix}[Jobs]");
		connection.Execute($"drop table if exists {prefix}[Migrations]");
	}

	protected override int CountJobs(string schema)
	{
		var prefix = string.IsNullOrWhiteSpace(schema) ? string.Empty : $"[{schema}].";
		using var connection = new SqlConnection(ConnectionString);
		return connection.QueryFirst<int>($"select count(*) from {prefix}[Jobs]");
	}
		
	// [Fact]
	// public Task MassPruningDoesNotThrowExceptions() => 
	// 	MassPruningDoesNotThrowExceptionsImpl();
}