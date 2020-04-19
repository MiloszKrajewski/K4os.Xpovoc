using System.Collections.Generic;
using System.Data;
using Dapper;
using K4os.Xpovoc.Toolbox.Sql;

namespace K4os.Xpovoc.MySql
{
	public class MySqlMigrator: AnySqlMigrator
	{
		private readonly string _table;

		public MySqlMigrator(
			IDbConnection connection,
			string prefix,
			IEnumerable<IMigration> migrations):
			base(connection, migrations)
		{
			_table = $"{prefix ?? string.Empty}Migrations";
		}

		protected override void ExecuteScript(
			IDbConnection connection, IDbTransaction transaction, string script)
		{
			connection.Execute(script, null, transaction);
		}

		protected override bool MigrationTableExists(IDbConnection connection) =>
			connection.ExecuteScalar<string>(
				$"show tables like '{_table}'") != null;

		protected override void CreateMigrationTable(IDbConnection connection) =>
			connection.Execute(
				$@"/* Create migrations table */
                create table `{_table}` (
					`Id` nvarchar(128) not null collate utf8_general_ci, primary key (`Id`)
				)");

		protected override bool IsMigrationApplied(
			IDbConnection connection, string id)
		{
			return connection.QueryFirst<int>(
				$"select count(*) from `{_table}` where `Id` = @id",
				new { id }) > 0;
		}

		protected override void MarkMigrationDone(
			IDbConnection connection, IDbTransaction transaction, string id)
		{
			connection.Execute(
				$@"/* Mark migration as done */
				insert into `{_table}` (`Id`) values (@id)",
				new { id },
				transaction);
		}
	}
}
