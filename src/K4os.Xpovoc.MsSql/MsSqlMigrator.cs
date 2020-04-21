using System.Collections.Generic;
using System.Data;
using Dapper;
using K4os.Xpovoc.Toolbox.Sql;

namespace K4os.Xpovoc.MsSql
{
	public class MsSqlMigrator: AnySqlMigrator
	{
		private readonly string _schema;
		private readonly string _table;
		private readonly string _schemaName;
		private readonly string _tableName;

		public MsSqlMigrator(
			IDbConnection connection,
			string schema,
			IEnumerable<IMigration> migrations):
			base(connection, migrations)
		{
			var hasSchema = !string.IsNullOrWhiteSpace(schema);
			_schemaName = schema ?? string.Empty;
			_tableName = "Migrations";

			_schema = hasSchema ? Quoted(schema) : string.Empty;
			_table = (hasSchema ? $"{_schema}." : string.Empty) + $"{Quoted(_tableName)}";
		}

		protected override void ExecuteScript(
			IDbConnection connection, IDbTransaction transaction, string script) =>
			connection.Execute(script, null, transaction);

		private static string Quoted(string name) => $@"[{name}]";

		protected override bool MigrationTableExists(IDbConnection connection) =>
			connection.QueryFirstOrDefault<int>(
				@"/* Check if table exists */ 
				select count(*) 
				from information_schema.tables 
				where table_schema = @schema and table_name = @table",
				new { schema = _schemaName, table = _tableName }) > 0;

		private void CreateXpovocSchema(IDbConnection connection)
		{
			if (string.IsNullOrWhiteSpace(_schema)) return;

			var found = connection.QueryFirstOrDefault<int>(
				@"/* Check if schema exists */
				select count(*) 
				from information_schema.schemata 
				where schema_name = @schema",
				new { schema = _schemaName }) > 0;

			if (found) return;

			connection.Execute(
				$@"/* Create Xpovoc schema */
				create schema {_schema}");
		}

		protected override void CreateMigrationTable(IDbConnection connection)
		{
			CreateXpovocSchema(connection);

			connection.Execute(
				$@"/* Create migrations table */
                create table {_table} (Id nvarchar(128) not null primary key)");
		}

		protected override bool IsMigrationApplied(
			IDbConnection connection, string id)
		{
			return connection.QueryFirst<int>(
				$"select count(*) from {_table} where Id = @id",
				new { id }) > 0;
		}

		protected override void MarkMigrationDone(
			IDbConnection connection, IDbTransaction transaction, string id)
		{
			connection.Execute(
				$@"/* Mark migration as done */
				insert into {_table} (Id) values (@id)",
				new { id },
				transaction);
		}
	}
}
