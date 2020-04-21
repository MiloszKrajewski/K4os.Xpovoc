using System.Collections.Generic;
using System.Data;
using Dapper;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.PgSql
{
	public class PgSqlMigrator: AnySqlMigrator
	{
		private readonly string _schema;
		private readonly string _table;

		public PgSqlMigrator(
			IDbConnection connection,
			string schema,
			IEnumerable<IMigration> migrations):
			base(connection, migrations)
		{
			var hasSchema = !string.IsNullOrWhiteSpace(schema);
			_schema = hasSchema ? Quoted(schema) : string.Empty;
			_table = (hasSchema ? $"{_schema}." : string.Empty) + $"{Quoted("Migrations")}";
		}

		protected override void ExecuteScript(
			IDbConnection connection, IDbTransaction transaction, string script) =>
			connection.Execute(script, null, transaction);

		private static string Quoted(string name) => $@"""{name}""";

		protected override bool MigrationTableExists(IDbConnection connection) =>
			// it's easier to just do "if not exists"
			false;
		
		private void CreateXpovocSchema(IDbConnection connection)
		{
			if (string.IsNullOrWhiteSpace(_schema)) return;

			connection.Execute(
				$@"/* Create Xpovoc schema */
				create schema if not exists {_schema}");
		}

		protected override void CreateMigrationTable(IDbConnection connection)
		{
			CreateXpovocSchema(connection);

			connection.Execute(
				$@"/* Create migrations table */
                create table if not exists {_table} (Id varchar(128) not null primary key)");
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
