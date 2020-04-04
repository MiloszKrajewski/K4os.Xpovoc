using System.Collections.Generic;
using System.Data;
using Dapper;
using K4os.Xpovoc.AnySql;

namespace K4os.Xpovoc.MySql
{
	public class MySqlMigrator: AnySqlMigrator
	{
		private readonly string _tablePrefix;

		public MySqlMigrator(
			IDbConnection connection,
			string tablePrefix,
			IEnumerable<IMigration> migrations):
			base(connection, migrations)
		{
			_tablePrefix = tablePrefix ?? string.Empty;
		}

		protected override bool MigrationTableExists(IDbConnection connection) =>
			connection.ExecuteScalar<string>(
				$"show tables like '{_tablePrefix}Migration'") != null;

		protected override void CreateMigrationTable(IDbConnection connection) =>
			connection.Execute(
				$@"/* Create migrations table */
                create table {_tablePrefix}Migration (
					Id nvarchar(128) not null collate utf8_general_ci, primary key (`Id`)
				)");

		protected override bool IsMigrationApplied(
			IDbConnection connection, string id)
		{
			return connection.QueryFirst<int>(
				$"select count(*) from `{_tablePrefix}Migration` where Id = @id",
				new { id }) > 0;
		}

		protected override void MarkMigrationDone(
			IDbConnection connection, IDbTransaction transaction, string id)
		{
			connection.Execute(
				$@"/* Mark migration as done */
				insert into `{_tablePrefix}Migration` (Id) values (@id)",
				new { id },
				transaction);
		}
	}
}
