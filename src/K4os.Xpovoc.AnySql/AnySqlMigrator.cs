using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace K4os.Xpovoc.AnySql
{
	public abstract class AnySqlMigrator
	{
		private readonly IDbConnection _connection;
		private readonly string _prefix;
		private readonly Migration[] _migrations;

		protected AnySqlMigrator(
			IDbConnection connection,
			string prefix,
			IEnumerable<IMigration> migrations)
		{
			_connection = connection ?? throw new ArgumentNullException(nameof(connection));
			_prefix = prefix ?? string.Empty;
			_migrations = (migrations ?? throw new ArgumentNullException(nameof(migrations)))
				.Select(m => new Migration(m))
				.ToArray();
		}

		protected AnySqlMigrator(
			IDbConnection connection,
			string prefix,
			XElement migrations):
			this(connection, prefix, ParseMigrationsXml(migrations)) { }

		private static IEnumerable<IMigration> ParseMigrationsXml(XElement xml)
		{
			Migration ParseElement(XElement node) =>
				new Migration(
					node.Attribute("id").Required("id").Value,
					node.Value);

			return xml
				.Elements("migration")
				.Select(ParseElement)
				.ToArray();
		}

		public void Install()
		{
			EnsureMigrationsTable(_connection, _prefix);

			foreach (var migration in _migrations)
			{
				var alreadyApplied = IsMigrationApplied(_connection, _prefix, migration.Id);
				if (alreadyApplied) continue;

				ApplyMigration(_connection, _prefix, migration.Id, migration.Script);
			}
		}

		private void EnsureMigrationsTable(IDbConnection connection, string prefix)
		{
			var tableExists = MigrationTableExists(connection, prefix);
			if (tableExists) return;

			CreateMigrationTable(connection, prefix);
		}

		private void ApplyMigration(
			IDbConnection connection, string prefix, string id, string script)
		{
			// NOTE: Some operations cannot be executed in transactions (create table?)
			// we will need some mechanism to handle that if we need it
			using (var transaction = connection.BeginTransaction())
			{
				var statement = GetFormattedScript(script, prefix);
				connection.Execute(statement, null, transaction);
				MarkMigrationDone(connection, transaction, prefix, id);
				transaction.Commit();
			}
		}

		private static string GetFormattedScript(string script, string tablesPrefix)
		{
			var sb = new StringBuilder(script);
			sb.Replace("{prefix}", tablesPrefix);
			return sb.ToString();
		}

		protected abstract bool MigrationTableExists(
			IDbConnection connection, string prefix);

		protected abstract void CreateMigrationTable(IDbConnection connection, string prefix);

		protected abstract bool IsMigrationApplied(
			IDbConnection connection, string prefix, string id);

		protected abstract void MarkMigrationDone(
			IDbConnection connection, IDbTransaction transaction, string prefix, string id);
	}
}
