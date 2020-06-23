using System.Collections.Generic;
using System.Data;
using Dapper;
using K4os.Xpovoc.Core.Sql;

namespace K4os.Xpovoc.SqLite
{
	public class SqLiteMigrator: AnySqlMigrator
	{
		private readonly string _table;

		public SqLiteMigrator(
			IDbConnection connection,
			string prefix,
			IEnumerable<IMigration> migrations):
			base("Xpovoc", connection, migrations)
		{
			_table = $"`{prefix ?? string.Empty}Migrations`";
		}

		protected override void ExecuteScript(
			IDbConnection connection, IDbTransaction transaction, string script) =>
			connection.Execute(script, null, transaction);

		protected override bool MigrationTableExists(IDbConnection connection) =>
			// it's easier to just do "if not exists"
			false;
		
		protected override void CreateMigrationTable(IDbConnection connection)
		{
			connection.Execute(
				$@"/* Create migrations table */
                create table if not exists {_table} (Id text not null primary key)");
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
