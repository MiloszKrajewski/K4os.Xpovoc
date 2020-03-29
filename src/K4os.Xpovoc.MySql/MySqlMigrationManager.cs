using System.Collections.Generic;
using System.Data;
using System.Xml.Linq;
using Dapper;
using K4os.Xpovoc.AnySql;

namespace K4os.Xpovoc.MySql
{
	public class MySqlMigrationManager: AnySqlMigrator
	{
		public MySqlMigrationManager(
			IDbConnection connection,
			string prefix,
			IEnumerable<IMigration> migrations):
			base(connection, prefix, migrations) { }
		
		public MySqlMigrationManager(
			IDbConnection connection,
			string prefix,
			XElement migrations):
			base(connection, prefix, migrations) { }

		
		/*
			var migrations = xml
				.Elements("migration")
				.Select(e => new Migration(e.Attribute("id").Value, e.Value))
				.ToArray();

		 */

		// var migrations = _migrations
		// 	.Elements("migration")
		// 	.Select(e => new { id = e.Attribute("id")?.Value, script = e.Value })
		// 	.ToArray();

		// private static string GetStringResource(string resourceName)
		// {
		// 	var assembly = typeof(MySqlObjectsInstaller).GetTypeInfo().Assembly;
		//
		// 	using (var stream = assembly.GetManifestResourceStream(resourceName))
		// 	{
		// 		if (stream == null)
		// 		{
		// 			throw new InvalidOperationException(
		// 				String.Format(
		// 					"Requested resource `{0}` was not found in the assembly `{1}`.",
		// 					resourceName,
		// 					assembly));
		// 		}
		//
		// 		using (var reader = new StreamReader(stream))
		// 		{
		// 			return reader.ReadToEnd();
		// 		}
		// 	}
		// }

		protected override bool MigrationTableExists(IDbConnection connection, string prefix) =>
			connection.ExecuteScalar<string>(
				$"show tables like '{prefix}Migration'") != null;

		protected override void CreateMigrationTable(IDbConnection connection, string prefix)
		{
			connection.Execute(
				$@"/* Create migrations table */
                create table {prefix}Migration (
					Id nvarchar(128) not null collate utf8_general_ci, primary key (`Id`)
				)");
		}

		protected override bool IsMigrationApplied(
			IDbConnection connection, string prefix, string id)
		{
			return connection.QueryFirst<int>(
				$"select count(*) from `{prefix}Migration` where Id = @id",
				new { id }) > 0;
		}

		protected override void MarkMigrationDone(
			IDbConnection connection, IDbTransaction transaction, string prefix, string id)
		{
			connection.Execute(
				$@"/* Mark migration as done */
				insert into `{prefix}Migration` (Id) values (@id)",
				new { id },
				transaction);
		}
	}
}
